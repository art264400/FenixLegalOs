'use strict';
// Browser interaction tests for the shipped editor, with a deterministic API fixture.
const { test, before, after } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const { createRequire } = require('node:module');
const localRequire = createRequire(path.join(__dirname, 'package.json'));
let playwright;
try { playwright = localRequire('playwright'); }
catch { playwright = require(path.join(os.homedir(), '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright')); }
const scriptPath = path.join(__dirname, '../wwwroot/js/admin-answer-editor.js');
const style = fs.readFileSync(path.join(__dirname, '../wwwroot/css/style.css'), 'utf8') + '\n' +
  fs.readFileSync(path.join(__dirname, '../wwwroot/css/admin-answer-editor.css'), 'utf8');
let browser;
before(async () => {
  try { browser = await playwright.chromium.launch({ headless: true }); }
  catch { browser = await playwright.chromium.launch({ headless: true, channel: process.platform === 'win32' ? 'msedge' : 'chrome' }); }
});
after(async () => { if (browser) await browser.close(); });

async function fixture(options = {}) {
  const page = await browser.newPage({ viewport: options.mobile ? { width: 390, height: 844 } : { width: 1365, height: 1000 } });
  await page.route('**/*', route => route.abort());
  await page.setContent('<!doctype html><html lang="ru"><body class="admin-page"><main id="app" class="wrap"></main></body></html>');
  await page.addStyleTag({ content: style });
  await page.addScriptTag({ path: scriptPath });
  await page.evaluate((options) => {
    const original = { 'PROD-01': 'prelaunch', 'TEAM-01': ['founders'], 'FND-C02': { founder_1: 50, founder_2: 50 } };
    const all = [
      { id: 'PROD-01', question: 'Пользуются ли продуктом реальные пользователи/клиенты?', type: 'single',
        options: [{ id: 'prelaunch', label: 'Пока нет' }, { id: 'first', label: 'Есть первые пользователи' }] },
      { id: 'PROD-02', question: 'Кто пользуется продуктом?', type: 'multiple',
        options: [{ id: 'people', label: 'Физические лица' }, { id: 'companies', label: 'Компании' }, { id: 'unknown', label: 'Не уверен(а)', exclusive: true }] },
      { id: 'TEAM-01', question: 'Кто работает над проектом?', type: 'multiple',
        options: [{ id: 'founders', label: 'Основатели' }, { id: 'contractors', label: 'Подрядчики' }, { id: 'none', label: 'Никого' }] },
      { id: 'FND-C02', question: 'Как распределены доли?', type: 'equity_inputs', options: [] },
    ];
    if (options.entity) {
      original['COR-C02C'] = [{ index: 2, jurisdiction: 'kz', roles: ['holding'] }];
      all.push({ id: 'COR-C02C', question: 'Где зарегистрированы остальные компании?', type: 'entity_builder', options: [{ id: 'holding', label: 'Холдинг' }, { id: 'hiring', label: 'Найм команды' }, { id: 'clients', label: 'Клиенты и договоры' }, { id: 'payments', label: 'Платежи и выручка' }, { id: 'ip_assets', label: 'Владение активами' }, { id: 'other', label: 'Другое' }, { id: 'unknown', label: 'Не уверен(а)' }] });
    }
    let current = JSON.parse(JSON.stringify(original));
    let draft = null;
    let publishedRevisionId = null;
    let publishedRevisionNumber = 0;
    let reason = '';
    window.fixtureRequests = [];
    window.fixtureBlocked = null;
    const render = (answers = current, reasonOverride = reason) => ({
      sessionId: 'session-test', companyName: '<script>Example</script>',
      publishedRevisionId, publishedRevisionNumber,
      answers, originalAnswers: original,
      sections: [{ id: 'product', order: 5, title: 'Продукт и пользователи', questions: all.filter(q => q.id !== 'PROD-02' || answers['PROD-01'] === 'first').map(q =>
        ({ ...q, answer: answers[q.id], originalAnswer: original[q.id], missing: !Object.hasOwn(answers, q.id) })) }],
      draft: draft ? { ...draft, reason: reasonOverride } : null,
      revisions: [{ id: 'original', number: 0, status: 'original', createdAt: '2026-10-11T12:00:00Z', pdfAvailable: true }],
    });
    window.fetch = async (url, init = {}) => {
      const method = init.method || 'GET';
      const body = init.body ? JSON.parse(init.body) : null;
      window.fixtureRequests.push({ url, method, body });
      let data;
      if (url.endsWith('/preview')) {
        const candidate = JSON.parse(JSON.stringify(body.answers));
        if (candidate['PROD-01'] !== 'first') delete candidate['PROD-02'];
        data = render(candidate, body.reason);
      } else if (method === 'PUT') {
        current = body.answers; reason = body.reason;
        draft = { id: 'revision-test', version: (draft ? draft.version : 0) + 1, status: 'draft', pdfAvailable: false };
        data = render();
      } else if (url.endsWith('/recalculate')) {
        draft.version++;
        draft.status = 'generating';
        if (options.blockGeneration) await new Promise(resolve => { window.fixtureBlocked = resolve; });
        if (options.failGeneration && !window.fixtureFailedOnce) {
          window.fixtureFailedOnce = true; draft.version++; draft.status = 'failed';
          return { status: 500, ok: false, json: async () => ({ message: 'Не удалось сформировать PDF.' }) };
        }
        draft.version++; draft.status = 'ready'; draft.pdfAvailable = true; draft.result = { overall: 72, risks: [{ title: 'Проверка договора' }] };
        data = render();
      } else if (url.endsWith('/publish')) {
        publishedRevisionId = draft.id; publishedRevisionNumber = 1; draft = null; Object.assign(original, current); data = render();
      } else if (url.endsWith('/discard')) { draft = null; current = JSON.parse(JSON.stringify(original)); reason = ''; data = render(); }
      else { data = render(); }
      return { status: 200, ok: true, json: async () => data };
    };
    window.editorControl = null;
    window.editorReady = window.AdminAnswerEditor.open(document.getElementById('app'), 'lead-test', {
      onBack() { document.getElementById('app').innerHTML = '<p>Карточка отчёта</p>'; },
    }).then(editor => { window.editorControl = editor; });
  }, options);
  await page.evaluate(() => window.editorReady);
  return page;
}
async function edit(page, id, option) {
  await page.locator('[data-edit="' + id + '"]').click();
  await page.locator('[data-option="' + option + '"]').click();
  await page.locator('[data-apply]').click();
  await page.locator('[role="dialog"]').waitFor({ state: 'hidden' });
}
async function recalculate(page) {
  await page.locator('#answer-editor-reason').fill('Уточнили ответы клиента');
  await page.locator('[data-action="recalculate"]').click();
  await page.locator('[data-action="publish"]').waitFor();
}

