/* Fenix Legal OS — admin dashboard. */
(function () {
  'use strict';

  const app = document.getElementById('app');

  function esc(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  async function api(method, url, body) {
    const res = await fetch(url, {
      method: method,
      headers: { 'Content-Type': 'application/json' },
      body: body ? JSON.stringify(body) : undefined,
    });
    if (res.status === 401) { renderLogin(); throw new Error('unauthorized'); }
    if (!res.ok) throw new Error('api_error_' + res.status);
    return res.json();
  }

  const STATUS_LABEL = {
    new: 'New', contacted: 'Contacted', qualified: 'Qualified',
    proposal: 'Proposal', client: 'Client', not_relevant: 'Not relevant', closed: 'Closed',
  };
  const HEAT_LABEL = { cold: 'Cold', warm: 'Warm', hot: 'Hot', priority: 'Priority' };
  const TIMELINE_LABEL = {
    m3: 'Раунд в ближайшие 3 месяца', m3_6: 'Раунд через 3–6 месяцев',
    m6_12: 'Раунд через 6–12 месяцев', later: 'Раунд позже', no: 'Раунд не планируется',
  };

  // -----------------------------------------------------------------------
  // Login
  // -----------------------------------------------------------------------

  function renderLogin() {
    app.innerHTML =
      '<section class="gate" style="max-width:420px;margin:80px auto">' +
        '<h2>Вход в admin</h2>' +
        '<form id="login-form" style="margin-top:20px;display:grid;gap:14px">' +
          '<div class="field"><label for="pwd">Пароль</label><input id="pwd" type="password" autocomplete="current-password"></div>' +
          '<div class="form-error" id="login-error" hidden>Неверный пароль</div>' +
          '<button class="btn" type="submit">Войти</button>' +
        '</form>' +
      '</section>';
    document.getElementById('login-form').addEventListener('submit', async function (e) {
      e.preventDefault();
      try {
        const res = await fetch('/api/admin/login', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ password: document.getElementById('pwd').value }),
        });
        if (!res.ok) throw new Error();
        renderShell('overview');
      } catch (err) {
        document.getElementById('login-error').hidden = false;
      }
    });
  }

  // -----------------------------------------------------------------------
  // Shell + tabs
  // -----------------------------------------------------------------------

  const TABS = [
    ['overview', 'Overview'],
    ['leads', 'Leads'],
    ['questions', 'Question Bank'],
    ['risks', 'Risk Library'],
  ];

  function renderShell(active, detailId) {
    const nav = TABS.map(function (t) {
      return '<button data-tab="' + t[0] + '" class="' + (t[0] === active ? 'active' : '') + '">' + t[1] + '</button>';
    }).join('');
    app.innerHTML = '<nav class="admin-nav">' + nav + '</nav><div id="tab-content"><p style="color:var(--ink-faint)">Загрузка…</p></div>';
    app.querySelectorAll('.admin-nav button').forEach(function (b) {
      b.addEventListener('click', function () { renderShell(b.getAttribute('data-tab')); });
    });
    const content = document.getElementById('tab-content');
    if (active === 'overview') loadOverview(content);
    if (active === 'leads') detailId ? loadLeadDetail(content, detailId) : loadLeads(content);
    if (active === 'questions') loadQuestions(content);
    if (active === 'risks') loadRisks(content);
  }

  async function loadOverview(el) {
    const s = await api('GET', '/api/admin/overview');
    const conv = s.diagnosticsStarted
      ? Math.round((s.diagnosticsCompleted / s.diagnosticsStarted) * 100) + '%'
      : '—';
    const cards = [
      [s.diagnosticsStarted, 'Диагностик начато'],
      [s.diagnosticsCompleted, 'Диагностик завершено'],
      [conv, 'Completion rate'],
      [s.leadsCaptured, 'Лидов получено'],
      [s.paidSessions || 0, 'Оплаченных отчётов'],
      [(s.totalRevenue || 0).toLocaleString('ru') + ' ₸', 'Выручка'],
      [s.hotLeads, 'Hot / Priority'],
      [s.consultationRequests, 'Заявок на разбор'],
    ];
    el.innerHTML = '<div class="stat-grid">' + cards.map(function (c) {
      return '<div class="stat-card"><div class="n">' + c[0] + '</div><div class="l">' + c[1] + '</div></div>';
    }).join('') + '</div>';
  }

  // -----------------------------------------------------------------------
  // Leads
  // -----------------------------------------------------------------------

  async function loadLeads(el) {
    const data = await api('GET', '/api/admin/leads');
    if (!data.leads.length) {
      el.innerHTML = '<p style="color:var(--ink-faint);padding:24px 0">Пока нет лидов. Они появятся после того, как пользователи пройдут диагностику и оставят контакты.</p>';
      return;
    }
    const rows = data.leads.map(function (l) {
      const paidBadge = l.paid
        ? '<span class="paid-badge">✓ Оплата (' + (l.paymentAmount ? l.paymentAmount.toLocaleString('ru') + ' ₸' : '') + ')</span>'
        : '<span style="color:var(--ink-faint)">—</span>';

      return '<tr class="clickable" data-id="' + esc(l.id) + '">' +
        '<td><strong>' + esc(l.name) + '</strong><br><span style="color:var(--ink-faint);font-size:12.5px">' + esc(l.company || '—') + '</span></td>' +
        '<td>' + esc(l.email) + (l.messenger ? '<br><span style="color:var(--ink-faint);font-size:12.5px">' + esc(l.messenger) + '</span>' : '') + '</td>' +
        '<td>' + (l.overall != null ? l.overall : '—') + '</td>' +
        '<td>' + (l.criticalCount != null ? l.criticalCount : '—') + '</td>' +
        '<td>' + paidBadge + '</td>' +
        '<td><span class="heat heat-' + esc(l.heatLabel) + '">' + l.heatScore + ' · ' + HEAT_LABEL[l.heatLabel] + '</span></td>' +
        '<td>' + esc(STATUS_LABEL[l.status] || l.status) + '</td>' +
        '<td style="white-space:nowrap">' + new Date(l.createdAt).toLocaleDateString('ru') + '</td>' +
        '</tr>';
    }).join('');
    el.innerHTML =
      '<table class="data"><thead><tr>' +
      '<th>Имя / компания</th><th>Контакты</th><th>Score</th><th>Critical</th><th>Оплата</th><th>Heat</th><th>Статус</th><th>Дата</th>' +
      '</tr></thead><tbody>' + rows + '</tbody></table>';
    el.querySelectorAll('tr.clickable').forEach(function (tr) {
      tr.addEventListener('click', function () { renderShell('leads', tr.getAttribute('data-id')); });
    });
  }

  async function loadLeadDetail(el, id) {
    const data = await api('GET', '/api/admin/leads/' + id);
    const l = data.lead;
    const r = data.result;

    const statusOptions = Object.keys(STATUS_LABEL).map(function (s) {
      return '<option value="' + s + '"' + (s === l.status ? ' selected' : '') + '>' + STATUS_LABEL[s] + '</option>';
    }).join('');

    const sectionScores = r ? r.sections.map(function (s) {
      return '<div class="sec-row"><span class="name">' + esc(s.title) + '</span>' +
        '<span class="bar"><i style="width:' + (s.score || 0) + '%"></i></span>' +
        '<span class="val">' + (s.score == null ? '—' : s.score) + '</span></div>';
    }).join('') : '';

    const risks = r && r.risks.length ? r.risks.map(function (x) {
      return '<div class="arow"><span class="sev sev-' + x.severity + '" style="font-size:10.5px">' + x.severity + '</span> ' +
        '<strong style="font-size:14px"> ' + esc(x.title) + '</strong></div>';
    }).join('') : '<p style="color:var(--ink-faint)">Рисков не выявлено</p>';

    const answers = data.answers.map(function (a) {
      return '<div class="arow"><div class="aq">' + esc(a.question) + '</div><div class="aa">' + esc(a.answer) + '</div></div>';
    }).join('');

    const notes = data.notes.map(function (n) {
      return '<div class="arow"><div class="aq">' + new Date(n.created_at).toLocaleString('ru') + '</div><div class="aa">' + esc(n.note) + '</div></div>';
    }).join('');

    el.innerHTML =
      '<button class="btn-ghost" id="back-to-leads">← Все лиды</button>' +
      '<div style="display:grid;grid-template-columns:1fr 1fr;gap:32px;margin-top:20px" class="lead-grid">' +
        '<div>' +
          '<h2 class="serif" style="font-size:28px">' + esc(l.name) + '</h2>' +
          '<p style="color:var(--ink-soft);margin-top:4px">' + esc(l.company || 'Компания не указана') +
            (l.website ? ' · <a href="' + esc(l.website) + '" target="_blank" rel="noopener">' + esc(l.website) + '</a>' : '') + '</p>' +
          '<div style="margin-top:16px;font-size:14.5px;line-height:2">' +
            'Email: <strong>' + esc(l.email) + '</strong><br>' +
            'Мессенджер: ' + esc(l.messenger || '—') + '<br>' +
            'Тип: ' + (l.type === 'consultation' ? 'Заявка на разбор' : 'Запрос отчёта') + '<br>' +
            'Запрос: ' + esc(l.interest || '—') + '<br>' +
            (l.sourceRiskCode ? 'Источник CTA: <code>' + esc(l.sourceRiskCode) + '</code><br>' : '') +
            'Lead heat: <span class="heat heat-' + esc(l.heatLabel) + '">' + l.heatScore + ' · ' + HEAT_LABEL[l.heatLabel] + '</span><br>' +
            'Fundraising: ' + esc(TIMELINE_LABEL[data.fundraisingTimeline] || 'нет данных') +
          '</div>' +
          '<div class="field" style="margin-top:20px;max-width:260px"><label>Статус</label>' +
            '<select id="lead-status">' + statusOptions + '</select></div>' +
          '<div class="field" style="margin-top:16px;max-width:420px"><label>Внутренняя заметка</label>' +
            '<textarea id="note-text" rows="3"></textarea>' +
            '<button class="btn btn-secondary" id="add-note" style="margin-top:10px;padding:10px 18px;font-size:14px">Добавить заметку</button></div>' +
          '<div class="answers-list" style="margin-top:8px">' + notes + '</div>' +
        '</div>' +
        '<div>' +
          (r ? '<div class="score-label">Legal Score</div>' +
            '<div class="serif" style="font-size:56px;font-weight:600">' + r.overall + '<span style="font-size:20px;color:var(--ink-faint)"> / 100 · ' + esc(r.levelTitle) + '</span></div>' +
            '<div style="margin-top:16px">' + sectionScores + '</div>' +
            '<h3 class="serif" style="font-size:20px;margin-top:28px">Риски (' + r.risks.length + ')</h3>' +
            '<div class="answers-list">' + risks + '</div>'
          : '<p style="color:var(--ink-faint)">Диагностика не завершена</p>') +
        '</div>' +
      '</div>' +
      '<h3 class="serif" style="font-size:20px;margin-top:40px">Все ответы (' + data.answers.length + ')</h3>' +
      '<div class="answers-list" style="max-width:720px">' + answers + '</div>';

    document.getElementById('back-to-leads').addEventListener('click', function () { renderShell('leads'); });
    document.getElementById('lead-status').addEventListener('change', async function (e) {
      await api('POST', '/api/admin/leads/' + id + '/status', { status: e.target.value });
    });
    document.getElementById('add-note').addEventListener('click', async function () {
      const note = document.getElementById('note-text').value.trim();
      if (!note) return;
      await api('POST', '/api/admin/leads/' + id + '/notes', { note: note });
      renderShell('leads', id);
    });
  }

  // -----------------------------------------------------------------------
  // Question bank / risk library (read-only view, v1)
  // -----------------------------------------------------------------------

  async function loadQuestions(el) {
    const data = await api('GET', '/api/admin/question-bank');
    const bySection = data.sections.map(function (s) {
      const qs = data.questions.filter(function (q) { return q.sectionId === s.id; });
      const rows = qs.map(function (q) {
        return '<tr><td><code style="font-size:12px">' + esc(q.id) + '</code></td>' +
          '<td>' + esc(q.question) + (q.showIf ? '<br><span style="color:var(--ink-faint);font-size:12px">условный показ</span>' : '') + '</td>' +
          '<td>' + q.weight + '</td>' +
          '<td>' + (q.options ? q.options.length : 0) + '</td></tr>';
      }).join('');
      return '<h3 class="serif" style="font-size:20px;margin:28px 0 12px">' + s.order + '. ' + esc(s.title) + ' <span style="color:var(--ink-faint);font-size:14px">вес ' + s.weight + '</span></h3>' +
        '<table class="data"><thead><tr><th>ID</th><th>Вопрос</th><th>Вес</th><th>Опций</th></tr></thead><tbody>' + rows + '</tbody></table>';
    }).join('');
    el.innerHTML = '<p style="color:var(--ink-faint);font-size:13px">Question bank v' + esc(data.version) + ' · ' + data.questions.length + ' вопросов · редактирование — в следующей итерации, сейчас question bank версионируется в коде (src/data/questions.ts)</p>' + bySection;
  }

  async function loadRisks(el) {
    const data = await api('GET', '/api/admin/risk-library');
    const rows = data.risks.map(function (r) {
      return '<tr><td><code style="font-size:12px">' + esc(r.code) + '</code></td>' +
        '<td><span class="sev sev-' + r.severity + '">' + r.severity + '</span></td>' +
        '<td><strong>' + esc(r.title) + '</strong><br><span style="color:var(--ink-soft);font-size:13px">' + esc(r.finding) + '</span></td>' +
        '<td style="font-size:12.5px;color:var(--ink-faint)">' + esc(r.resolution) + '</td></tr>';
    }).join('');
    el.innerHTML =
      '<p style="color:var(--ink-faint);font-size:13px">Risk library v' + esc(data.version) + ' · ' + data.risks.length + ' правил · CRITICAL_* — rule-based флаги</p>' +
      '<table class="data" style="margin-top:16px"><thead><tr><th>Код</th><th>Severity</th><th>Риск</th><th>Resolution</th></tr></thead><tbody>' + rows + '</tbody></table>';
  }

  // -----------------------------------------------------------------------
  // Boot: пробуем overview; 401 → login
  // -----------------------------------------------------------------------

  api('GET', '/api/admin/overview')
    .then(function () { renderShell('overview'); })
    .catch(function () { /* renderLogin уже вызван */ });
})();
