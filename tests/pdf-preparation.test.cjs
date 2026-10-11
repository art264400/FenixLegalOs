'use strict';

// Run with: node --test tests/pdf-preparation.test.cjs
// Exercise the shipped script without loading the application or making network calls.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'app.js'), 'utf8');
const startup = /  window\.addEventListener\('hashchange', route\);[\s\S]*?  route\(\);(?=\s*\}\)\(\);\s*$)/;
assert.match(source, startup, 'The harness must replace only the terminal route startup.');

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}

function response(status = 200, body = new Blob(['%PDF-test'], { type: 'application/pdf' })) {
  return {
    ok: status >= 200 && status < 300,
    status,
    blob: async () => body,
    json: async () => body,
    clone() { return this; },
  };
}

async function flushMicrotasks() {
  for (let i = 0; i < 20; i++) await Promise.resolve();
}

function createHarness({ deferResults = false } = {}) {
  const elements = new Map();
  const downloads = [];
  const objectUrls = [];
  const requests = [];
  const resultRequests = [];
  const reportResults = new Map();
  const defaultResult = { overall: 70, risks: [], sections: [], strongAreas: [] };
  const resultData = (revisionId = null, overall = 70) => ({ result: { ...defaultResult, overall }, paid: true, reportRevisionId: revisionId, reportRevisionNumber: revisionId ? 1 : 0 });
  const timers = new Map();
  const alerts = [];
  let now = 1_000_000;
  let nextTimer = 1;

  class Element {
    constructor(id = '') {
      this.id = id;
      this.dataset = {};
      this.style = {};
      this.attributes = {};
      this.disabled = false;
      this.hidden = false;
      this.children = [];
      this.listeners = new Map();
      this._text = '';
      this._html = '';
      this.descendantIds = [];
      const classes = new Set();
      this.classList = {
        add: (...names) => names.forEach(name => classes.add(name)),
        remove: (...names) => names.forEach(name => classes.delete(name)),
        contains: name => classes.has(name),
        toggle: (name, enabled) => enabled ? classes.add(name) : classes.delete(name),
      };
    }
    set textContent(value) { this._text = String(value); this._html = String(value); }
    get textContent() { return this._text; }
    set innerHTML(value) {
      this._html = value;
      this._text = value.replace(/<[^>]*>/g, '');
      for (const id of this.descendantIds) elements.delete(id);
      this.descendantIds = [];
      // Only IDs and attributes are needed by the report UI. This also replaces
      // the report elements on rerender instead of accidentally retaining state.
      for (const match of value.matchAll(/<[a-z][^>]*\sid="([^"]+)"[^>]*>/gi)) {
        const child = new Element(match[1]);
        for (const attribute of match[0].matchAll(/([\w-]+)="([^"]*)"/g)) {
          child.setAttribute(attribute[1], attribute[2]);
        }
        child.disabled = /\sdisabled(?:\s|>|=)/.test(match[0]);
        elements.set(child.id, child);
        this.descendantIds.push(child.id);
      }
    }
    get innerHTML() { return this._html; }
    setAttribute(name, value) {
      this.attributes[name] = String(value);
      if (name.startsWith('data-')) {
        const key = name.slice(5).replace(/-([a-z])/g, (_, letter) => letter.toUpperCase());
        this.dataset[key] = String(value);
      }
    }
    getAttribute(name) { return this.attributes[name] ?? null; }
    removeAttribute(name) { delete this.attributes[name]; }
    addEventListener(name, listener) { this.listeners.set(name, listener); }
    querySelectorAll() { return []; }
    appendChild(child) { this.children.push(child); }
    removeChild(child) { this.children = this.children.filter(item => item !== child); }
    click() { downloads.push({ href: this.href, filename: this.download }); }
  }

  for (const id of ['app', 'modal-root', 'progress']) elements.set(id, new Element(id));
  const document = {
    getElementById: id => elements.get(id) || null,
    createElement: () => new Element(),
    querySelectorAll: () => [],
    body: new Element(),
  };
  const schedule = (callback, delay, interval) => {
    const id = nextTimer++;
    timers.set(id, { callback, at: now + delay, interval: interval ? delay : 0 });
    return id;
  };
  class FakeDate extends Date {
    constructor(...args) { super(...(args.length ? args : [now])); }
    static now() { return now; }
  }
  const context = vm.createContext({
    document,
    window: { scrollTo() {}, print() {} },
    location: { hash: '#/report/session-a' },
    localStorage: { getItem: () => null, setItem() {}, removeItem() {} },
    AbortController,
    Blob,
    Date: FakeDate,
    requestAnimationFrame: callback => callback(),
    setInterval: (callback, delay) => schedule(callback, delay, true),
    setTimeout: (callback, delay) => schedule(callback, delay, false),
    clearInterval: id => timers.delete(id),
    clearTimeout: id => timers.delete(id),
    URL: {
      createObjectURL(blob) { objectUrls.push(blob); return 'blob:pdf-' + objectUrls.length; },
      revokeObjectURL() {},
    },
    alert: message => alerts.push(message),
    fetch(url, options = {}) {
      if (url === '/api/events') return Promise.resolve(response());
      const resultMatch = /^\/api\/sessions\/([^/]+)\/result$/.exec(url);
      if (resultMatch) {
        const pending = deferred();
        resultRequests.push({ url, options, ...pending });
        if (!deferResults) pending.resolve(response(200, reportResults.get(resultMatch[1]) || resultData()));
        return pending.promise;
      }
      assert.match(url, /^\/api\/sessions\/[^/]+\/pdf\?revisionId=[^&]+$/, 'Unexpected network request');
      const pending = deferred();
      requests.push({ url, options, ...pending });
      return pending.promise;
    },
  });
  const exports = `
  globalThis.pdfTest = {
    startPdfPreparation, preheatPdf, downloadPDFReport, renderPdfStatus,
    updatePdfButtonState, screenFullReport, renderFullReport, cancelInFlightPdfFetch,
    acceptReportResult, refreshReportResult, selectReportSession, route,
    configure() {
      state.sessionId = 'session-a';
      isPaid = true;
      currentUser = { id: 'user-a', termsAccepted: true };
      bank = { sections: [], questions: [] };
      acceptReportResult('session-a', { result: { overall: 70, risks: [], sections: [], strongAreas: [] }, paid: true });
    },
    setSession(sessionId) { selectReportSession(sessionId); acceptReportResult(sessionId, { result: { overall: 70, risks: [], sections: [], strongAreas: [] }, paid: true }); },
    setReAuth(callback) { requestReAuth = callback; },
    setPaymentPrompt(callback) { showPaymentPrompt = callback; },
    snapshot() {
      return { preparation: { ...pdfPreparation }, promise: pdfFetchPromise,
        controller: pdfAbortController, fetchSessionId: pdfFetchSessionId, timer: pdfWaitTimer,
        cachedBlob: cachedPdfBlob, cachedSessionId: cachedPdfSessionId, cachedRevisionId: cachedPdfRevisionId,
        sessionId: state.sessionId, resultSessionId: lastResultSessionId, revisionId: reportRevisionId, result: lastResult };
    }
  };`;
  vm.runInContext(source.replace(startup, exports), context, { filename: 'wwwroot/js/app.js' });
  const api = context.pdfTest;
  api.configure();

  return {
    api, requests, resultRequests, downloads, objectUrls, alerts, timers,
    resultData, setServerResult(sessionId, data) { reportResults.set(sessionId, data); },
    setHash(hash) { context.location.hash = hash; },
    element: id => {
      assert.ok(elements.has(id), 'Missing rendered element #' + id);
      return elements.get(id);
    },
    advance(milliseconds) {
      const end = now + milliseconds;
      while (true) {
        const next = [...timers].filter(([, timer]) => timer.at <= end)
          .sort((a, b) => a[1].at - b[1].at)[0];
        if (!next) break;
        const [id, timer] = next;
        now = timer.at;
        if (timer.interval) timer.at += timer.interval;
        else timers.delete(id);
        timer.callback();
      }
      now = end;
    },
  };
}