test('new branch is shown and blocks recalculation until required answers are entered', async () => {
  const page = await fixture();
  try {
    await edit(page, 'PROD-01', 'first');
    assert.equal(await page.locator('[data-edit="PROD-02"]').count(), 1);
    assert.equal(await page.locator('.answer-editor-tag.is-required').count(), 1);
    assert.equal(await page.locator('[data-action="recalculate"]').isDisabled(), true);
    assert.match(await page.locator('.answer-editor-old').first().innerText(), /Пока нет/);
    assert.match(await page.locator('.answer-editor-new').first().innerText(), /первые пользователи/);
    await edit(page, 'PROD-02', 'people');
    assert.equal(await page.locator('[data-action="recalculate"]').isDisabled(), false);
    await edit(page, 'PROD-01', 'prelaunch');
    assert.equal(await page.locator('[data-edit="PROD-02"]').count(), 0);
    await page.locator('[data-action="save"]').click();
    await page.getByText('Черновик сохранён.', { exact: false }).waitFor();
    const requests = await page.evaluate(() => window.fixtureRequests);
    assert.equal(Object.hasOwn(requests.findLast(r => r.method === 'PUT').body.answers, 'PROD-02'), false);
  } finally { await page.close(); }
});

test('multiple choice honors exclusive answers and renders readable labels', async () => {
  const page = await fixture();
  try {
    await page.locator('[data-edit="TEAM-01"]').click();
    await page.locator('[data-option="none"]').click();
    assert.equal(await page.locator('[data-option="founders"]').getAttribute('aria-pressed'), 'false');
    await page.locator('[data-option="contractors"]').click();
    assert.equal(await page.locator('[data-option="none"]').getAttribute('aria-pressed'), 'false');
    await page.locator('[data-apply]').click();
    await page.locator('[role="dialog"]').waitFor({ state: 'hidden' });
    assert.match(await page.locator('.answer-editor-new').innerText(), /Подрядчики/);
  } finally { await page.close(); }
});

