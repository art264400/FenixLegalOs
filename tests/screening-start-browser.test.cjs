'use strict';

// Run with: node --test tests/screening-start-browser.test.cjs
// Use the real registration/start handlers and CSS with a deterministic, offline API.
let test, before, after;
try {
  ({ test, before, after } = require('node:test'));
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
  before = function(fn) { try { fn(); } catch {} };
  after = function(fn) { try { fn(); } catch {} };
}
let assert;
try {
  assert = require('node:assert/strict');
} catch {
  assert = require('assert').strict || require('assert');
}
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
let playwright;
try { playwright = require('playwright'); }
catch { playwright = require(path.join(os.homedir(), '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright')); }
const source = fs.readFileSync(path.join(__dirname, '../wwwroot/js/app.js'), 'utf8');
const style = fs.readFileSync(path.join(__dirname, '../wwwroot/css/style.css'), 'utf8');
const startup = /  window\.addEventListener\('hashchange', route\);[\s\S]*?  route\(\);(?=\s*\}\)\(\);\s*$)/;
assert.match(source, startup);
let browser;
before(async () => {
  try { browser = await playwright.chromium.launch({ headless: true }); }
  catch { browser = await playwright.chromium.launch({ headless: true, channel: process.platform === 'win32' ? 'msedge' : 'chrome' }); }
});
after(async () => { if (browser) await browser.close(); });

async function fixture({ mobile = false, registered = true, draft = false } = {}) {
  const page = await browser.newPage({ viewport: mobile ? { width: 390, height: 844 } : { width: 1365, height: 1000 } });
  await page.route('**/*', route => route.abort());
  await page.setContent('<!doctype html><html lang="ru"><body><main id="app"></main><div id="modal-root"></div><div id="progress"></div></body></html>');
  await page.addStyleTag({ content: style });
  await page.evaluate(({ registered, draft }) => {
    window.fixtureUser = registered ? { id: 'user-a', name: 'Test User', company: 'Profile company', termsAccepted: true } : null;
    window.fixtureRequests = [];
    window.fetch = async (url, init = {}) => {
      const body = init.body ? JSON.parse(init.body) : null;
      window.fixtureRequests.push({ url, method: init.method || 'GET', body });
      let data = {};
      if (url === '/api/auth/register') data = { sessionId: null, user: { id: 'user-a', ...body, termsAccepted: true } };
      else if (url === '/api/auth/me/reports') data = { reports: draft ? [{ id: 'saved', companyName: 'Saved company', isCompleted: false }] : [] };
      else if (url === '/api/sessions') data = { id: 'created' };
      else if (url === '/api/sessions/saved/answers') data = { answers: { 'FND-C01': 'solo' }, currentQuestionId: 'FND-C02' };
      else if (url !== '/api/events') throw new Error('Unexpected API call: ' + url);
      return { ok: true, status: 200, json: async () => data };
    };
  }, { registered, draft });
  await page.addScriptTag({ content: source.replace(startup, `
    currentUser = window.fixtureUser;
    route = async function () {};
    window.screeningTest = {
      start: startDiagnosticSession,
      snapshot() { return { sessionId: state.sessionId, answers: state.answers, currentQuestionId: state.currentQuestionId }; }
    };
  `) });
  return page;
}

for (const mobile of [false, true]) {
  test(`registration then company confirmation creates exactly one screening (${mobile ? 'mobile' : 'desktop'})`, async () => {
    const page = await fixture({ mobile, registered: false });
    try {
      await page.evaluate(() => window.screeningTest.start('report'));
      await page.locator('#auth-name').fill('Test User');
      await page.locator('#auth-position').fill('CEO');
      await page.locator('#auth-company').fill('234321');
      await page.locator('#auth-phone').fill('+77001234567');
      await page.locator('#auth-email').fill('test@example.com');
      await page.locator('#auth-password').fill('password123');
      await page.locator('#auth-consent-cb').check();
      await page.locator('#auth-submit-btn').click();
      await page.locator('#screening-start-form').waitFor();
      assert.equal(await page.locator('#screening-company').inputValue(), '234321');
      assert.equal(await page.evaluate(() => window.fixtureRequests.filter(r => r.url === '/api/sessions').length), 0);
      const box = await page.locator('#screening-start-overlay .auth-modal').boundingBox();
      const viewport = page.viewportSize();
      assert.ok(box.x >= 0 && box.x + box.width <= viewport.width + 1);
      assert.ok(box.y >= 0 && box.y + box.height <= viewport.height + 1);
      await page.locator('#screening-company').fill('Fenix Law');
      await page.locator('#screening-create-btn').click();
      await page.waitForFunction(() => window.screeningTest.snapshot().sessionId === 'created');
      const creates = await page.evaluate(() => window.fixtureRequests.filter(r => r.url === '/api/sessions'));
      assert.equal(creates.length, 1);
      assert.deepEqual(creates[0].body, { companyName: 'Fenix Law' });
    } finally { await page.close(); }
  });
}

test('continue restores the existing screening without a create request', async () => {
  const page = await fixture({ registered: true, draft: true });
  try {
    await page.evaluate(() => window.screeningTest.start());
    assert.match(await page.locator('#screening-start-overlay').innerText(), /Saved company/);
    await page.locator('#screening-resume-btn').click();
    await page.waitForFunction(() => window.screeningTest.snapshot().currentQuestionId === 'FND-C02');
    const state = await page.evaluate(() => window.screeningTest.snapshot());
    assert.equal(state.sessionId, 'saved');
    assert.equal(state.answers['FND-C01'], 'solo');
    assert.equal(await page.evaluate(() => window.fixtureRequests.filter(r => r.url === '/api/sessions').length), 0);
  } finally { await page.close(); }
});

test('closing the start dialog creates nothing', async () => {
  const page = await fixture();
  try {
    await page.evaluate(() => window.screeningTest.start());
    await page.locator('#screening-start-close').click();
    assert.equal(await page.locator('#screening-start-overlay').count(), 0);
    assert.equal(await page.evaluate(() => window.fixtureRequests.filter(r => r.url === '/api/sessions').length), 0);
  } finally { await page.close(); }
});