test('the report disables downloading while one shared PDF request is pending', async () => {
  const h = createHarness();
  h.api.renderFullReport('session-a');
  const pending = h.api.snapshot().promise;
  assert.ok(pending);
  assert.equal(h.api.startPdfPreparation('session-a'), pending);
  h.api.preheatPdf('session-a');
  assert.equal(h.requests.length, 1);
  assert.equal(h.requests[0].options.credentials, 'same-origin');
  assert.equal(h.element('download-pdf-btn').disabled, true);
  assert.equal(h.element('download-pdf-btn').getAttribute('aria-busy'), 'true');
  assert.equal(h.element('pdf-status').dataset.state, 'generating');
  assert.equal(h.element('pdf-status').getAttribute('role'), 'status');
  assert.equal(h.element('pdf-status').getAttribute('aria-live'), 'polite');
  assert.match(h.element('pdf-status-description').textContent, /2 минут/);
  assert.equal(h.api.snapshot().preparation.sessionId, 'session-a');
  assert.ok(h.api.snapshot().timer);

  const blob = new Blob(['%PDF-ready']);
  h.requests[0].resolve(response(200, blob));
  assert.equal(await pending, blob);
  assert.equal(h.element('pdf-status').dataset.state, 'ready');
  assert.equal(h.element('download-pdf-btn').disabled, false);
  assert.equal(h.element('download-pdf-btn').getAttribute('aria-busy'), 'false');
  assert.equal(h.api.snapshot().cachedBlob, blob);
  assert.equal(h.api.snapshot().cachedSessionId, 'session-a');
  assert.equal(h.api.snapshot().promise, null);
  assert.equal(h.api.snapshot().fetchSessionId, null);
  assert.equal(h.api.snapshot().controller, null);
  assert.equal(h.api.snapshot().timer, null);
  assert.equal(h.timers.size, 0);

  await h.api.downloadPDFReport();
  assert.equal(h.requests.length, 1);
  assert.equal(h.objectUrls[0], blob);
  assert.equal(h.downloads[0].filename, 'Fenix_SLS_Report_session-a.pdf');
});

