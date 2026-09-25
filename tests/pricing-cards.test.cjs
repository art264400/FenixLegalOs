'use strict';

// Run with: node --test tests/pricing-cards.test.cjs
// Load the shipped handlers in a VM; no application, database or external calls.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'app.js'), 'utf8');
const startup = /  window\.addEventListener\('hashchange', route\);\s+route\(\);(?=\s*\}\)\(\);\s*$)/;
assert.match(source, startup);

async function flush() {
  for (let i = 0; i < 30; i++) await Promise.resolve();
}

function harness(pricing = { priceKzt: 35000, consultationPriceKzt: 79000 }) {
  const elements = new Map();
  const requests = [];
  let focused = null;
  class Element {
    constructor(id) {
      this.id = id;
      this.style = {};
      this.attributes = {};
      this.listeners = new Map();
      this.descendants = [];
      this.value = '';
      this._html = '';
      const classes = new Set();
      this.classList = {
        toggle: (name, on) => on ? classes.add(name) : classes.delete(name),
        contains: name => classes.has(name),
      };
    }
    set innerHTML(value) {
      this._html = String(value);
      this.textContent = this._html.replace(/<[^>]*>/g, '');
      for (const id of this.descendants) elements.delete(id);
      this.descendants = [];
      for (const match of this._html.matchAll(/<([a-z][\w-]*)\b[^>]*\sid="([^"]+)"[^>]*>/gi)) {
        const node = new Element(match[2]);
        for (const attribute of match[0].matchAll(/([\w-]+)="([^"]*)"/g)) {
          node.setAttribute(attribute[1], attribute[2]);
        }
        node.value = node.getAttribute('value') || '';
        const contentStart = match.index + match[0].length;
        const contentEnd = this._html.indexOf('</' + match[1] + '>', contentStart);
        node.textContent = this._html.slice(contentStart, contentEnd).replace(/<[^>]*>/g, '');
        elements.set(node.id, node);
        this.descendants.push(node.id);
      }
    }
    get innerHTML() { return this._html; }
    setAttribute(name, value) { this.attributes[name] = String(value); }
    getAttribute(name) { return this.attributes[name] ?? null; }
    addEventListener(name, listener) { this.listeners.set(name, listener); }
    querySelectorAll() { return []; }
    scrollIntoView() {}
    focus() { focused = this.id; }
    emit(name, event = {}) { return this.listeners.get(name)?.(event); }
  }
  for (const id of ['app', 'modal-root', 'progress']) elements.set(id, new Element(id));
  const context = vm.createContext({
    document: { getElementById: id => elements.get(id) || null, querySelectorAll: () => [] },
    window: { scrollTo() {} },
    location: { hash: '#/' },
    localStorage: { getItem: () => null, setItem() {}, removeItem() {} },
    requestAnimationFrame() {}, setTimeout() {}, clearTimeout() {},
    console,
    fetch: async (url, options = {}) => {
      const body = options.body ? JSON.parse(options.body) : null;
      requests.push({ url, method: options.method, body });
      let result;
      if (url === '/api/sessions/pricing') result = pricing;
      else if (url === '/api/stats/benchmark' || url === '/api/events') result = {};
      else if (url === '/api/sessions') result = { id: 'new-session' };
      else if (url === '/api/leads' || url === '/api/sessions/new-session/pay') result = { ok: true };
      else throw new Error('Unexpected request: ' + url);
      return { ok: true, status: 200, json: async () => result };
    },
  });
  const exports = `
  let pendingAuth = null;
  openAuthModal = callback => { pendingAuth = callback; };
  screenFullReport = () => {};
  globalThis.pricingTest = {
    screenLanding, screenResults, renderPricingCard, getSelectedPriceKzt,
    authorize() { currentUser = { id: 'user-a', name: 'Founder', email: 'founder@example.test', termsAccepted: true }; },
    async completeAuth() { this.authorize(); pendingAuth(); },
    hasPendingAuth() { return Boolean(pendingAuth); },
    prepareResult() {
      state.sessionId = 'new-session';
      lastResult = { overall: 70, risks: [], sections: [], actionPlan: [], strongAreas: [] };
      isPaid = false;
    },
    snapshot() { return { selectedTier, sessionId: state.sessionId }; }
  };`;
  vm.runInContext(source.replace(startup, exports), context, { filename: 'wwwroot/js/app.js' });
  return {
    api: context.pricingTest, requests, elements,
    el: id => { assert.ok(elements.has(id), 'Missing rendered element: ' + id); return elements.get(id); },
    focus: () => focused,
  };
}

