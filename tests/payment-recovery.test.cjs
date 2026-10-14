'use strict';

// Run with: node --test tests/payment-recovery.test.cjs
let test;
try {
  test = require('node:test');
} catch {
  test = async function(name, fn) {
    try {
      await fn();
      console.log('✔ ' + name);
    } catch (err) {
      console.error('✖ ' + name);
      throw err;
    }
  };
}
let assert;
try {
  assert = require('node:assert/strict');
} catch {
  assert = require('assert').strict || require('assert');
}
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'app.js'), 'utf8');
const startup = /  window\.addEventListener\('hashchange', route\);[\s\S]*?  route\(\);(?=\s*\}\)\(\);\s*$)/;
assert.match(source, startup);
const restoreBindings = source.slice(source.indexOf("  window.addEventListener('hashchange', route);"),
  source.indexOf('  updateTopbarAuth();', source.indexOf("  window.addEventListener('hashchange', route);")));

function harness(respond) {
  const requests = [];
  const delays = [];
  const events = new Map();
  const reloads = [];
  const context = vm.createContext({
    document: { getElementById: () => ({ style: {} }) },
    window: { addEventListener: (name, callback) => events.set(name, callback) }, location: { hash: '#/results' },
    localStorage: { getItem: () => null, setItem() {}, removeItem() {} },
    clearTimeout() {},
    setTimeout(callback, delay) { delays.push(delay); callback(); },
    mockApi: async (method, url) => { requests.push({ method, url }); return respond(method, url); },
    recordReload: (sessionId, hash) => reloads.push({ sessionId, hash })
  });
  vm.runInContext(source.replace(startup, `
    api = mockApi;
    globalThis.recovery = {
      refresh: refreshReportResult,
      select(sessionId) { selectReportSession(sessionId); },
      current() { return { paid: isPaid, sessionId: lastResultSessionId, result: lastResult }; },
      prepareRouting() {
        ensureBank = async function () {};
        loadResultFromServer = recordReload;
      }
    };
    ${restoreBindings}
  `), context);
  context.recovery.select('session-a');
  return { recovery: context.recovery, requests, delays, location: context.location, events, reloads };
}

const preview = { paid: false, result: { isPreview: true, overall: 50 } };
const full = { paid: true, result: { isPreview: false, overall: 50 } };

test('Paid report never asks the bank', async () => {
  const h = harness(() => full);
  await h.recovery.refresh('session-a');
  assert.equal(h.requests.length, 1);
  assert.equal(h.recovery.current().paid, true);
});

test('Recovered purchase reloads the authoritative full report', async () => {
  let results = 0;
  const h = harness((method, url) => url.endsWith('/result')
    ? (++results === 1 ? preview : full) : { paid: true, status: 'paid' });
  const first = h.recovery.refresh('session-a');
  assert.equal(h.recovery.refresh('session-a'), first, 'Parallel report loads share one check');
  await first;
  assert.deepEqual(h.requests.map(r => r.method), ['GET', 'POST', 'GET']);
  assert.equal(h.requests[1].url, '/api/payments/session-a/status/check');
  assert.equal(h.recovery.current().result.isPreview, false);
});

test('Delayed callback polls local status only, then reloads full report', async () => {
  let results = 0;
  const h = harness((method, url) => {
    if (url.endsWith('/result')) return ++results === 1 ? preview : full;
    return { paid: method === 'GET', status: method === 'GET' ? 'paid' : 'pending' };
  });
  await h.recovery.refresh('session-a');
  assert.equal(h.requests.filter(r => r.method === 'POST').length, 1);
  assert.equal(h.requests[2].url, '/api/payments/session-a/status');
  assert.deepEqual(h.delays, [2000]);
  assert.equal(h.recovery.current().paid, true);
});

test('Still pending stops after three local polls without granting access', async () => {
  const h = harness((method, url) => url.endsWith('/result') ? preview : { paid: false, status: 'pending' });
  await h.recovery.refresh('session-a');
  assert.equal(h.requests.filter(r => r.method === 'POST').length, 1);
  assert.equal(h.requests.filter(r => r.url.endsWith('/status')).length, 3);
  assert.equal(h.recovery.current().paid, false);
});

for (const status of ['not_started', 'failed', 'refunded', 'cancelled', 'expired']) {
  test(`${status} does not poll or unlock a preview`, async () => {
    const h = harness((method, url) => url.endsWith('/result') ? preview : { paid: false, status });
    await h.recovery.refresh('session-a');
    assert.equal(h.requests.length, 2);
    assert.equal(h.delays.length, 0);
    assert.equal(h.recovery.current().paid, false);
  });
}

test('Bank/network failure leaves the unpaid result available', async () => {
  const h = harness((method, url) => { if (method === 'POST') throw new Error('offline'); return preview; });
  assert.equal(await h.recovery.refresh('session-a'), preview);
  assert.equal(h.recovery.current().paid, false);
});

test('Switching sessions while checking does not unlock the wrong report', async () => {
  let complete;
  const h = harness((method, url) => method === 'POST'
    ? new Promise(resolve => { complete = resolve; }) : preview);
  const loading = h.recovery.refresh('session-a');
  for (let i = 0; i < 10; i++) await Promise.resolve();
  h.recovery.select('session-b');
  complete({ paid: true, status: 'paid' });
  assert.equal(await loading, null);
  assert.equal(h.recovery.current().paid, false);
  assert.equal(h.recovery.current().sessionId, null);
  assert.equal(h.requests.filter(r => r.url.endsWith('/result')).length, 1);
});

test('Leaving the report during a check stops polling and does not reload the full result', async () => {
  let complete;
  const h = harness((method, url) => method === 'POST'
    ? new Promise(resolve => { complete = resolve; }) : preview);
  const loading = h.recovery.refresh('session-a');
  for (let i = 0; i < 10; i++) await Promise.resolve();
  h.location.hash = '#/';
  complete({ paid: false, status: 'pending' });
  await loading;
  assert.equal(h.delays.length, 0);
  assert.equal(h.requests.length, 2);
  assert.equal(h.recovery.current().paid, false);
});

test('Navigating to results revalidates instead of rendering cached payment state', async () => {
  const h = harness(() => preview);
  h.recovery.prepareRouting();
  await h.events.get('hashchange')();
  assert.deepEqual(h.reloads, [{ sessionId: 'session-a', hash: '#/results' }]);
});

test('Restoring results from browser history revalidates payment state', async () => {
  const h = harness(() => preview);
  h.recovery.prepareRouting();
  h.events.get('pageshow')({ persisted: true });
  for (let i = 0; i < 10; i++) await Promise.resolve();
  assert.equal(h.reloads.length, 1);
  h.events.get('pageshow')({ persisted: false });
  h.location.hash = '#/';
  h.events.get('pageshow')({ persisted: true });
  for (let i = 0; i < 10; i++) await Promise.resolve();
  assert.equal(h.reloads.length, 1, 'Normal startup and other pages do not start an extra check');
});