test('a failure stays visible across report rerenders until an explicit retry succeeds', async () => {
  const h = createHarness();
  h.api.renderFullReport('session-a');
  const first = h.api.snapshot().promise;
  h.requests[0].resolve(response(503, { error: 'pdf_unavailable' }));
  assert.equal(await first, null);
  assert.equal(h.element('pdf-status').dataset.state, 'error');
  assert.match(h.element('download-pdf-btn').textContent, /повтор/i);
  assert.equal(h.element('download-pdf-btn').disabled, false);
  assert.equal(h.api.snapshot().timer, null);
  h.api.renderFullReport('session-a');
  h.api.preheatPdf('session-a');
  assert.equal(h.requests.length, 1, 'A rerender must not start an automatic retry loop');
  assert.equal(h.element('pdf-status').dataset.state, 'error');

  const download = h.api.downloadPDFReport();
  await flushMicrotasks();
  assert.equal(h.requests.length, 2);
  assert.equal(h.element('download-pdf-btn').disabled, true);
  h.requests[1].resolve(response());
  await download;
  assert.equal(h.element('pdf-status').dataset.state, 'ready');
  assert.equal(h.downloads.length, 1);
  assert.equal(h.alerts.length, 0);
});

test('the long-wait message changes after two minutes and its timer stops on completion', async () => {
  const h = createHarness();
  h.api.renderFullReport('session-a');
  const pending = h.api.snapshot().promise;
  const initial = h.element('pdf-status-note').textContent;
  h.advance(119_999);
  assert.equal(h.element('pdf-status-note').textContent, initial);
  h.advance(1);
  assert.notEqual(h.element('pdf-status-note').textContent, initial);
  assert.match(h.element('pdf-status-note').textContent, /больше времени/i);
  assert.equal(h.element('pdf-status-note').hidden, false);
  assert.equal(h.requests.length, 1);
  h.requests[0].resolve(response());
  await pending;
  const readyMessage = h.element('pdf-status-description').textContent;
  assert.equal(h.timers.size, 0);
  assert.equal(h.element('pdf-status-note').hidden, true);
  h.advance(300_000);
  assert.equal(h.element('pdf-status-description').textContent, readyMessage);
});

test('rerendering a pending report preserves its start time and shared request', async () => {
  const h = createHarness();
  h.api.renderFullReport('session-a');
  const before = h.api.snapshot();
  const oldButton = h.element('download-pdf-btn');
  h.advance(60_000);
  h.api.renderFullReport('session-a');
  const after = h.api.snapshot();
  assert.notEqual(h.element('download-pdf-btn'), oldButton, 'Report DOM was actually replaced');
  assert.equal(h.element('download-pdf-btn').disabled, true);
  assert.equal(h.element('pdf-status').dataset.state, 'generating');
  assert.equal(after.preparation.startedAt, before.preparation.startedAt);
  assert.equal(after.promise, before.promise);
  assert.equal(after.timer, before.timer);
  assert.equal(h.requests.length, 1);
  h.requests[0].resolve(response());
  await after.promise;
});