const money = amount => amount.toLocaleString('ru');

test('configured prices match both pages and actual payment follows the clicked tariff', async () => {
  const h = harness();
  h.api.authorize();
  h.api.screenLanding();
  await flush();
  assert.equal(h.el('landing-price-1').textContent.trim(), money(35000) + ' ₸');
  assert.equal(h.el('landing-price-2').textContent.trim(), money(79000) + ' ₸');
  h.el('start-btn-2').emit('click');
  await flush();
  assert.equal(h.api.snapshot().selectedTier, 'report');
  assert.equal(h.api.snapshot().sessionId, 'new-session');

  h.api.prepareResult();
  await h.api.screenResults();
  assert.equal(h.el('tier-price-report').textContent.trim(), money(35000) + ' ₸');
  assert.equal(h.el('tier-price-consultation').textContent.trim(), money(79000) + ' ₸');
  assert.equal(h.el('tier-card-report').getAttribute('aria-checked'), 'true');
  assert.ok(h.el('btn-pay-kaspi').textContent.includes(money(35000)));

  h.el('tier-card-consultation').emit('click');
  assert.equal(h.el('tier-card-report').getAttribute('aria-checked'), 'false');
  assert.equal(h.el('tier-card-consultation').getAttribute('aria-checked'), 'true');
  assert.ok(h.el('sticky-pay-btn').textContent.includes(money(79000)));
  h.el('btn-pay-demo').emit('click');
  await flush();
  const pay = h.requests.find(request => request.url.endsWith('/pay'));
  assert.equal(pay.body.amount, 79000);
  const lead = h.requests.find(request => request.url === '/api/leads');
  assert.equal(lead.body.type, 'consultation');
  assert.match(lead.body.interest, /60-мин/);
});

test('consultation selected on landing survives registration callback and session creation', async () => {
  const h = harness();
  h.api.screenLanding();
  await flush();
  h.el('start-btn-3').emit('click');
  assert.equal(h.api.hasPendingAuth(), true);
  assert.equal(h.requests.some(request => request.url === '/api/sessions'), false);
  await h.api.completeAuth();
  await flush();
  assert.equal(h.api.snapshot().sessionId, 'new-session');
  assert.equal(h.api.snapshot().selectedTier, 'consultation');
  h.api.prepareResult();
  await h.api.screenResults();
  assert.equal(h.el('tier-card-consultation').getAttribute('aria-checked'), 'true');
  assert.ok(h.el('btn-pay-kaspi').textContent.includes(money(79000)));
});

test('payment keyboard selection updates focus, radio state and both payment amounts', async () => {
  const h = harness();
  h.api.prepareResult();
  await h.api.screenResults();
  let prevented = 0;
  h.el('tier-card-consultation').emit('keydown', { key: 'ArrowLeft', preventDefault() { prevented++; } });
  assert.equal(h.focus(), 'tier-card-report');
  assert.equal(h.el('tier-card-report').getAttribute('tabindex'), '0');
  assert.equal(h.el('tier-card-consultation').getAttribute('aria-checked'), 'false');
  assert.equal(h.api.getSelectedPriceKzt(), 35000);
  assert.ok(h.el('btn-pay-kaspi').textContent.includes(money(35000)));
  assert.ok(h.el('sticky-pay-btn').textContent.includes(money(35000)));

  h.el('tier-card-consultation').emit('keydown', { key: 'Enter', preventDefault() { prevented++; } });
  assert.equal(h.el('tier-card-consultation').getAttribute('aria-checked'), 'true');
  assert.equal(h.api.getSelectedPriceKzt(), 79000);
  h.el('tier-card-report').emit('keydown', { key: ' ', preventDefault() { prevented++; } });
  assert.equal(h.el('tier-card-report').getAttribute('aria-checked'), 'true');
  assert.equal(prevented, 3);
});

test('server-configured zero prices remain zero on landing and payment', async () => {
  const h = harness({ priceKzt: 0, consultationPriceKzt: 0 });
  h.api.screenLanding();
  await flush();
  assert.equal(h.el('landing-price-1').textContent.trim(), '0 ₸');
  assert.equal(h.el('landing-price-2').textContent.trim(), '0 ₸');
  h.api.prepareResult();
  await h.api.screenResults();
  assert.equal(h.el('tier-price-report').textContent.trim(), '0 ₸');
  assert.equal(h.el('tier-price-consultation').textContent.trim(), '0 ₸');
  assert.equal(h.api.getSelectedPriceKzt(), 0);
});
