'use strict';

// Run with: node --test tests/pricing-cards.test.cjs
// Load the shipped handlers in a VM; no application, database or external calls.
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
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'app.js'), 'utf8');
const startup = /  window\.addEventListener\('hashchange', route\);[\s\S]*?  route\(\);(?=\s*\}\)\(\);\s*$)/;
assert.match(source, startup);

async function flush() {
  for (let i = 0; i < 30; i++) await Promise.resolve();
}

function harness(pricing = { priceKzt: 35000, consultationPriceKzt: 79000, paymentsEnabled: true }) {
  const elements = new Map();
  const requests = [];
  let focused = null;
  let paymentStartError = null;
  let paidReportViews = 0;
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
    recordPaidReportView: () => { paidReportViews++; },
    fetch: async (url, options = {}) => {
      const body = options.body ? JSON.parse(options.body) : null;
      requests.push({ url, method: options.method, body });
      let result;
      let status = 200;
      if (url === '/api/sessions/pricing') {
        if (pricing instanceof Error) throw pricing;
        result = typeof pricing === 'function' ? await pricing() : pricing;
      }
      else if (url === '/api/stats/benchmark' || url === '/api/events') result = {};
      else if (url === '/api/sessions') result = { id: 'new-session' };
      else if (url === '/api/payments/new-session/start') {
        result = paymentStartError ? { error: paymentStartError, message: 'Payment unavailable' } : {};
        if (paymentStartError) status = 503;
      }
      else throw new Error('Unexpected request: ' + url);
      return { ok: status >= 200 && status < 300, status, json: async () => result };
    },
  });
  const exports = `
  let pendingAuth = null;
  openAuthModal = callback => { pendingAuth = callback; };
  screenFullReport = () => recordPaidReportView();
  globalThis.pricingTest = {
    screenLanding, screenResults, renderPricingCard, getSelectedPriceKzt, fetchPricing,
    setPaid(value) { isPaid = value; },
    authorize() { currentUser = { id: 'user-a', name: 'Founder', email: 'founder@example.test', phone: '+77001234567', termsAccepted: true }; },
    async completeAuth() { this.authorize(); pendingAuth(); },
    hasPendingAuth() { return Boolean(pendingAuth); },
    prepareResult() {
      state.sessionId = 'new-session';
      state.answers = { 'question-a': 'answer-a' };
      state.currentQuestionId = 'question-b';
      lastResultSessionId = state.sessionId;
      lastResult = { overall: 70, risks: [], sections: [], actionPlan: [], strongAreas: [] };
      isPaid = false;
    },
    snapshot() { return { selectedTier, sessionId: state.sessionId, answers: state.answers, currentQuestionId: state.currentQuestionId }; }
  };`;
  vm.runInContext(source.replace(startup, exports), context, { filename: 'wwwroot/js/app.js' });
  return {
    api: context.pricingTest, requests, elements,
    setPricing(value) { pricing = value; },
    setPaymentStartError(code) { paymentStartError = code; },
    paidReportViews: () => paidReportViews,
    el: id => { assert.ok(elements.has(id), 'Missing rendered element: ' + id); return elements.get(id); },
    focus: () => focused,
  };
}

const money = amount => amount.toLocaleString('ru');

test('configured prices match both pages and card payment modal follows the clicked tariff', async () => {
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
  assert.ok(h.el('btn-pay-card').textContent.includes(money(35000)));
  assert.equal(h.elements.has('btn-pay-demo'), false);
  assert.equal(h.elements.has('btn-pay-kaspi'), false);

  h.el('tier-card-consultation').emit('click');
  assert.equal(h.el('tier-card-report').getAttribute('aria-checked'), 'false');
  assert.equal(h.el('tier-card-consultation').getAttribute('aria-checked'), 'true');
  assert.ok(h.el('sticky-pay-btn').textContent.includes(money(79000)));
  h.el('btn-pay-card').emit('click');
  await flush();
  assert.equal(h.elements.has('payment-confirm-overlay'), true);
  assert.equal(h.el('pay-phone').value, '+77001234567');
  assert.equal(h.requests.some(request => request.url === '/api/payments/new-session/start'), false);
  h.el('pay-address').value = 'Астана, ул. Абая, д. 10';
  await h.el('payment-confirm-form').emit('submit', { preventDefault() {} });
  const start = h.requests.find(request => request.url === '/api/payments/new-session/start');
  assert.equal(start.body.tariff, 'consultation');
  assert.equal(start.body.billingAddress, 'Астана, ул. Абая, д. 10');
  assert.equal(h.requests.some(request => request.url.endsWith('/pay')), false);
  assert.equal(h.requests.some(request => request.url === '/api/leads'), false);
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
  assert.ok(h.el('btn-pay-card').textContent.includes(money(79000)));
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
  assert.ok(h.el('btn-pay-card').textContent.includes(money(35000)));
  assert.ok(h.el('sticky-pay-btn').textContent.includes(money(35000)));

  h.el('tier-card-consultation').emit('keydown', { key: 'Enter', preventDefault() { prevented++; } });
  assert.equal(h.el('tier-card-consultation').getAttribute('aria-checked'), 'true');
  assert.equal(h.api.getSelectedPriceKzt(), 79000);
  h.el('tier-card-report').emit('keydown', { key: ' ', preventDefault() { prevented++; } });
  assert.equal(h.el('tier-card-report').getAttribute('aria-checked'), 'true');
  assert.equal(prevented, 3);
});