test('a session switch cancels the old request and ignores its delayed blob and cleanup', async () => {
  const h = createHarness();
  h.api.renderFullReport('session-a');
  const first = h.api.snapshot();
  const delayedBlob = deferred();
  const oldResponse = response();
  oldResponse.blob = () => delayedBlob.promise;
  h.requests[0].resolve(oldResponse);
  await flushMicrotasks();

  h.api.setSession('session-b');
  h.api.renderFullReport('session-b');
  const second = h.api.snapshot();
  assert.equal(first.controller.signal.aborted, true);
  assert.equal(h.requests.length, 2);
  assert.equal(h.requests[1].url, '/api/sessions/session-b/pdf?revisionId=original');
  delayedBlob.resolve(new Blob(['%PDF-stale']));
  assert.equal(await first.promise, null);
  const afterOldCompletion = h.api.snapshot();
  assert.equal(afterOldCompletion.promise, second.promise);
  assert.equal(afterOldCompletion.controller, second.controller);
  assert.equal(afterOldCompletion.timer, second.timer);
  assert.equal(afterOldCompletion.cachedBlob, null);
  assert.equal(h.element('pdf-status').dataset.state, 'generating');
  assert.equal(afterOldCompletion.preparation.sessionId, 'session-b');

  const currentBlob = new Blob(['%PDF-current']);
  h.requests[1].resolve(response(200, currentBlob));
  assert.equal(await second.promise, currentBlob);
  assert.equal(h.api.snapshot().cachedBlob, currentBlob);
  assert.equal(h.api.snapshot().cachedSessionId, 'session-b');
  assert.equal(h.element('pdf-status').dataset.state, 'ready');
});

test('an interactive retry can reauthenticate and download after background authorization fails', async () => {
  const h = createHarness();
  let reauthCalls = 0;
  h.api.setReAuth(async () => { reauthCalls++; });
  h.api.renderFullReport('session-a');
  const background = h.api.snapshot().promise;
  h.requests[0].resolve(response(401, { error: 'unauthorized' }));
  await background;
  assert.equal(reauthCalls, 0, 'Background work must not interrupt with an auth dialog');
  assert.equal(h.element('pdf-status').dataset.state, 'error');

  const download = h.api.downloadPDFReport();
  await flushMicrotasks();
  h.requests[1].resolve(response(401, { error: 'unauthorized' }));
  await flushMicrotasks();
  assert.equal(reauthCalls, 1);
  assert.equal(h.requests.length, 3);
  assert.equal(h.requests[2].url, '/api/sessions/session-a/pdf?revisionId=original');
  h.requests[2].resolve(response());
  await download;
  assert.equal(h.element('pdf-status').dataset.state, 'ready');
  assert.equal(h.downloads.length, 1);
});

for (const error of ['forbidden_session_owner', 'terms_required']) {
  test('an interactive 403 ' + error + ' response reauthenticates before retrying', async () => {
    const h = createHarness();
    let reauthCalls = 0;
    h.api.setReAuth(async () => { reauthCalls++; });
    const download = h.api.downloadPDFReport();
    await flushMicrotasks();
    h.requests[0].resolve(response(403, { error }));
    await flushMicrotasks();
    assert.equal(reauthCalls, 1);
    assert.equal(h.requests.length, 2);
    h.requests[1].resolve(response());
    await download;
    assert.equal(h.downloads.length, 1);
    assert.equal(h.api.snapshot().preparation.status, 'ready');
  });
}

test('payment failures prompt for payment only on an interactive attempt and never reauthenticate', async () => {
  const h = createHarness();
  const paymentPrompts = [];
  let reauthCalls = 0;
  h.api.setPaymentPrompt(sessionId => paymentPrompts.push(sessionId));
  h.api.setReAuth(async () => { reauthCalls++; });
  h.api.renderFullReport('session-a');
  const background = h.api.snapshot().promise;
  h.requests[0].resolve(response(403, { error: 'payment_required' }));
  await background;
  assert.deepEqual(paymentPrompts, []);
  assert.equal(h.element('pdf-status').dataset.state, 'error');

  const download = h.api.downloadPDFReport();
  await flushMicrotasks();
  h.requests[1].resolve(response(403, { error: 'payment_required' }));
  await download;
  assert.deepEqual(paymentPrompts, ['session-a']);
  assert.equal(reauthCalls, 0);
  assert.equal(h.downloads.length, 0);
  assert.equal(h.api.snapshot().promise, null);
  assert.equal(h.api.snapshot().timer, null);
});