test('equity editor rejects empty percentages and supports equal distribution', async () => {
  const page = await fixture();
  try {
    await page.locator('[data-edit="FND-C02"]').click();
    await page.locator('[data-share="founder_1"]').fill('');
    await page.locator('[data-apply]').click();
    assert.match(await page.locator('.answer-editor-modal-error').innerText(), /больше 0/);
    await page.locator('[data-add-share]').click();
    await page.locator('[data-equal-shares]').click();
    assert.equal(await page.locator('[data-share]').count(), 3);
    assert.equal(await page.locator('[data-share-total]').innerText(), '100%');
    await page.locator('[data-apply]').click();
    await page.locator('[role="dialog"]').waitFor({ state: 'hidden' });
    assert.match(await page.locator('.answer-editor-new').innerText(), /Основатель 3/);
  } finally { await page.close(); }
});

test('save, recalculate and publish use the version returned by each preceding operation', async () => {
  const page = await fixture();
  try {
    await recalculate(page);
    assert.match(await page.locator('.answer-editor-result').innerText(), /72/);
    await page.locator('[data-action="publish"]').click();
    await page.getByText('Исправленная редакция опубликована', { exact: false }).waitFor();
    const requests = await page.evaluate(() => window.fixtureRequests);
    const save = requests.find(r => r.method === 'PUT');
    assert.equal(save.body.version, 0);
    assert.equal(requests.find(r => r.url.endsWith('/recalculate')).body.version, 1);
    assert.equal(requests.find(r => r.url.endsWith('/publish')).body.version, 3);
    assert.equal(await page.locator('[data-action="publish"]').count(), 0);
  } finally { await page.close(); }
});

test('generation failure refreshes the draft version and permits a retry', async () => {
  const page = await fixture({ failGeneration: true });
  try {
    await page.locator('#answer-editor-reason').fill('Уточнили ответы клиента');
    await page.locator('[data-action="recalculate"]').click();
    await page.getByText('Не удалось сформировать PDF.', { exact: true }).waitFor();
    await page.locator('[data-action="recalculate"]').click();
    await page.locator('[data-action="publish"]').waitFor();
    const requests = await page.evaluate(() => window.fixtureRequests.filter(r => r.url.endsWith('/recalculate')));
    assert.equal(requests[0].body.version, 1);
    assert.equal(requests[1].body.version, 3);
  } finally { await page.close(); }
});

test('unsaved edits prevent publication of a PDF generated from older data', async () => {
  const page = await fixture();
  try {
    await recalculate(page);
    await page.locator('#answer-editor-reason').fill('Другая причина изменения');
    assert.equal(await page.locator('[data-action="publish"]').isDisabled(), true);
    await edit(page, 'TEAM-01', 'contractors');
    assert.equal(await page.locator('[data-action="publish"]').count(), 0);
    assert.equal(await page.locator('.answer-editor-preview-link').count(), 0);
  } finally { await page.close(); }
});