test('server-configured zero prices remain zero on landing and payment', async () => {
  const h = harness({ priceKzt: 0, consultationPriceKzt: 0, paymentsEnabled: true });
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


function assertUnavailable(h) {
  const html = h.el('app').innerHTML;
  assert.ok(html.includes('Оплата пока недоступна'));
  assert.ok(html.includes('Ответы сохранены, предварительные результаты готовы.'));
  assert.ok(html.includes('Оплата полного отчёта пока недоступна.'));
  assert.ok(html.includes('Вы можете вернуться позже — возможность оплаты будет добавлена.'));
  assert.ok(html.includes('Ваш Fenix Legal Score'));
  for (const id of ['tier-card-report', 'tier-card-consultation', 'btn-pay-card', 'sticky-pay-btn', 'payment-confirm-overlay', 'download-pdf-btn']) {
    assert.equal(h.elements.has(id), false, id + ' must not be present while payment is unavailable');
  }
  assert.equal(html.includes('tariff-grid'), false);
  assert.equal(html.includes('mobile-sticky-bar'), false);
  assert.equal(html.includes('₸'), false);
  assert.equal(html.includes('blurred-preview-layer'), false);
  assert.equal(h.requests.some(request => request.url === '/api/payments/new-session/start'), false);
}

test('disabled payments show saved results and a return button without changing answers', async () => {
  const h = harness({ priceKzt: 35000, consultationPriceKzt: 79000, paymentsEnabled: false });
  h.api.prepareResult();
  const before = JSON.stringify(h.api.snapshot());
  await h.api.screenResults();
  assertUnavailable(h);
  assert.equal(JSON.stringify(h.api.snapshot()), before);
  h.el('payment-results-btn').emit('click');
  assert.equal(h.focus(), 'preliminary-results');
  assert.equal(JSON.stringify(h.api.snapshot()), before);
});

for (const [name, pricing] of [
  ['missing payment flag', { priceKzt: 35000, consultationPriceKzt: 79000 }],
  ['non-boolean payment flag', { priceKzt: 35000, consultationPriceKzt: 79000, paymentsEnabled: 'true' }],
  ['pricing request error', new Error('offline')],
]) {
  test(name + ' fails closed without exposing purchase controls', async () => {
    const h = harness(pricing);
    h.api.prepareResult();
    await h.api.screenResults();
    assertUnavailable(h);
  });
}

test('an enabled page rechecks availability before opening payment confirmation', async () => {
  const h = harness();
  h.api.authorize();
  h.api.prepareResult();
  await h.api.screenResults();
  assert.equal(h.elements.has('btn-pay-card'), true);
  h.setPricing({ priceKzt: 35000, consultationPriceKzt: 79000, paymentsEnabled: false });
  await h.el('btn-pay-card').emit('click');
  await flush();
  assertUnavailable(h);
  assert.ok(h.requests.filter(request => request.url === '/api/sessions/pricing').length >= 2);
});

for (const code of ['payment_disabled', 'payment_gateway_not_configured']) {
  test(code + ' from an already open confirmation closes it and shows the notice', async () => {
    const h = harness();
    h.api.authorize();
    h.api.prepareResult();
    await h.api.screenResults();
    h.el('btn-pay-card').emit('click');
    await flush();
    assert.equal(h.elements.has('payment-confirm-overlay'), true);
    h.setPaymentStartError(code);
    h.el('pay-address').value = 'Астана, ул. Абая, д. 10';
    await h.el('payment-confirm-form').emit('submit', { preventDefault() {} });
    const html = h.el('app').innerHTML;
    assert.ok(html.includes('Оплата пока недоступна'));
    assert.equal(h.elements.has('payment-confirm-overlay'), false);
    assert.equal(h.elements.has('btn-pay-card'), false);
    assert.equal(h.elements.has('sticky-pay-btn'), false);
    assert.equal(h.requests.filter(request => request.url === '/api/payments/new-session/start').length, 1);
    assert.equal(h.requests.some(request => request.url.endsWith('/pay')), false);
  });
}

test('older availability responses cannot re-enable a freshly disabled page', async () => {
  let resolveOld;
  const oldRequest = new Promise(resolve => { resolveOld = resolve; });
  let requestCount = 0;
  const h = harness(() => {
    requestCount++;
    return requestCount === 1 ? oldRequest : { priceKzt: 35000, consultationPriceKzt: 79000, paymentsEnabled: false };
  });
  h.api.authorize();
  h.api.prepareResult();
  const pending = h.api.fetchPricing();
  await h.api.screenResults();
  assertUnavailable(h);
  resolveOld({ priceKzt: 35000, consultationPriceKzt: 79000, paymentsEnabled: true });
  await pending;
  assertUnavailable(h);
});

test('paid reports retain their existing access when new payments are disabled', async () => {
  const h = harness({ paymentsEnabled: false });
  h.api.prepareResult();
  h.api.setPaid(true);
  await h.api.screenResults();
  assert.equal(h.paidReportViews(), 1);
  assert.equal(h.requests.some(request => request.url === '/api/sessions/pricing'), false);
  assert.equal(h.elements.has('payment-unavailable-title'), false);
});