test('a network rejection releases the disabled state and wait timer', async () => {
  const h = createHarness();
  h.api.renderFullReport('session-a');
  const pending = h.api.snapshot().promise;
  h.requests[0].reject(new TypeError('Failed to fetch'));
  assert.equal(await pending, null);
  assert.equal(h.element('pdf-status').dataset.state, 'error');
  assert.equal(h.element('download-pdf-btn').disabled, false);
  assert.equal(h.api.snapshot().promise, null);
  assert.equal(h.api.snapshot().controller, null);
  assert.equal(h.timers.size, 0);
});

test('cancellation resets preparation and an abort rejection does not resurrect an error', async () => {
  const h = createHarness();
  h.api.renderFullReport('session-a');
  const before = h.api.snapshot();
  h.api.cancelInFlightPdfFetch();
  assert.equal(before.controller.signal.aborted, true);
  assert.equal(h.api.snapshot().promise, null);
  assert.equal(h.api.snapshot().controller, null);
  assert.equal(h.api.snapshot().fetchSessionId, null);
  assert.equal(h.api.snapshot().preparation.status, 'initial');
  assert.equal(h.api.snapshot().preparation.sessionId, null);
  assert.equal(h.api.snapshot().preparation.startedAt, null);
  assert.equal(h.timers.size, 0);
  const abortError = new Error('The operation was aborted');
  abortError.name = 'AbortError';
  h.requests[0].reject(abortError);
  assert.equal(await before.promise, null);
  assert.equal(h.api.snapshot().preparation.status, 'initial');
  assert.equal(h.api.snapshot().cachedBlob, null);
});


test('opening a report revalidates its published revision and discards an older cached PDF', async () => {
  const h = createHarness();
  const initial = h.api.startPdfPreparation('session-a');
  const oldBlob = new Blob(['%PDF-original']);
  h.requests[0].resolve(response(200, oldBlob));
  await initial;
  h.setServerResult('session-a', h.resultData('revision-1', 86));

  await h.api.screenFullReport('session-a');
  assert.equal(h.resultRequests.length, 1);
  assert.equal(h.api.snapshot().revisionId, 'revision-1');
  assert.equal(h.api.snapshot().result.overall, 86);
  assert.equal(h.api.snapshot().cachedBlob, null);
  assert.equal(h.requests[1].url, '/api/sessions/session-a/pdf?revisionId=revision-1');
  const current = h.api.snapshot().promise;
  h.requests[1].resolve(response());
  await current;
  assert.equal(h.api.snapshot().cachedRevisionId, 'revision-1');
});

test('downloading after publication never reuses the previous revision from the open tab', async () => {
  const h = createHarness();
  h.api.renderFullReport('session-a');
  const oldPreparation = h.api.snapshot().promise;
  const oldBlob = new Blob(['%PDF-original']);
  h.requests[0].resolve(response(200, oldBlob));
  await oldPreparation;
  h.setServerResult('session-a', h.resultData('revision-2', 91));

  const download = h.api.downloadPDFReport();
  await flushMicrotasks();
  assert.equal(h.resultRequests.length, 1);
  assert.equal(h.requests[1].url, '/api/sessions/session-a/pdf?revisionId=revision-2');
  assert.equal(h.downloads.length, 0);
  const correctedBlob = new Blob(['%PDF-corrected']);
  h.requests[1].resolve(response(200, correctedBlob));
  await download;
  assert.equal(h.downloads.length, 1);
  assert.equal(h.objectUrls[0], correctedBlob);
  assert.equal(h.api.snapshot().cachedRevisionId, 'revision-2');
  assert.equal(h.api.snapshot().result.overall, 91);
});

test('publication during preparation refreshes the result and retries the new PDF revision', async () => {
  const h = createHarness();
  const download = h.api.downloadPDFReport();
  await flushMicrotasks();
  assert.equal(h.requests[0].url, '/api/sessions/session-a/pdf?revisionId=original');
  h.setServerResult('session-a', h.resultData('revision-live', 93));
  h.requests[0].resolve(response(409, { error: 'report_revision_changed' }));
  await flushMicrotasks();
  assert.equal(h.resultRequests.length, 2);
  assert.equal(h.requests.length, 2);
  assert.equal(h.requests[1].url, '/api/sessions/session-a/pdf?revisionId=revision-live');
  const revised = new Blob(['%PDF-live']);
  h.requests[1].resolve(response(200, revised));
  await download;
  assert.equal(h.objectUrls[0], revised);
  assert.equal(h.downloads.length, 1);
  assert.equal(h.api.snapshot().cachedRevisionId, 'revision-live');
});