test('a detached editor does not overwrite a later screen when generation finishes', async () => {
  const page = await fixture({ blockGeneration: true });
  try {
    await page.locator('#answer-editor-reason').fill('Уточнили ответы клиента');
    await page.locator('[data-action="recalculate"]').click();
    await page.getByText('Формируем новую редакцию отчёта', { exact: true }).waitFor();
    assert.equal(await page.locator('[data-edit="TEAM-01"]').isDisabled(), true);
    await page.evaluate(() => { window.editorControl.close(); document.getElementById('app').innerHTML = '<p>Другой экран</p>'; window.fixtureBlocked(); });
    await page.waitForTimeout(50);
    assert.equal(await page.locator('#app').innerText(), 'Другой экран');
  } finally { await page.close(); }
});

test('desktop and mobile editor and modal fit their viewport', async () => {
  fs.mkdirSync(path.join(__dirname, '../tmp/admin-answer-editor'), { recursive: true });
  for (const mobile of [false, true]) {
    const page = await fixture({ mobile });
    try {
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true);
      assert.equal(await page.locator('script').count(), 1, 'Company text must be escaped, not executed as HTML.');
      await page.screenshot({ path: path.join(__dirname, '../tmp/admin-answer-editor/' + (mobile ? 'mobile' : 'desktop') + '.png'), fullPage: true });
      await page.locator('[data-edit="TEAM-01"]').click();
      const box = await page.locator('[role="dialog"]').boundingBox();
      assert.ok(box.x >= 0 && box.x + box.width <= await page.evaluate(() => window.innerWidth));
      await page.screenshot({ path: path.join(__dirname, '../tmp/admin-answer-editor/' + (mobile ? 'mobile-modal' : 'desktop-modal') + '.png') });
      await page.keyboard.press('Escape');
      assert.equal(await page.locator('[role="dialog"]').count(), 0);
      assert.equal(await page.locator('[data-edit="TEAM-01"]').evaluate(el => document.activeElement === el), true);
    } finally { await page.close(); }
  }
});

test('entity builder requires a jurisdiction and roles for newly added companies', async () => {
  const page = await fixture({ entity: true, mobile: true });
  try {
    await page.locator('[data-edit="COR-C02C"]').click();
    await page.locator('[data-add-entity]').click();
    const actions = await page.locator('[data-apply]').boundingBox();
    assert.ok(actions.y >= 0 && actions.y + actions.height <= 844, 'Apply remains visible in a long mobile dialog.');
    const dialog = await page.locator('[role="dialog"]').boundingBox();
    assert.ok(dialog.y >= 0 && dialog.y + dialog.height <= 844, 'The dialog fits the mobile viewport.');
    await page.screenshot({ path: path.join(__dirname, '../tmp/admin-answer-editor/mobile-long-entity.png') });
    await page.locator('[data-apply]').click();
    assert.match(await page.locator('.answer-editor-modal-error').innerText(), /выберите страну/);
    await page.locator('[data-jurisdiction="1"]').selectOption('us');
    await page.locator('[data-entity-role="1"][value="hiring"]').check();
    await page.locator('[data-apply]').click();
    await page.locator('[role="dialog"]').waitFor({ state: 'hidden' });
    const text = await page.locator('.answer-editor-new').innerText();
    assert.match(text, /Компания 3: США — Найм команды/);
    const payload = await page.evaluate(() => window.fixtureRequests.findLast(r => r.url.endsWith('/preview')).body);
    assert.deepEqual(payload.answers['COR-C02C'][1], { index: 3, jurisdiction: 'us', roles: ['hiring'] });
  } finally { await page.close(); }
});

test('discard sends the current version after an explicit choice and resets the draft', async () => {
  const page = await fixture();
  try {
    await edit(page, 'TEAM-01', 'contractors');
    await page.locator('[data-action="save"]').click();
    await page.locator('[data-action="discard"]').waitFor();
    page.once('dialog', dialog => dialog.accept());
    await page.locator('[data-action="discard"]').click();
    await page.getByText('Черновик удалён.', { exact: true }).waitFor();
    const request = await page.evaluate(() => window.fixtureRequests.find(r => r.url.endsWith('/discard')));
    assert.equal(request.body.version, 1);
    assert.equal(await page.locator('.answer-editor-new').count(), 0);
    assert.equal(await page.locator('[data-action="discard"]').count(), 0);
  } finally { await page.close(); }
});