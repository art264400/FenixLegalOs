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
    ['testbench', '🧪 QA Simulator & Test Bench'],
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
    if (active === 'testbench') loadTestBench(content);
    if (active === 'questions') loadQuestions(content);
    if (active === 'risks') loadRisks(content);
  }

  // -----------------------------------------------------------------------
  // Test Bench / QA Simulator
  // -----------------------------------------------------------------------

  async function loadTestBench(el) {
    el.innerHTML = '<div class="tb-container"><p style="color:var(--ink-faint)">Загрузка тестовых пресетов…</p></div>';
    const data = await api('GET', '/api/admin/testbench/presets');
    const presets = data.presets || [];
    const liveSessions = data.liveSessions || [];

    let currentAnswers = presets[0] ? presets[0].answers : {};
    let currentResult = null;
    let currentAiMemo = null;

    function renderBenchLayout() {
      const presetButtons = presets.map(function (p, idx) {
        return '<button class="tb-preset-btn ' + (idx === 0 ? 'active' : '') + '" data-pid="' + p.id + '">' + esc(p.badge) + ' ' + esc(p.title) + '</button>';
      }).join('');

      const sessionOptions = liveSessions.map(function (s) {
        return '<option value="' + s.id + '">' + esc(s.title) + ' (' + esc(s.badge) + ')</option>';
      }).join('');

      el.innerHTML =
        '<div class="tb-container">' +
          '<div class="tb-toolbar">' +
            '<div style="display:flex;flex-direction:column;gap:8px;width:100%">' +
              '<div style="display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:12px">' +
                '<strong style="font-size:15px;color:#FFF">Тестовые сценарии (Presets):</strong>' +
                '<div style="display:flex;align-items:center;gap:10px">' +
                  '<label style="font-size:12px;color:var(--ink-soft)">Или выбрать живую сессию:</label>' +
                  '<select id="tb-session-select" style="background:var(--bg-elev);border:1px solid var(--line);border-radius:6px;padding:6px 12px;color:#FFF;font-size:12.5px">' +
                    '<option value="">— Выбрать из базы данных —</option>' +
                    sessionOptions +
                  '</select>' +
                '</div>' +
              '</div>' +
              '<div class="tb-presets">' + presetButtons + '</div>' +
            '</div>' +
          '</div>' +
          '<div id="tb-grid-root" class="tb-grid">' +
            '<div class="tb-col" id="tb-col-answers"><p style="color:var(--ink-faint)">Расчет ответов…</p></div>' +
            '<div class="tb-col" id="tb-col-risks"><p style="color:var(--ink-faint)">Расчет рисков…</p></div>' +
            '<div class="tb-col" id="tb-col-ai"><p style="color:var(--ink-faint)">Готовность AI-генерации…</p></div>' +
          '</div>' +
        '</div>';

      // Event handlers for presets
      el.querySelectorAll('.tb-preset-btn').forEach(function (btn) {
        btn.addEventListener('click', function () {
          el.querySelectorAll('.tb-preset-btn').forEach(function (b) { b.classList.remove('active'); });
          btn.classList.add('active');
          const pid = btn.getAttribute('data-pid');
          const found = presets.find(function (p) { return p.id === pid; });
          if (found) {
            currentAnswers = found.answers;
            currentAiMemo = null;
            runSimulation();
          }
        });
      });

      const sessSelect = document.getElementById('tb-session-select');
      if (sessSelect) {
        sessSelect.addEventListener('change', function (e) {
          const sid = e.target.value;
          if (!sid) return;
          el.querySelectorAll('.tb-preset-btn').forEach(function (b) { b.classList.remove('active'); });
          const found = liveSessions.find(function (s) { return s.id === sid; });
          if (found) {
            currentAnswers = found.answers;
            currentAiMemo = null;
            runSimulation();
          }
        });
      }

      runSimulation();
    }

    async function runSimulation() {
      const colAnswers = document.getElementById('tb-col-answers');
      const colRisks = document.getElementById('tb-col-risks');
      const colAi = document.getElementById('tb-col-ai');

      colAnswers.innerHTML = '<p style="color:var(--accent)">Загрузка ответов…</p>';
      colRisks.innerHTML = '<p style="color:var(--accent)">Движок рассчитывает скоринг и риски…</p>';

      try {
        const sim = await api('POST', '/api/admin/testbench/simulate', { answers: currentAnswers });
        currentResult = sim.result;

        // 1. Render Column 1: Answers
        const secHtml = sim.structuredAnswers.map(function (sec) {
          const qRows = sec.questions.map(function (q) {
            return '<div class="tb-qa-item">' +
              '<div class="tb-qa-q">' + esc(q.question) + '</div>' +
              '<div class="tb-qa-a">↳ ' + esc(q.answerText) + '</div>' +
            '</div>';
          }).join('');

          return '<div class="tb-sec-group">' +
            '<div class="tb-sec-title">' + esc(sec.sectionTitle) + ' (' + sec.questions.length + ')</div>' +
            qRows +
          '</div>';
        }).join('');

        colAnswers.innerHTML =
          '<div class="tb-col-header">' +
            '<h3>📋 1. Входные ответы</h3>' +
            '<span class="badge" style="background:rgba(56,189,248,0.15);color:var(--accent)">' + sim.totalAnswered + ' ответов</span>' +
          '</div>' +
          '<div style="display:flex;flex-direction:column;gap:10px;overflow-y:auto;max-height:750px;padding-right:4px">' +
            secHtml +
          '</div>';

        // 2. Render Column 2: Risks & Score
        const r = sim.result;
        const scoreColor = r.overall >= 75 ? 'var(--positive)' : r.overall >= 50 ? 'var(--high)' : 'var(--critical)';
        
        const riskCards = (r.risks || []).map(function (rk) {
          const sevClass = rk.severity === 'critical' ? 'rc-critical' : rk.severity === 'high' ? 'rc-high' : 'rc-medium';
          const sevBadge = rk.severity === 'critical' ? '🔴 КРИТИЧЕСКИЙ' : rk.severity === 'high' ? '🟠 ВЫСОКИЙ' : '🟡 УМЕРЕННЫЙ';
          return '<div class="tb-risk-item ' + sevClass + '">' +
            '<div style="display:flex;align-items:center;justify-content:space-between;gap:8px;margin-bottom:4px">' +
              '<span class="tb-risk-title">' + esc(rk.title) + '</span>' +
              '<span style="font-size:10.5px;font-weight:700">' + sevBadge + '</span>' +
            '</div>' +
            '<div class="tb-risk-desc">' + esc(rk.finding) + '</div>' +
            (rk.whyItMatters ? '<div style="font-size:11px;color:var(--gold);margin-top:4px"><b>Почему важно:</b> ' + esc(rk.whyItMatters) + '</div>' : '') +
          '</div>';
        }).join('');

        const secBars = (r.sections || []).map(function (s) {
          const scVal = s.score != null ? s.score : 0;
          const scColor = scVal >= 75 ? 'var(--positive)' : scVal >= 50 ? 'var(--high)' : 'var(--critical)';
          return '<div style="margin-bottom:6px">' +
            '<div style="display:flex;justify-content:space-between;font-size:11.5px;margin-bottom:2px">' +
              '<span style="color:#FFF">' + esc(s.title) + '</span>' +
              '<span style="color:' + scColor + ';font-weight:700">' + (s.score != null ? s.score + '%' : 'N/A') + '</span>' +
            '</div>' +
            '<div style="height:4px;background:rgba(255,255,255,0.08);border-radius:2px;overflow:hidden">' +
              '<div style="height:100%;width:' + scVal + '%;background:' + scColor + '"></div>' +
            '</div>' +
          '</div>';
        }).join('');

        colRisks.innerHTML =
          '<div class="tb-col-header">' +
            '<h3>⚖️ 2. Скоринг и Риски</h3>' +
            '<span class="badge" style="background:rgba(229,192,123,0.15);color:var(--gold)">' + (r.risks ? r.risks.length : 0) + ' рисков</span>' +
          '</div>' +
          '<div class="tb-score-box">' +
            '<div class="tb-score-num" style="color:' + scoreColor + '">' + r.overall + '<span style="font-size:18px;color:var(--ink-soft)">/100</span></div>' +
            '<div class="tb-score-label">' + esc(r.levelTitle) + '</div>' +
            '<div style="display:flex;justify-content:center;gap:8px;margin-top:10px;font-size:11px">' +
              '<span style="color:var(--critical);font-weight:700">🔴 ' + (r.criticalCount || 0) + ' крит</span>' +
              '<span style="color:var(--high);font-weight:700">🟠 ' + (r.highCount || 0) + ' выс</span>' +
              '<span style="color:var(--medium);font-weight:700">🟡 ' + (r.mediumCount || 0) + ' умер</span>' +
            '</div>' +
          '</div>' +
          '<div style="background:var(--bg-elev);border:1px solid var(--line);border-radius:8px;padding:12px">' +
            '<div style="font-size:11px;font-weight:700;text-transform:uppercase;color:var(--accent);margin-bottom:8px">Оценка 8 областей:</div>' +
            secBars +
          '</div>' +
          '<div style="display:flex;flex-direction:column;gap:8px;overflow-y:auto;max-height:420px;padding-right:4px">' +
            (riskCards || '<p style="color:var(--positive);font-size:13px;text-align:center">Критических рисков не обнаружено</p>') +
          '</div>';

        // 3. Render Column 3: LLM Report generator
        renderAiColumn(colAi);

      } catch (err) {
        colAnswers.innerHTML = '<p style="color:var(--critical)">Ошибка расчета: ' + esc(err.message) + '</p>';
        colRisks.innerHTML = '<p style="color:var(--critical)">Ошибка</p>';
      }
    }

    function renderAiColumn(colAi) {
      if (currentAiMemo) {
        colAi.innerHTML =
          '<div class="tb-col-header">' +
            '<h3>🤖 3. LLM-отчет</h3>' +
            '<button class="btn btn-secondary btn-sm" id="tb-regen-ai-btn">🔄 Перегенерировать</button>' +
          '</div>' +
          '<div style="display:flex;align-items:center;justify-content:space-between;font-size:11px;color:var(--ink-soft);background:var(--bg-elev);padding:6px 12px;border-radius:6px">' +
            '<span>Модель: <b style="color:var(--accent)">' + esc(currentAiMemo.model) + '</b></span>' +
            '<span>Время: <b style="color:var(--positive)">' + (currentAiMemo.durationMs / 1000).toFixed(2) + ' сек</b></span>' +
          '</div>' +
          '<div class="ai-memo-body" style="background:var(--bg-elev);border:1px solid var(--line);border-radius:8px;padding:16px;overflow-y:auto;max-height:680px;font-size:13px;line-height:1.55">' +
            formatMarkdown(currentAiMemo.memo) +
          '</div>';

        document.getElementById('tb-regen-ai-btn').addEventListener('click', triggerAiGeneration);
      } else {
        colAi.innerHTML =
          '<div class="tb-col-header">' +
            '<h3>🤖 3. LLM-отчет</h3>' +
            '<span class="badge" style="background:rgba(255,255,255,0.08);color:var(--ink-soft)">Ожидание</span>' +
          '</div>' +
          '<div style="text-align:center;padding:40px 20px;display:flex;flex-direction:column;align-items:center;gap:14px">' +
            '<p style="color:var(--ink-soft);font-size:13.5px;max-width:280px">Отчет LLM еще не сгенерирован для этого набора ответов.</p>' +
            '<button class="btn" id="tb-gen-ai-btn" style="padding:12px 24px;font-size:14px">⚡ Сгенерировать отчет LLM</button>' +
          '</div>';

        document.getElementById('tb-gen-ai-btn').addEventListener('click', triggerAiGeneration);
      }
    }

    async function triggerAiGeneration() {
      const colAi = document.getElementById('tb-col-ai');
      colAi.innerHTML =
        '<div class="tb-col-header">' +
          '<h3>🤖 3. LLM-отчет</h3>' +
          '<span class="badge" style="background:rgba(56,189,248,0.2);color:var(--accent)">Генерация…</span>' +
        '</div>' +
        '<div style="text-align:center;padding:60px 20px;display:flex;flex-direction:column;align-items:center;gap:14px">' +
          '<div class="spinner" style="width:32px;height:32px;border-width:3px"></div>' +
          '<p style="color:var(--accent);font-size:14px;font-weight:600">OpenAI синтезирует юридический меморандум…</p>' +
          '<span style="font-size:12px;color:var(--ink-soft)">Анализ 8 зон, рисков и 30-дневного Action Plan</span>' +
        '</div>';

      try {
        const res = await api('POST', '/api/admin/testbench/generate-ai', {
          result: currentResult,
          answers: currentAnswers
        });
        currentAiMemo = res;
        renderAiColumn(colAi);
      } catch (err) {
        colAi.innerHTML =
          '<div class="tb-col-header"><h3>🤖 3. LLM-отчет</h3></div>' +
          '<p style="color:var(--critical);padding:20px">Ошибка генерации LLM: ' + esc(err.message) + '</p>' +
          '<button class="btn btn-secondary" id="tb-retry-ai-btn" style="margin:0 20px">Повторить попытку</button>';
        document.getElementById('tb-retry-ai-btn').addEventListener('click', triggerAiGeneration);
      }
    }

    function formatMarkdown(md) {
      if (!md) return '';
      let text = String(md);
      text = text.replace(/^(\d+(\.\d+)*\.\s*)+/gm, '');
      text = esc(text);

      const lines = text.split('\n');
      let out = '';
      let inList = false;
      let inOrderedList = false;

      for (let i = 0; i < lines.length; i++) {
        let line = lines[i].trim();
        if (!line) {
          if (inList) { out += '</ul>'; inList = false; }
          if (inOrderedList) { out += '</ol>'; inOrderedList = false; }
          continue;
        }

        if (line.startsWith('### ')) {
          if (inList) { out += '</ul>'; inList = false; }
          if (inOrderedList) { out += '</ol>'; inOrderedList = false; }
          out += '<h3 class="ai-h3" style="font-size:15px;color:var(--gold);margin:14px 0 6px">' + line.slice(4) + '</h3>';
        } else if (line.startsWith('## ')) {
          if (inList) { out += '</ul>'; inList = false; }
          if (inOrderedList) { out += '</ol>'; inOrderedList = false; }
          out += '<h2 class="ai-h2" style="font-size:17px;color:#FFF;margin:18px 0 8px">' + line.slice(3) + '</h2>';
        } else if (line.startsWith('* ') || line.startsWith('- ')) {
          if (inOrderedList) { out += '</ol>'; inOrderedList = false; }
          if (!inList) { out += '<ul class="ai-ul" style="margin:6px 0;padding-left:18px">'; inList = true; }
          out += '<li class="ai-li" style="margin-bottom:6px">' + line.slice(2) + '</li>';
        } else if (/^\d+\.\s+/.test(line)) {
          if (inList) { out += '</ul>'; inList = false; }
          if (!inOrderedList) { out += '<ol class="ai-ol" style="margin:6px 0;padding-left:18px">'; inOrderedList = true; }
          out += '<li class="ai-oli" style="margin-bottom:6px">' + line.replace(/^\d+\.\s+/, '') + '</li>';
        } else {
          if (inList) { out += '</ul>'; inList = false; }
          if (inOrderedList) { out += '</ol>'; inOrderedList = false; }
          out += '<p class="ai-p" style="margin-bottom:8px">' + line + '</p>';
        }
      }
      if (inList) out += '</ul>';
      if (inOrderedList) out += '</ol>';

      out = out.replace(/\*\*(.*?)\*\*/g, '<strong style="color:#FFF">$1</strong>');
      out = out.replace(/\*(.*?)\*/g, '<em style="color:var(--ink-2)">$1</em>');
      return out;
    }

    renderBenchLayout();
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