test('a response identifying a different PDF revision cannot populate the cache', async () => {
  const h = createHarness();
  h.api.renderFullReport('session-a');
  const old = h.api.snapshot().promise;
  h.setServerResult('session-a', h.resultData('revision-header', 88));
  const mismatched = response();
  mismatched.headers = { get: name => name === 'X-Report-Revision-Id' ? 'revision-header' : null };
  h.requests[0].resolve(mismatched);
  assert.equal(await old, null);
  assert.equal(h.api.snapshot().cachedBlob, null);
  assert.equal(h.requests[1].url, '/api/sessions/session-a/pdf?revisionId=revision-header');
  const current = h.api.snapshot().promise;
  h.requests[1].resolve(response());
  await current;
});

test('a delayed previous-revision blob cannot clear or overwrite the new preparation', async () => {
  const h = createHarness();
  h.api.renderFullReport('session-a');
  const previous = h.api.snapshot();
  const delayedBlob = deferred();
  const oldResponse = response();
  oldResponse.blob = () => delayedBlob.promise;
  h.requests[0].resolve(oldResponse);
  await flushMicrotasks();
  h.api.acceptReportResult('session-a', h.resultData('revision-next', 84));
  h.api.renderFullReport('session-a');
  const current = h.api.snapshot();
  assert.equal(previous.controller.signal.aborted, true);
  delayedBlob.resolve(new Blob(['%PDF-stale']));
  assert.equal(await previous.promise, null);
  assert.equal(h.api.snapshot().promise, current.promise);
  assert.equal(h.api.snapshot().controller, current.controller);
  assert.equal(h.api.snapshot().cachedBlob, null);
  const corrected = new Blob(['%PDF-next']);
  h.requests[1].resolve(response(200, corrected));
  await current.promise;
  assert.equal(h.api.snapshot().cachedRevisionId, 'revision-next');
  assert.equal(h.api.snapshot().cachedBlob, corrected);
});

test('the report URL selects its session and late results from another session are ignored', async () => {
  const h = createHarness({ deferResults: true });
  const openingA = h.api.screenFullReport('session-a');
  h.setHash('#/report/session-b');
  await h.api.route();
  await flushMicrotasks();
  assert.equal(h.api.snapshot().sessionId, 'session-b');
  assert.equal(h.api.snapshot().result, null);
  h.resultRequests[0].resolve(response(200, h.resultData('revision-a', 12)));
  await openingA;
  assert.equal(h.api.snapshot().result, null);
  h.resultRequests[1].resolve(response(200, h.resultData('revision-b', 82)));
  await flushMicrotasks();
  assert.equal(h.api.snapshot().resultSessionId, 'session-b');
  assert.equal(h.api.snapshot().result.overall, 82);
  assert.equal(h.api.snapshot().revisionId, 'revision-b');
  assert.equal(h.requests[0].url, '/api/sessions/session-b/pdf?revisionId=revision-b');
  const current = h.api.snapshot().promise;
  h.requests[0].resolve(response());
  await current;
});

test('a failed publication-pointer check blocks downloading a previously cached PDF', async () => {
  const h = createHarness({ deferResults: true });
  h.api.renderFullReport('session-a');
  const initial = h.api.snapshot().promise;
  h.requests[0].resolve(response());
  await initial;
  const download = h.api.downloadPDFReport();
  assert.equal(h.resultRequests.length, 1);
  h.resultRequests[0].reject(new TypeError('Failed to fetch'));
  await download;
  assert.equal(h.downloads.length, 0);
  assert.equal(h.requests.length, 1);
  assert.equal(h.api.snapshot().preparation.status, 'error');
});


test('publication recovery does not reopen a report after the user leaves its page', async () => {
  const h = createHarness();
  h.api.renderFullReport('session-a');
  const background = h.api.snapshot().promise;
  h.setHash('#/');
  h.element('app').innerHTML = '<p>Главная страница</p>';
  h.setServerResult('session-a', h.resultData('revision-while-away', 90));
  h.requests[0].resolve(response(409, { error: 'report_revision_changed' }));
  await background;
  assert.equal(h.element('app').innerHTML, '<p>Главная страница</p>');
  assert.equal(h.requests.length, 1);
  assert.equal(h.api.snapshot().cachedBlob, null);
});
