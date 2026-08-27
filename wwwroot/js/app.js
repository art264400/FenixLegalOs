/* Fenix Legal OS — client app (vanilla JS, no build step). */
(function () {
  'use strict';

  const app = document.getElementById('app');
  const modalRoot = document.getElementById('modal-root');
  const progressEl = document.getElementById('progress');

  // ---------------------------------------------------------------------
  // State
  // ---------------------------------------------------------------------

  const STORAGE_KEY = 'fenix_diagnostic_v1';

  let bank = null; // { sections, questions, versions }
  let state = loadState(); // { sessionId, answers, idx }
  let lastResult = null;
  let unlocked = false;

  function loadState() {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) return JSON.parse(raw);
    } catch (e) { /* ignore */ }
    return { sessionId: null, answers: {}, idx: 0 };
  }

  function saveState() {
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(state)); } catch (e) { /* ignore */ }
  }

  // ---------------------------------------------------------------------
  // API helpers
  // ---------------------------------------------------------------------

  async function api(method, url, body) {
    const res = await fetch(url, {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: body ? JSON.stringify(body) : undefined,
    });
    if (!res.ok) throw new Error('api_error_' + res.status);
    return res.json();
  }

  function track(name, payload) {
    fetch('/api/events', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: name, sessionId: state.sessionId, payload: payload }),
    }).catch(function () {});
  }

  async function ensureBank() {
    if (!bank) bank = await api('GET', '/api/questionnaire');
    return bank;
  }

  // ---------------------------------------------------------------------
  // Conditional logic (mirror of server engine)
  // ---------------------------------------------------------------------

  function evalRule(rule, answers) {
    if (rule.all || rule.any) {
      if (rule.all && !rule.all.every(function (r) { return evalRule(r, answers); })) return false;
      if (rule.any && !rule.any.some(function (r) { return evalRule(r, answers); })) return false;
      return true;
    }
    let a = answers[rule.questionId];

    // Client-side fact resolutions
    if (rule.questionId === 'ip.coreProductExists') {
      const pStage = answers['IP-01'];
      a = pStage !== undefined && pStage !== null && pStage !== 'idea' ? 'true' : 'false';
    } else if (rule.questionId === 'ip.creators') {
      a = answers['IP-03'];
    }

    switch (rule.op) {
      case 'answered': return a !== undefined && a !== null && a !== '' && !(Array.isArray(a) && !a.length);
      case 'eq': return String(a).toLowerCase() === String(rule.value).toLowerCase();
      case 'neq': return a !== undefined && String(a).toLowerCase() !== String(rule.value).toLowerCase();
      case 'in':
        if (typeof a !== 'string') return false;
        if (Array.isArray(rule.value)) return rule.value.indexOf(a) !== -1;
        if (typeof rule.value === 'string') return rule.value.split(',').map(function (s) { return s.trim(); }).indexOf(a) !== -1;
        return false;
      case 'notIn':
        if (typeof a !== 'string') return true;
        if (Array.isArray(rule.value)) return rule.value.indexOf(a) === -1;
        if (typeof rule.value === 'string') return rule.value.split(',').map(function (s) { return s.trim(); }).indexOf(a) === -1;
        return true;
      case 'includes':
      case 'contains':
        if (Array.isArray(a)) return a.some(function (x) { return String(x).toLowerCase() === String(rule.value).toLowerCase(); });
        if (typeof a === 'string') return a.split(',').map(function (s) { return s.trim().toLowerCase(); }).indexOf(String(rule.value).toLowerCase()) !== -1;
        return false;
      case 'notContains':
        if (Array.isArray(a)) return !a.some(function (x) { return String(x).toLowerCase() === String(rule.value).toLowerCase(); });
        if (typeof a === 'string') return a.split(',').map(function (s) { return s.trim().toLowerCase(); }).indexOf(String(rule.value).toLowerCase()) === -1;
        return true;
      default: return false;
    }
  }

  function visibleQuestions(answers) {
    return bank.questions.filter(function (q) {
      if (q.enabled === false) return false;
      if (!q.showIf || !q.showIf.length) return true;
      return q.showIf.every(function (r) { return evalRule(r, answers); });
    });
  }

  // ---------------------------------------------------------------------
  // Rendering helpers
  // ---------------------------------------------------------------------

  function esc(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  function setProgress(fraction) {
    progressEl.style.width = Math.round(fraction * 100) + '%';
  }

  function render(html) {
    app.innerHTML = '<div class="fade-in">' + html + '</div>';
    window.scrollTo({ top: 0, behavior: 'instant' in window ? 'instant' : 'auto' });
  }

  const SEV_LABEL = {
    critical: 'CRITICAL', CRITICAL: 'CRITICAL', blocker: 'CRITICAL', BLOCKER: 'CRITICAL',
    high: 'HIGH', HIGH: 'HIGH',
    medium: 'MEDIUM', MEDIUM: 'MEDIUM',
    low: 'LOW', LOW: 'LOW'
  };
  const RESOLUTION_LABEL = {
    self: 'Можно решить самостоятельно',
    check_with_lawyer: 'Желательно проверить с юристом',
    lawyer_required: 'Требуется индивидуальная юридическая работа',
    lawyer: 'Требуется индивидуальная юридическая работа',
    template: 'Типовой документ / шаблон',
    expert: 'Экспертная проверка',
  };

  // ---------------------------------------------------------------------
  // Screens
  // ---------------------------------------------------------------------

  function screenLanding() {
    setProgress(0);
    track('landing_viewed');
    render(
      '<section class="hero">' +
        '<div class="hero-flex">' +
          '<div class="hero-copy">' +
            '<div class="hero-badge">Smart Legal Screening · by Fenix Law</div>' +
            '<h1>Что такое FENIX SLS?</h1>' +
            '<p class="sub">Первичная юридическая диагностика для технологических компаний. Ответьте на понятные вопросы и получите персональный <strong>Legal Score</strong>, приоритизированный <strong>Action Plan</strong> и карту юридических рисков компании до прихода инвестора.</p>' +
          '</div>' +
          '<img class="hero-logo" src="/img/logo.png" alt="FENIX SLS">' +
        '</div>' +
        '<div class="hero-pillars">' +
          '<div class="pillar-card">' +
            '<strong style="color:var(--ink)">Увидеть реальные риски</strong>' +
            '<span>до прихода инвестора и Due Diligence</span>' +
          '</div>' +
          '<div class="pillar-card">' +
            '<strong style="color:var(--gold)">Понять приоритеты</strong>' +
            '<span>что критично устранить прямо сейчас</span>' +
          '</div>' +
          '<div class="pillar-card">' +
            '<strong style="color:var(--positive)">Подготовиться к росту</strong>' +
            '<span>и безопасным венчурным сделкам</span>' +
          '</div>' +
        '</div>' +
        '<div class="cta-row">' +
          '<button class="btn" id="start-btn">Пройти диагностику (10 мин)</button>' +
        '</div>' +
        '<div class="trust-row">' +
          '<span>✓ Бесплатно</span><span>✓ 10 минут</span><span>✓ Без загрузки документов</span><span>✓ На базе практики Fenix Law</span>' +
        '</div>' +
        '<div class="flow-section">' +
          '<div class="flow-title">Как работает FENIX SLS</div>' +
          '<div class="flow-grid">' +
            '<div class="flow-step">' +
              '<div class="step-tag">ШАГ 01</div>' +
              '<h3>Ваши ответы</h3>' +
              '<p>10 минут без юристов и сложных терминов</p>' +
            '</div>' +
            '<div class="flow-arrow">→</div>' +
            '<div class="flow-step active">' +
              '<div class="step-tag" style="color:var(--gold)">ШАГ 02 · АЛГОРИТМ</div>' +
              '<h3>Синтез связей</h3>' +
              '<p>SLS анализирует всю юридическую конструкцию компании</p>' +
            '</div>' +
            '<div class="flow-arrow">→</div>' +
            '<div class="flow-step">' +
              '<div class="step-tag" style="color:var(--positive)">ШАГ 03</div>' +
              '<h3>Legal Roadmap</h3>' +
              '<p>Legal Score, карта рисков и пошаговый план</p>' +
            '</div>' +
          '</div>' +
        '</div>' +
        '<div class="hero-domains">' +
          '<div class="label">Проверка 8 ключевых зон бизнеса</div>' +
          '<div class="domain-grid">' +
            '<div><div class="d-num">01</div><div class="d-title">Основатели</div><small>доли · роли · решения · вестинг</small></div>' +
            '<div><div class="d-num">02</div><div class="d-title">Корпоративная структура</div><small>владение · полномочия · структура</small></div>' +
            '<div><div class="d-num">03</div><div class="d-title">Интеллектуальная собственность</div><small>код · разработки · бренд · права</small></div>' +
            '<div><div class="d-num">04</div><div class="d-title">Команда</div><small>сотрудники · подрядчики · доступы</small></div>' +
            '<div><div class="d-num">05</div><div class="d-title">Продукт и пользователи</div><small>оферта · оплаты · ответственность</small></div>' +
            '<div><div class="d-num">06</div><div class="d-title">Данные и ИИ</div><small>персональные данные · сбор · модели ИИ</small></div>' +
            '<div><div class="d-num">07</div><div class="d-title">Договоры</div><small>клиенты · партнеры · обязательства</small></div>' +
            '<div><div class="d-num">08</div><div class="d-title">Инвестиционная готовность</div><small>раунд · SAFE / КИС · проверка</small></div>' +
          '</div>' +
        '</div>' +
        '<div class="method">' +
          '<h2>Юридическая экспертиза, превращенная в систему</h2>' +
          '<p>Методология и алгоритм <strong>FENIX SLS</strong> созданы на базе реальной практики бутиковой юридической фирмы <strong>FENIX LAW</strong>, которая специализируется на технологических компаниях, венчурных сделках и структурировании бизнеса.</p>' +
          '<p>Система анализирует не просто отдельные ответы, а выявляет критические юридические конфигурации (например, риск тупика при 50/50 без deadlock-механизмов) раньше, чем их увидит инвестор или возникнет спор.</p>' +
        '</div>' +
      '</section>'
    );
    document.getElementById('start-btn').addEventListener('click', async function () {
      try {
        const created = await api('POST', '/api/sessions');
        state.sessionId = created.id;
      } catch (e) {
        state.sessionId = 'local_' + Date.now();
      }
      state.answers = {};
      state.idx = 0;
      lastResult = null;
      unlocked = false;
      isPaid = false;
      saveState();
      location.hash = '#/diagnostic';
    });
  }

  function screenIntro() {
    setProgress(0);
    const rows = bank.sections.map(function (s) {
      return '<div class="row"><span class="num">0' + s.order + '</span><span>' + esc(s.title) + '</span></div>';
    }).join('');
    render(
      '<section class="q-screen wrap-narrow" style="padding-left:0;padding-right:0">' +
        '<h1 style="font-size:clamp(28px,4.5vw,40px)">Мы зададим вопросы о восьми областях вашей компании</h1>' +
        '<div class="intro-list">' + rows + '</div>' +
        '<div class="note-quote">Здесь нет правильных и неправильных компаний. Задача диагностики — понять вашу текущую юридическую конструкцию и определить вопросы, которые могут требовать внимания.</div>' +
        '<div class="q-nav">' +
          '<button class="btn" id="continue-btn">Продолжить</button>' +
          '<button class="btn-ghost" onclick="location.hash=\'#/\'">Назад</button>' +
        '</div>' +
      '</section>'
    );
    document.getElementById('continue-btn').addEventListener('click', async function () {
      try {
        const created = await api('POST', '/api/sessions');
        state.sessionId = created.id;
      } catch (e) {
        state.sessionId = 'local_' + Date.now();
      }
      state.answers = {};
      state.idx = 0;
      lastResult = null;
      unlocked = false;
      isPaid = false;
      saveState();
      location.hash = '#/diagnostic';
    });
  }

  function syncAnswers(sectionId) {
    if (!state.sessionId) return;
    fetch('/api/sessions/' + state.sessionId + '/answers', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ answers: state.answers, lastSectionId: sectionId }),
    }).catch(function () {});
  }

  function screenQuestion() {
    const visible = visibleQuestions(state.answers);
    if (state.idx >= visible.length) { finishDiagnostic(); return; }
    if (state.idx < 0) state.idx = 0;

    const q = visible[state.idx];
    const section = bank.sections.find(function (s) { return s.id === q.sectionId; });
    const answered = state.idx;
    setProgress(answered / visible.length);

    const current = state.answers[q.id];
    const isMultiple = q.type === 'multiple';
    const isEquityInputs = q.type === 'equity_inputs';
    const isEntityBuilder = q.type === 'entity_builder' || q.id === 'COR-C02C';

    let contentHtml = '';

    if (isEquityInputs) {
      const fCountAns = state.answers['FND-C01'] || '2';
      let count = 2;
      if (fCountAns === '3') count = 3;
      else if (fCountAns === '4plus') count = 4;

      let eqMap = (typeof current === 'object' && current !== null) ? current : {};
      const mapKeys = Object.keys(eqMap).filter(function(k) { return k.startsWith('founder_'); });
      if (mapKeys.length > count) count = mapKeys.length;

      let rowsHtml = '';
      for (let i = 1; i <= count; i++) {
        const key = 'founder_' + i;
        const val = eqMap[key] !== undefined ? eqMap[key] : (current === 'equal_50_50' || !current ? Math.floor(100 / count) : '');
        rowsHtml += '<div class="equity-input-row" data-index="' + i + '">' +
          '<label>Основатель ' + i + '</label>' +
          '<div class="equity-percent-wrap">' +
            '<input type="number" class="equity-percent-field" data-founder="' + key + '" min="0" max="100" value="' + esc(val) + '" placeholder="0" />' +
            '<span>%</span>' +
            (count > 2 ? '<button type="button" class="equity-del-btn" data-del="' + i + '" title="Удалить" style="background:none; border:none; color:#f87171; font-size:16px; cursor:pointer; padding:0 4px; line-height:1;">✕</button>' : '') +
          '</div>' +
        '</div>';
      }

      const optionsHtml = q.options.map(function (o) {
        const selected = current === o.id;
        return '<button class="q-option' + (selected ? ' selected' : '') + '" data-opt="' + esc(o.id) + '">' + esc(o.label) + '</button>';
      }).join('');

      contentHtml = '<div class="equity-input-group">' +
        '<div id="equity-rows-container">' + rowsHtml + '</div>' +
        '<button type="button" class="btn-ghost" id="equity-add-founder-btn" style="font-size:13px; margin:8px 0 14px; width:100%; border:1px dashed var(--line); padding:8px 12px; border-radius:var(--radius-sm); text-align:center;">+ Добавить сооснователя</button>' +
        '<div class="equity-status-bar">' +
          '<span id="equity-sum-text">Сумма: 100%</span>' +
          '<button type="button" class="btn-ghost" id="equity-equal-btn" style="font-size:12px; padding:4px 8px;">Разделить поровну</button>' +
        '</div>' +
      '</div>' +
      '<div class="q-options" style="margin-top:16px;">' + optionsHtml + '</div>';
    } else if (isEntityBuilder) {
      const countCode = state.answers['COR-C02B'] || '2';
      let entityCount = countCode === '3' ? 2 : countCode === '4plus' ? 3 : 1;
      let existingArr = Array.isArray(current) ? current : [];
      if (existingArr.length > entityCount) entityCount = existingArr.length;

      const roleList = [
        { id: 'holding', label: 'Холдинг / владение долями' },
        { id: 'clients', label: 'Работа с клиентами и договоры' },
        { id: 'payments', label: 'Платежи и выручка' },
        { id: 'ip_assets', label: 'Владение IP-активами' },
        { id: 'hiring', label: 'Найм команды' },
        { id: 'other', label: 'Другое' }
      ];

      const jurList = [
        { id: 'kz', label: 'Казахстан' },
        { id: 'aifc', label: 'МФЦА' },
        { id: 'us', label: 'США' },
        { id: 'uae', label: 'ОАЭ' },
        { id: 'uk', label: 'Великобритания' },
        { id: 'other', label: 'Другая' }
      ];

      let cardsHtml = '';
      for (let i = 0; i < entityCount; i++) {
        const ent = existingArr[i] || {};
        const cNum = i + 2;
        const curJur = ent.jurisdiction || 'kz';
        const curRoles = Array.isArray(ent.roles) ? ent.roles : [];

        const jBtns = jurList.map(function (j) {
          const sel = curJur === j.id;
          return '<button type="button" class="jur-btn' + (sel ? ' selected' : '') + '" data-cidx="' + i + '" data-jur="' + j.id + '">' + esc(j.label) + '</button>';
        }).join('');

        const rChips = roleList.map(function (r) {
          const sel = curRoles.indexOf(r.id) !== -1;
          return '<span class="role-chip' + (sel ? ' selected' : '') + '" data-cidx="' + i + '" data-role="' + r.id + '">' + esc(r.label) + '</span>';
        }).join('');

        cardsHtml += '<div class="jur-card" data-entity-idx="' + i + '">' +
          '<div class="jur-card-title"><span>Компания ' + cNum + '</span>' + (entityCount > 1 ? '<button type="button" class="entity-del-btn" data-del-entity="' + i + '" style="background:none;border:none;color:#f87171;font-size:14px;cursor:pointer">✕ Удалить</button>' : '') + '</div>' +
          '<div class="roles-header">Страна регистрации:</div>' +
          '<div class="jur-grid">' + jBtns + '</div>' +
          '<div class="roles-header">Для чего используется эта компания:</div>' +
          '<div class="roles-grid">' + rChips + '</div>' +
        '</div>';
      }

      contentHtml = '<div class="jur-builder-wrap">' +
        '<div id="entity-cards-container">' + cardsHtml + '</div>' +
        '<button type="button" class="btn-ghost" id="add-entity-btn" style="width:100%; border:1px dashed var(--line); padding:10px; border-radius:var(--radius-sm); font-size:13px">+ Добавить еще компанию в структуру</button>' +
      '</div>';
    } else {
      const optionsHtml = q.options.map(function (o) {
        const selected = isMultiple
          ? (Array.isArray(current) && current.indexOf(o.id) !== -1)
          : current === o.id;
        return '<button class="q-option' + (selected ? ' selected' : '') + '" data-opt="' + esc(o.id) + '">' + esc(o.label) + '</button>';
      }).join('');
      contentHtml = '<div class="q-options">' + optionsHtml + '</div>';
    }

    render(
      '<section class="q-screen">' +
        '<div class="q-meta">Раздел ' + section.order + ' из ' + bank.sections.length + ' — ' + esc(section.title) + '</div>' +
        '<h1 class="q-title">' + esc(q.question) + '</h1>' +
        contentHtml +
        (q.explanation
          ? '<div class="q-why"><button id="why-btn" aria-expanded="false">Почему мы это спрашиваем?</button>' +
            '<div class="why-text" id="why-text" hidden>' + esc(q.explanation) + '</div></div>'
          : '') +
        '<div class="q-nav">' +
          (state.idx > 0 ? '<button class="btn-ghost" id="back-btn">← Назад</button>' : '') +
          (isMultiple || isEquityInputs || isEntityBuilder ? '<button class="btn" id="next-btn">Продолжить</button>' : '') +
          '<span class="q-count">' + (state.idx + 1) + ' / ' + visible.length + '</span>' +
        '</div>' +
      '</section>'
    );

    const whyBtn = document.getElementById('why-btn');
    if (whyBtn) {
      whyBtn.addEventListener('click', function () {
        const t = document.getElementById('why-text');
        t.hidden = !t.hidden;
        whyBtn.setAttribute('aria-expanded', String(!t.hidden));
      });
    }

    const backBtn = document.getElementById('back-btn');
    if (backBtn) backBtn.addEventListener('click', function () { state.idx -= 1; saveState(); screenQuestion(); });

    if (isEquityInputs) {
      function updateSum() {
        let sum = 0;
        const map = {};
        app.querySelectorAll('.equity-percent-field').forEach(function (inp) {
          const v = parseFloat(inp.value) || 0;
          sum += v;
          map[inp.getAttribute('data-founder')] = v;
        });
        const sumEl = document.getElementById('equity-sum-text');
        if (sumEl) {
          sumEl.textContent = 'Сумма: ' + sum + '%' + (sum === 100 ? ' (норма)' : ' (должна быть 100%)');
          sumEl.style.color = sum === 100 ? '#4ade80' : '#f87171';
        }
        state.answers[q.id] = map;
        saveState();
      }

      function bindEquityEvents() {
        app.querySelectorAll('.equity-percent-field').forEach(function (inp) {
          inp.removeEventListener('input', updateSum);
          inp.addEventListener('input', updateSum);
        });
        app.querySelectorAll('.equity-del-btn').forEach(function (btn) {
          btn.onclick = function () {
            const row = btn.closest('.equity-input-row');
            if (row) {
              row.remove();
              const allRows = app.querySelectorAll('.equity-input-row');
              allRows.forEach(function (r, i) {
                const idx = i + 1;
                r.setAttribute('data-index', String(idx));
                const lbl = r.querySelector('label');
                if (lbl) lbl.textContent = 'Основатель ' + idx;
                const field = r.querySelector('.equity-percent-field');
                if (field) field.setAttribute('data-founder', 'founder_' + idx);
                const del = r.querySelector('.equity-del-btn');
                if (del) {
                  if (allRows.length <= 2) del.remove();
                  else del.setAttribute('data-del', String(idx));
                }
              });
              updateSum();
            }
          };
        });
      }

      bindEquityEvents();

      const addBtn = document.getElementById('equity-add-founder-btn');
      if (addBtn) {
        addBtn.addEventListener('click', function () {
          const container = document.getElementById('equity-rows-container');
          if (!container) return;
          const rows = container.querySelectorAll('.equity-input-row');
          const newIdx = rows.length + 1;
          const key = 'founder_' + newIdx;
          const div = document.createElement('div');
          div.className = 'equity-input-row';
          div.setAttribute('data-index', String(newIdx));
          div.innerHTML = '<label>Основатель ' + newIdx + '</label>' +
            '<div class="equity-percent-wrap">' +
              '<input type="number" class="equity-percent-field" data-founder="' + key + '" min="0" max="100" value="0" placeholder="0" />' +
              '<span>%</span>' +
              '<button type="button" class="equity-del-btn" data-del="' + newIdx + '" title="Удалить" style="background:none; border:none; color:#f87171; font-size:16px; cursor:pointer; padding:0 4px; line-height:1;">✕</button>' +
            '</div>';
          container.appendChild(div);
          bindEquityEvents();
          updateSum();
        });
      }

      const equalBtn = document.getElementById('equity-equal-btn');
      if (equalBtn) {
        equalBtn.addEventListener('click', function () {
          const fields = app.querySelectorAll('.equity-percent-field');
          const eqVal = Math.floor(100 / fields.length);
          fields.forEach(function (inp, idx) {
            inp.value = idx === fields.length - 1 ? (100 - eqVal * (fields.length - 1)) : eqVal;
          });
          updateSum();
        });
      }
    }

    if (isEntityBuilder) {
      function collectEntities() {
        const arr = [];
        app.querySelectorAll('#entity-cards-container .jur-card').forEach(function (card, idx) {
          const selJurBtn = card.querySelector('.jur-btn.selected');
          const jur = selJurBtn ? selJurBtn.getAttribute('data-jur') : 'kz';
          const roles = [];
          card.querySelectorAll('.role-chip.selected').forEach(function (chip) {
            roles.push(chip.getAttribute('data-role'));
          });
          arr.push({ index: idx + 2, jurisdiction: jur, roles: roles });
        });
        state.answers[q.id] = arr;
        saveState();
      }

      function bindBuilderEvents() {
        app.querySelectorAll('#entity-cards-container .jur-btn').forEach(function (btn) {
          btn.onclick = function () {
            const card = btn.closest('.jur-card');
            if (!card) return;
            card.querySelectorAll('.jur-btn').forEach(function (b) { b.classList.remove('selected'); });
            btn.classList.add('selected');
            collectEntities();
          };
        });

        app.querySelectorAll('#entity-cards-container .role-chip').forEach(function (chip) {
          chip.onclick = function () {
            chip.classList.toggle('selected');
            collectEntities();
          };
        });

        app.querySelectorAll('.entity-del-btn').forEach(function (delBtn) {
          delBtn.onclick = function () {
            const card = delBtn.closest('.jur-card');
            if (card) {
              card.remove();
              collectEntities();
            }
          };
        });
      }

      bindBuilderEvents();

      const addEntBtn = document.getElementById('add-entity-btn');
      if (addEntBtn) {
        addEntBtn.addEventListener('click', function () {
          const container = document.getElementById('entity-cards-container');
          if (!container) return;
          const currentCards = container.querySelectorAll('.jur-card');
          const newIdx = currentCards.length;
          const cNum = newIdx + 2;

          const roleList = [
            { id: 'holding', label: 'Холдинг / владение долями' },
            { id: 'clients', label: 'Работа с клиентами и договоры' },
            { id: 'payments', label: 'Платежи и выручка' },
            { id: 'ip_assets', label: 'Владение IP-активами' },
            { id: 'hiring', label: 'Найм команды' },
            { id: 'other', label: 'Другое' }
          ];

          const jurList = [
            { id: 'kz', label: 'Казахстан' },
            { id: 'aifc', label: 'МФЦА' },
            { id: 'us', label: 'США' },
            { id: 'uae', label: 'ОАЭ' },
            { id: 'uk', label: 'Великобритания' },
            { id: 'other', label: 'Другая' }
          ];

          const jBtns = jurList.map(function (j, i) {
            return '<button type="button" class="jur-btn' + (i === 0 ? ' selected' : '') + '" data-cidx="' + newIdx + '" data-jur="' + j.id + '">' + esc(j.label) + '</button>';
          }).join('');

          const rChips = roleList.map(function (r) {
            return '<span class="role-chip" data-cidx="' + newIdx + '" data-role="' + r.id + '">' + esc(r.label) + '</span>';
          }).join('');

          const newCard = document.createElement('div');
          newCard.className = 'jur-card';
          newCard.setAttribute('data-entity-idx', String(newIdx));
          newCard.innerHTML = '<div class="jur-card-title"><span>Компания ' + cNum + '</span><button type="button" class="entity-del-btn" data-del-entity="' + newIdx + '" style="background:none;border:none;color:#f87171;font-size:14px;cursor:pointer">✕ Удалить</button></div>' +
            '<div class="roles-header">Страна регистрации:</div>' +
            '<div class="jur-grid">' + jBtns + '</div>' +
            '<div class="roles-header">Для чего используется эта компания:</div>' +
            '<div class="roles-grid">' + rChips + '</div>';

          container.appendChild(newCard);
          bindBuilderEvents();
          collectEntities();
        });
      }
    }

    const nextBtn = document.getElementById('next-btn');
    if (nextBtn) {
      nextBtn.addEventListener('click', function () {
        if (isEquityInputs && (!state.answers[q.id] || typeof state.answers[q.id] !== 'object')) {
          const map = {};
          app.querySelectorAll('.equity-percent-field').forEach(function (inp) {
            map[inp.getAttribute('data-founder')] = parseFloat(inp.value) || 0;
          });
          state.answers[q.id] = map;
          saveState();
        } else if (isEntityBuilder) {
          const arr = [];
          app.querySelectorAll('#entity-cards-container .jur-card').forEach(function (card, idx) {
            const selJurBtn = card.querySelector('.jur-btn.selected');
            const jur = selJurBtn ? selJurBtn.getAttribute('data-jur') : 'kz';
            const roles = [];
            card.querySelectorAll('.role-chip.selected').forEach(function (chip) {
              roles.push(chip.getAttribute('data-role'));
            });
            arr.push({ index: idx + 2, jurisdiction: jur, roles: roles });
          });
          state.answers[q.id] = arr;
          saveState();
        } else if (isMultiple && (!Array.isArray(state.answers[q.id]) || !state.answers[q.id].length)) {
          state.answers[q.id] = [];
          saveState();
        }
        advance();
      });
    }

    function advance() {
      const prevSection = q.sectionId;
      state.idx += 1;
      saveState();
      const nextVisible = visibleQuestions(state.answers);
      if (state.idx < nextVisible.length && nextVisible[state.idx].sectionId !== prevSection) {
        track('diagnostic_section_completed', { sectionId: prevSection });
        syncAnswers(prevSection);
      }
      screenQuestion();
    }

    app.querySelectorAll('.q-option').forEach(function (btn) {
      btn.addEventListener('click', function () {
        const optId = btn.getAttribute('data-opt');
        if (isMultiple) {
          let arr = Array.isArray(state.answers[q.id]) ? state.answers[q.id].slice() : [];
          const opt = q.options.find(function (o) { return o.id === optId; });
          if (opt && opt.exclusive) {
            arr = arr.indexOf(optId) !== -1 ? [] : [optId];
          } else {
            const i = arr.indexOf(optId);
            if (i === -1) {
              arr.push(optId);
              arr = arr.filter(function (id) {
                const oo = q.options.find(function (o) { return o.id === id; });
                return !(oo && oo.exclusive);
              });
            } else {
              arr.splice(i, 1);
            }
          }
          state.answers[q.id] = arr;
          saveState();

          app.querySelectorAll('.q-option').forEach(function (otherBtn) {
            const otherId = otherBtn.getAttribute('data-opt');
            if (arr.indexOf(otherId) !== -1) {
              otherBtn.classList.add('selected');
            } else {
              otherBtn.classList.remove('selected');
            }
          });
        } else {
          state.answers[q.id] = optId;
          saveState();
          btn.classList.add('selected');
          setTimeout(advance, 220);
        }
      });
    });
  }

  async function finishDiagnostic() {
    setProgress(1);
    render(
      '<section class="q-screen">' +
        '<div class="q-meta">Диагностика завершена</div>' +
        '<h1 class="q-title">Считаем ваш Legal Score…</h1>' +
        '<div class="spinner" style="margin:30px auto"></div>' +
      '</section>'
    );
    try {
      if (!state.sessionId) {
        const created = await api('POST', '/api/sessions');
        state.sessionId = created.id;
        saveState();
      }
      await api('PUT', '/api/sessions/' + state.sessionId + '/answers', { answers: state.answers });
      const data = await api('POST', '/api/sessions/' + state.sessionId + '/complete', { answers: state.answers });
      lastResult = data.result;
      unlocked = false;
      isPaid = false;
      if (location.hash === '#/results') {
        screenResults();
      } else {
        location.hash = '#/results';
      }
    } catch (e) {
      render(
        '<section class="q-screen"><h1 class="q-title">Не удалось сохранить результат</h1>' +
        '<p class="sub" style="margin-top:16px;color:var(--ink-soft)">Проверьте соединение и попробуйте ещё раз.</p>' +
        '<div class="q-nav"><button class="btn" id="retry-btn">Повторить</button></div></section>'
      );
      document.getElementById('retry-btn').addEventListener('click', finishDiagnostic);
    }
  }

  // ---------------------------------------------------------------------
  // Results
  // ---------------------------------------------------------------------

  function gaugeSvg(value, size) {
    const stroke = Math.max(5, Math.round(size * 0.075));
    const r = (size - stroke) / 2;
    const c = 2 * Math.PI * r;
    const cls = value === null ? 'g-na'
      : value >= 75 ? 'g-good' : value >= 50 ? 'g-ok' : value >= 30 ? 'g-mid' : 'g-low';

    const effectiveVal = (value !== null && value >= 0) ? Math.max(4, value) : 0;
    const dash = value === null ? 0 : (c * effectiveVal / 100);
    const half = size / 2;
    return '<svg class="gauge ' + cls + '" width="' + size + '" height="' + size + '" viewBox="0 0 ' + size + ' ' + size + '" role="img" aria-label="' + (value === null ? 'не применимо' : value + '% из 100%') + '">' +
      '<circle class="gauge-bg" cx="' + half + '" cy="' + half + '" r="' + r + '" stroke-width="' + stroke + '"></circle>' +
      '<circle class="gauge-arc" cx="' + half + '" cy="' + half + '" r="' + r + '" stroke-width="' + stroke + '"' +
        ' stroke-dasharray="' + c.toFixed(1) + '" stroke-dashoffset="' + c.toFixed(1) + '"' +
        ' data-final="' + (c - dash).toFixed(1) + '" transform="rotate(-90 ' + half + ' ' + half + ')"></circle>' +
      '</svg>';
  }

  function animateGauges() {
    requestAnimationFrame(function () {
      requestAnimationFrame(function () {
        document.querySelectorAll('.gauge-arc').forEach(function (el) {
          el.style.strokeDashoffset = el.getAttribute('data-final');
        });
      });
    });
  }

  function sectionCard(s) {
    const isNa = s.status === 'N_A' || s.score === null || s.score === undefined;
    const scoreVal = isNa ? 0 : s.score;
    const scoreText = isNa ? '—' : (scoreVal + '%');
    const color = isNa ? 'var(--ink-faint)' : scoreVal >= 75 ? 'var(--positive)' : scoreVal >= 50 ? 'var(--warning)' : 'var(--critical)';
    const statusText = isNa ? '• Не применимо' : scoreVal >= 75 ? '• Устойчиво' : scoreVal >= 50 ? '• В зоне внимания' : '• Критический риск';
    return (
      '<div class="sec-card">' +
        '<div class="mini-gauge">' + gaugeSvg(scoreVal, 76, color) +
          '<span class="gauge-value" style="color:' + color + ';font-size:19px">' + scoreText + '</span>' +
        '</div>' +
        '<div class="sec-info">' +
          '<h3>' + esc(s.title) + '</h3>' +
          '<span class="status-badge" style="color:' + color + ';font-weight:600;font-size:12px">' + statusText + '</span>' +
        '</div>' +
      '</div>'
    );
  }

  function riskCard(r, index, withCta) {
    const s = (r.severity || 'medium').toLowerCase();
    const sevClass = (s === 'critical' || s === 'blocker') ? 'critical' : (s === 'high' ? 'high' : 'medium');
    const sevText = SEV_LABEL[s] || s.toUpperCase();
    const resKey = (r.resolution || '').toLowerCase();
    const resText = RESOLUTION_LABEL[resKey] || (r.lawyerRequired ? 'Требуется индивидуальная юридическая работа' : 'Желательно проверить с юристом');

    return (
      '<article class="risk-card rc-' + sevClass + '">' +
        '<div class="head"><h3>' + esc(r.title) + '</h3>' +
        '<span class="sev sev-' + sevClass + '">' + esc(sevText) + '</span></div>' +
        '<p class="body">' + esc(r.finding) + '</p>' +
        '<div class="sub-label">Почему это важно</div>' +
        '<p class="why">' + esc(r.whyItMatters) + '</p>' +
        '<div class="sub-label">Что делать</div>' +
        '<p class="action">' + esc(r.recommendation) + '</p>' +
        '<div class="cta-row">' +
          (withCta && r.cta ? '<button class="btn btn-secondary risk-cta" data-code="' + esc(r.code) + '" data-cta="' + esc(r.cta) + '">' + esc(r.cta) + '</button>' : '') +
          '<span class="resolution-tag">' + esc(resText) + '</span>' +
        '</div>' +
      '</article>'
    );
  }

  function heroBlock(r) {
    const summary = r.overall >= 80 ? 'Компания имеет относительно сильную юридическую основу.'
      : r.overall >= 60 ? 'Основа сформирована частично. Некоторые вопросы требуют внимания.'
      : r.overall >= 40 ? 'Диагностика выявила несколько значимых пробелов в юридической конструкции.'
      : 'Юридическая основа бизнеса пока сформирована фрагментарно.';
    const chips = [];
    if (r.criticalCount) chips.push('<span class="chip-critical">' + r.criticalCount + ' критических</span>');
    if (r.highCount) chips.push('<span class="chip-high">' + r.highCount + ' высоких</span>');
    if (r.mediumCount) chips.push('<span class="chip-medium">' + r.mediumCount + ' умеренных</span>');
    if (r.strengths && r.strengths.length) chips.push('<span class="chip-positive">' + r.strengths.length + ' сильных областей</span>');
    const confVal = r.confidence || 85;
    const confText = r.confidenceText || 'Высокая определенность ответов.';
    return (
      '<section class="score-hero">' +
        '<div class="score-label">Ваш Fenix Legal Score</div>' +
        '<div class="score-flex">' +
          '<div class="score-gauge">' + gaugeSvg(r.overall, 210) +
            '<span class="gauge-value"><b>' + r.overall + '</b><span>из 100</span></span>' +
          '</div>' +
          '<div class="score-side">' +
            '<div class="score-level">' + esc(r.levelTitle) + '</div>' +
            '<p class="score-sub">' + esc(r.levelText) + '</p>' +
            '<div class="count-chips">' + chips.join('') + '</div>' +
          '</div>' +
        '</div>' +
        '<p class="score-disclaimer">Это предварительная автоматизированная диагностика, основанная исключительно на предоставленных вами ответах. Она не заменяет индивидуальную юридическую проверку документов и фактических обстоятельств.</p>' +
      '</section>' +
      '<section class="section-scores">' +
        '<h2>Оценка по областям</h2>' +
        '<div class="sec-grid">' + r.sections.map(sectionCard).join('') + '</div>' +
      '</section>'
    );
  }

  function aiMemoBlock(sessionId) {
    return (
      '<section class="ai-memo-card" id="ai-memo-section">' +
        '<div class="ai-memo-header">' +
          '<div class="ai-memo-badge">✨ AI Legal Assistant</div>' +
          '<h2>Персональное заключение венчурного юриста</h2>' +
          '<p class="ai-memo-sub">Автоматический юридический разбор ситуации фаундеров и корпоративной структуры на основе ваших ответов (стандарт LLM Contract v1.1).</p>' +
        '</div>' +
        '<div class="ai-memo-body" id="ai-memo-content">' +
          '<div class="ai-memo-loading">' +
            '<div class="spinner"></div>' +
            '<span>Формируем индивидуальное заключение для вашей структуры…</span>' +
          '</div>' +
        '</div>' +
      '</section>'
    );
  }

  async function loadAiMemo(sessionId) {
    const el = document.getElementById('ai-memo-content');
    if (!el || !sessionId) return;
    try {
      const res = await api('POST', '/api/sessions/' + sessionId + '/ai-summary');
      if (res && res.summary) {
        el.innerHTML = formatMarkdown(res.summary);
      } else {
        el.innerHTML = '<p class="hint">Заключение сформировано и доступно при персональной консультации.</p>';
      }
    } catch (err) {
      el.innerHTML = '<p class="hint">Не удалось загрузить онлайн-заключение. Ознакомьтесь с подробной картой рисков ниже.</p>';
    }
  }

  function formatMarkdown(md) {
    if (!md) return '';
    const lines = md.split('\n');
    let out = [];
    let inUl = false;
    let inOl = false;

    for (let i = 0; i < lines.length; i++) {
      let line = lines[i].trim();
      if (!line) {
        if (inUl) { out.push('</ul>'); inUl = false; }
        if (inOl) { out.push('</ol>'); inOl = false; }
        continue;
      }

      if (line.startsWith('### ')) {
        if (inUl) { out.push('</ul>'); inUl = false; }
        if (inOl) { out.push('</ol>'); inOl = false; }
        out.push('<h3 class="ai-h3">' + esc(line.substring(4)) + '</h3>');
        continue;
      }

      if (line.startsWith('## ')) {
        if (inUl) { out.push('</ul>'); inUl = false; }
        if (inOl) { out.push('</ol>'); inOl = false; }
        out.push('<h2 class="ai-h2">' + esc(line.substring(3)) + '</h2>');
        continue;
      }

      if (line.startsWith('* ') || line.startsWith('- ')) {
        if (inOl) { out.push('</ol>'); inOl = false; }
        if (!inUl) { out.push('<ul class="ai-ul">'); inUl = true; }
        let text = line.substring(2).trim();
        text = text.replace(/^(\d+(\.\d+)*\.\s*)+/, ''); // Strip redundant numbers like 1. 1.
        text = text.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
        out.push('<li class="ai-li">' + text + '</li>');
        continue;
      }

      const numMatch = line.match(/^(\d+(\.\d+)*\.\s*)+(.*)$/);
      if (numMatch) {
        if (inUl) { out.push('</ul>'); inUl = false; }
        if (!inOl) { out.push('<ol class="ai-ol">'); inOl = true; }
        let text = numMatch[3].trim();
        text = text.replace(/^(\d+(\.\d+)*\.\s*)+/, ''); // Strip any extra nested numbers
        text = text.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
        out.push('<li class="ai-oli">' + text + '</li>');
        continue;
      }

      if (inUl) { out.push('</ul>'); inUl = false; }
      if (inOl) { out.push('</ol>'); inOl = false; }

      let text = line.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
      out.push('<p class="ai-p">' + text + '</p>');
    }

    if (inUl) out.push('</ul>');
    if (inOl) out.push('</ol>');

    return out.join('');
  }

  function renderBlurredReportBackground(r, sessionId) {
    const bySeverity = { critical: [], high: [], medium: [] };
    r.risks.forEach(function (x) {
      const s = (x.severity || '').toLowerCase();
      if (s === 'critical' || s === 'blocker') bySeverity.critical.push(x);
      else if (s === 'high') bySeverity.high.push(x);
      else bySeverity.medium.push(x);
    });

    const mockAiMemo =
      '<section class="ai-memo-card" style="margin-bottom:28px">' +
        '<div class="ai-memo-badge">AI Legal Assistant · Заключение венчурного юриста</div>' +
        '<div class="ai-memo-header"><h2>Персональное юридическое заключение (Legal Memo)</h2></div>' +
        '<div class="ai-memo-sub">Автоматический юридический разбор ситуации фаундеров, структуры и прав на продукт от венчурного юриста Fenix Law.</div>' +
        '<div class="ai-memo-content markdown-body">' +
          '<h3>🎯 1. Юридический профиль проекта</h3>' +
          '<p>Комплексный анализ структуры владения и ключевых юридических активов компании выявил критические зоны внимания...</p>' +
          '<h3>⚠️ 2. Ключевые точки внимания</h3>' +
          '<p>• 🔴 Права на интеллектуальную собственность требуют срочного оформления передачи прав на компанию...</p>' +
          '<p>• 🟠 Риск блокировки корпоративного управления и дедлока при отсутствии утвержденного регламента...</p>' +
          '<h3>📋 3. Пошаговый Action Plan</h3>' +
          '<p>1. <strong>Оформление долей и Vesting</strong>: Подготовить соглашение основателей с графиком перехода долей...</p>' +
          '<p>2. <strong>Передача прав на интеллектуальную собственность</strong>: Подготовить и подписать IP Assignment договоры...</p>' +
          '<p>3. <strong>Порядок принятия решений</strong>: Утвердить матрицу ключевых решений и процедуру преодоления тупиков...</p>' +
        '</div>' +
      '</section>';

    return (
      '<div class="blurred-preview-layer">' +
        mockAiMemo +
        block('Критические вопросы', bySeverity.critical, 'Вопросы, которые могут влиять на контроль над компанией, принадлежность продукта или ближайшую сделку.') +
        block('Существенные вопросы', bySeverity.high, 'Пробелы, которые, вероятно, потребуется закрыть при росте или инвестиционном раунде.') +
        block('Умеренные вопросы', bySeverity.medium, 'Вопросы, требующие внимания в рабочем порядке.') +
        buildRoadmap(r) +
      '</div>'
    );
  }

  let currentPricing = { priceKzt: 19999, oldPriceKzt: 49990, currency: '₸', discountPercent: 60 };

  async function fetchPricing() {
    try {
      const data = await api('GET', '/api/sessions/pricing');
      if (data && data.priceKzt) {
        currentPricing = data;
      }
    } catch (e) {
      // fallback
    }
  }

  function renderPaywallSection(sessionId) {
    const p = currentPricing.priceKzt ? currentPricing.priceKzt.toLocaleString('ru') : '19 999';
    const o = currentPricing.oldPriceKzt ? currentPricing.oldPriceKzt.toLocaleString('ru') : '49 990';
    const disc = currentPricing.discountPercent != null ? currentPricing.discountPercent : 60;

    return (
      '<section class="pay-card-container" id="pay-section">' +
        '<div class="pay-badge-top">🔥 Разблокировать полный отчёт</div>' +
        '<h2 style="font-size:26px;color:#FFF;margin-bottom:8px">Полный юридический отчёт + AI-заключение + Action Plan</h2>' +
        '<p style="color:var(--ink-soft);max-width:540px;margin:0 auto 16px;font-size:14.5px">Получите полную карту уязвимостей, детальные рекомендации венчурного юриста, AI-меморандум и официальный PDF-отчет для инвесторов.</p>' +
        '<div class="pay-price-box">' +
          '<span class="pay-price-current">' + p + ' ₸</span>' +
          '<span class="pay-price-old">' + o + ' ₸</span>' +
          '<span class="pay-price-discount">-' + disc + '%</span>' +
        '</div>' +
        '<ul class="tariff-checklist" style="max-width:440px;margin:0 auto 24px;text-align:left">' +
          '<li><span class="chk">✓</span> Разблокировка всех выявленных рисков и персональных рекомендаций</li>' +
          '<li><span class="chk">✓</span> Персональное AI-заключение венчурного юриста Fenix Law</li>' +
          '<li><span class="chk">✓</span> Пошаговый 30–60 дневный Action Plan для фаундеров</li>' +
          '<li><span class="chk">✓</span> Официальный PDF-отчёт Fenix Legal Score для инвесторов</li>' +
          '<li><span class="chk">✓</span> Приоритетная скидка на персональную консультацию</li>' +
        '</ul>' +
        '<div style="max-width:440px;margin:0 auto 16px;text-align:left">' +
          '<div class="field"><label for="g-name" style="font-size:12.5px">Ваше имя (необязательно)</label><input id="g-name" placeholder="Фаундер / СЕО" maxlength="120"></div>' +
          '<div class="field" style="margin-top:10px"><label for="g-email" style="font-size:12.5px">Email (для отправки копии PDF)</label><input id="g-email" type="email" placeholder="founder@company.com" maxlength="200"></div>' +
          '<div class="field" style="margin-top:10px"><label for="g-msg" style="font-size:12.5px">WhatsApp / Telegram (необязательно)</label><input id="g-msg" placeholder="@username / +7..." maxlength="120"></div>' +
        '</div>' +
        '<div class="pay-btn-group">' +
          '<button class="btn-kaspi" id="btn-pay-kaspi">🔴 Оплатить ' + p + ' ₸ через Kaspi Pay</button>' +
          '<button class="btn-demo" id="btn-pay-demo">⚡ Демо-оплата в 1 клик (Бесплатно)</button>' +
        '</div>' +
        '<div class="form-error" id="pay-err" hidden style="margin-top:14px"></div>' +
      '</section>'
    );
  }

  function openKaspiPayModal(sessionId) {
    const p = currentPricing.priceKzt ? currentPricing.priceKzt.toLocaleString('ru') : '19 999';

    modalRoot.innerHTML =
      '<div class="paywall-modal-overlay" id="kaspi-overlay">' +
        '<div class="paywall-modal fade-in" role="dialog" aria-modal="true">' +
          '<button class="close-btn" id="kaspi-close" aria-label="Закрыть">×</button>' +
          '<h2 style="color:#F14635;display:flex;align-items:center;gap:10px">🔴 Оплата через Kaspi Pay</h2>' +
          '<div class="pay-price-box" style="justify-content:flex-start;margin:16px 0">' +
            '<span class="pay-price-current" style="font-size:32px">' + p + ' ₸</span>' +
          '</div>' +
          '<div style="background:var(--bg-card);border:1px solid var(--line);border-radius:var(--radius);padding:18px;margin:16px 0">' +
            '<p style="color:var(--ink);font-weight:600;margin-bottom:8px">Интеграция Kaspi Pay в процессе подключения</p>' +
            '<p style="color:var(--ink-soft);font-size:13.5px;line-height:1.5">Прямой эквайринг Kaspi QR / Kaspi Pay сейчас на этапе сертификации. Для мгновенного открытия отчёта и тестирования функционала вы можете воспользоваться бесплатной демо-оплатой в 1 клик.</p>' +
          '</div>' +
          '<button class="btn-demo" id="kaspi-modal-demo-btn" style="width:100%">⚡ Открыть полный отчёт через Демо-оплату</button>' +
        '</div>' +
      '</div>';

    function close() { modalRoot.innerHTML = ''; }
    document.getElementById('kaspi-close').addEventListener('click', close);
    document.getElementById('kaspi-overlay').addEventListener('click', function (e) {
      if (e.target === e.currentTarget) close();
    });
    document.getElementById('kaspi-modal-demo-btn').addEventListener('click', function () {
      close();
      executeDemoPayment(sessionId);
    });
  }

  async function executeDemoPayment(sessionId) {
    const errEl = document.getElementById('pay-err');
    if (errEl) errEl.hidden = true;

    const nameIn = document.getElementById('g-name');
    const emailIn = document.getElementById('g-email');
    const msgIn = document.getElementById('g-msg');

    const name = nameIn ? nameIn.value.trim() : '';
    const email = emailIn ? emailIn.value.trim() : '';
    const msg = msgIn ? msgIn.value.trim() : '';

    if (name || email) {
      try {
        await api('POST', '/api/leads', {
          sessionId: sessionId,
          type: 'report_gate',
          name: name || 'Фаундер',
          email: email || 'demo@fenixlegal.kz',
          messenger: msg
        });
      } catch (e) {
        // ignore
      }
    }

    try {
      await api('POST', '/api/sessions/' + sessionId + '/pay', {
        amount: currentPricing.priceKzt || 19999,
        method: 'demo_instant'
      });
      isPaid = true;
      unlocked = true;
      location.hash = '#/report/' + sessionId;
      screenFullReport(sessionId);
    } catch (err) {
      if (errEl) {
        errEl.textContent = 'Ошибка проведения оплаты: ' + err.message;
        errEl.hidden = false;
      }
    }
  }

  async function screenResults() {
    if (!lastResult) { loadResultFromServer(state.sessionId, '#/results'); return; }
    if (isPaid) {
      screenFullReport(state.sessionId);
      return;
    }

    await fetchPricing();
    const r = lastResult;
    setProgress(1);
    track('score_viewed', { overall: r.overall });

    const p = currentPricing.priceKzt ? currentPricing.priceKzt.toLocaleString('ru') : '19 999';

    render(
      heroBlock(r) +
      '<div class="paywall-overlay-wrapper" id="pay-section">' +
        renderBlurredReportBackground(r, state.sessionId) +
        renderPaywallSection(state.sessionId) +
      '</div>' +
      '<div class="mobile-sticky-bar">' +
        '<div class="bar-info">' +
          '<span class="bar-title">Fenix Legal Score: <b>' + r.overall + '/100</b></span>' +
          '<span class="bar-sub">Разблокировать полный отчёт</span>' +
        '</div>' +
        '<button class="btn btn-sm" id="sticky-pay-btn">Разблокировать (' + p + ' ₸)</button>' +
      '</div>'
    );
    animateGauges();
    track('report_gate_viewed');

    const kaspiBtn = document.getElementById('btn-pay-kaspi');
    if (kaspiBtn) kaspiBtn.addEventListener('click', function () { openKaspiPayModal(state.sessionId); });

    const demoBtn = document.getElementById('btn-pay-demo');
    if (demoBtn) demoBtn.addEventListener('click', function () { executeDemoPayment(state.sessionId); });

    const stickyBtn = document.getElementById('sticky-pay-btn');
    if (stickyBtn) {
      stickyBtn.addEventListener('click', function () {
        document.getElementById('pay-section').scrollIntoView({ behavior: 'smooth' });
      });
    }
  }

  let isPaid = false;

  async function loadResultFromServer(sessionId, backHash) {
    if (!sessionId) { location.hash = '#/'; return; }
    render('<section class="q-screen"><h1 class="q-title">Загружаем отчёт…</h1></section>');
    try {
      const data = await api('GET', '/api/sessions/' + sessionId + '/result');
      lastResult = data.result;
      unlocked = data.unlocked;
      isPaid = Boolean(data.paid);
      route();
    } catch (e) {
      location.hash = '#/';
    }
  }

  function downloadPDFReport() {
    if (state.sessionId) {
      window.open('/api/sessions/' + state.sessionId + '/pdf', '_blank');
      return;
    }
    window.print();
  }

  function buildRoadmap(r) {
    const nowItems = r.risks.filter(function (x) { return (x.severity || '').toLowerCase() === 'critical' || (x.severity || '').toLowerCase() === 'blocker'; });
    const soonItems = r.risks.filter(function (x) { return (x.severity || '').toLowerCase() === 'high'; });
    const laterItems = r.risks.filter(function (x) { return (x.severity || '').toLowerCase() === 'medium'; });
    function list(items) {
      return '<ol>' + items.map(function (x) { return '<li>' + esc(x.title) + '</li>'; }).join('') + '</ol>';
    }
    let html = '<section class="roadmap"><h2>Что делать дальше</h2>';
    if (nowItems.length) html += '<div class="phase"><div class="phase-title">Сейчас</div>' + list(nowItems) + '</div>';
    if (soonItems.length) html += '<div class="phase"><div class="phase-title">В течение 30 дней</div>' + list(soonItems) + '</div>';
    if (laterItems.length) html += '<div class="phase"><div class="phase-title">Перед следующим этапом роста</div>' + list(laterItems) + '</div>';
    if (!nowItems.length && !soonItems.length && !laterItems.length) {
      html += '<p class="hint" style="color:var(--ink-soft)">Существенных действий не требуется — вернитесь к диагностике перед инвестиционным раундом или значимым изменением структуры.</p>';
    }
    html += '</section>';
    return html;
  }

  function block(title, items, subtitle) {
    if (!items || !items.length) return '';
    return (
      '<section class="risks-block">' +
        '<h2>' + esc(title) + '</h2>' +
        (subtitle ? '<p class="hint">' + esc(subtitle) + '</p>' : '') +
        '<div class="risks-grid">' +
          items.map(function (x, idx) { return riskCard(x, idx, true); }).join('') +
        '</div>' +
      '</section>'
    );
  }

  function screenFullReport(sessionId) {
    if (!lastResult) { loadResultFromServer(sessionId, '#/report/' + sessionId); return; }
    if (!isPaid) {
      location.hash = '#/results';
      screenResults();
      return;
    }

    const r = lastResult;
    setProgress(1);
    track('full_report_viewed');

    const bySeverity = { critical: [], high: [], medium: [] };
    r.risks.forEach(function (x) {
      const s = (x.severity || '').toLowerCase();
      if (s === 'critical' || s === 'blocker') bySeverity.critical.push(x);
      else if (s === 'high') bySeverity.high.push(x);
      else bySeverity.medium.push(x);
    });

    const primaryCtaText = (r.consulting && r.consulting.primaryCta) ? r.consulting.primaryCta : 'Разобрать мои результаты с Fenix Law';
    const primaryServiceCode = (r.consulting && r.consulting.primaryServiceCode) ? r.consulting.primaryServiceCode : '';

    const strengths = r.strongAreas && r.strongAreas.length
      ? '<section class="risks-block"><h2>Сильные стороны компании</h2><p class="hint">Области с устойчивой правовой структурой.</p><div class="strong-list">' +
        r.strongAreas.map(function (s) { return '<span>✓ ' + esc(s) + '</span>'; }).join('') + '</div></section>'
      : '';

    const mainContent =
      heroBlock(r) +
      '<div style="text-align:center;margin:28px 0">' +
        '<button class="btn" id="download-pdf-btn" style="padding:14px 28px;font-size:15px">📥 Скачать официальный PDF-отчёт</button>' +
      '</div>' +
      aiMemoBlock(sessionId) +
      strengths +
      '<section class="gate" style="margin-top:56px">' +
        '<h2>Персональный юридический разбор Fenix Law</h2>' +
        '<p>Мы уже знаем основные результаты вашей диагностики. Не нужно заново объяснять историю компании: вместе с запросом будут переданы ваши ответы, выявленные риски и Legal Score.</p>' +
        '<div class="cta-row" style="margin-top:22px;display:flex;justify-content:center;align-items:center">' +
          '<button class="btn risk-cta" data-code="' + esc(primaryServiceCode) + '" data-cta="' + esc(primaryCtaText) + '">' + esc(primaryCtaText) + '</button>' +
        '</div>' +
      '</section>';

    render(mainContent);
    animateGauges();
    loadAiMemo(sessionId);
    bindRiskCtas();

    const pdfBtn = document.getElementById('download-pdf-btn');
    if (pdfBtn) pdfBtn.addEventListener('click', downloadPDFReport);
  }

  // ---------------------------------------------------------------------
  // Consultation modal
  // ---------------------------------------------------------------------

  const INTEREST_OPTIONS = [
    'Исправить найденные риски',
    'Подготовиться к инвестициям',
    'Разобраться с IP',
    'Урегулировать отношения фаундеров',
    'Провести полную юридическую проверку',
    'Другое',
  ];

  function bindRiskCtas() {
    app.querySelectorAll('.risk-cta').forEach(function (btn) {
      btn.addEventListener('click', function () {
        track('risk_cta_clicked', { code: btn.getAttribute('data-code'), cta: btn.getAttribute('data-cta') });
        openConsultModal(btn.getAttribute('data-code'), btn.getAttribute('data-interest') || '');
      });
    });
  }

  function openConsultModal(riskCode, presetInterest) {
    const options = INTEREST_OPTIONS.map(function (o) {
      return '<option' + (o === presetInterest ? ' selected' : '') + '>' + o + '</option>';
    }).join('');
    modalRoot.innerHTML =
      '<div class="modal-overlay" id="modal-overlay">' +
        '<div class="modal fade-in" role="dialog" aria-modal="true" aria-labelledby="m-title">' +
          '<button class="modal-close" id="modal-close" aria-label="Закрыть">×</button>' +
          '<h2 id="m-title">Разберём конкретно вашу ситуацию</h2>' +
          '<p style="margin-top:12px;color:var(--ink-soft);font-size:14.5px">Мы уже знаем основные результаты вашей диагностики. Не нужно заново объяснять историю компании: вместе с запросом будут переданы ваши ответы, выявленные риски и Legal Score.</p>' +
          '<form id="consult-form" style="margin-top:22px;display:grid;gap:14px">' +
            '<div class="field"><label for="c-name">Имя</label><input id="c-name" required maxlength="120"></div>' +
            '<div class="field"><label for="c-company">Название компании</label><input id="c-company" maxlength="200"></div>' +
            '<div class="field"><label for="c-website">Website — необязательно</label><input id="c-website" maxlength="200"></div>' +
            '<div class="field"><label for="c-email">Email</label><input id="c-email" type="email" required maxlength="200"></div>' +
            '<div class="field"><label for="c-msg">WhatsApp / Telegram</label><input id="c-msg" maxlength="120"></div>' +
            '<div class="field"><label for="c-interest">Что сейчас наиболее актуально?</label><select id="c-interest">' + options + '</select></div>' +
            '<div class="form-error" id="c-error" hidden></div>' +
            '<button class="btn" type="submit">Передать результаты Fenix Law</button>' +
          '</form>' +
        '</div>' +
      '</div>';

    function close() { modalRoot.innerHTML = ''; }
    document.getElementById('modal-close').addEventListener('click', close);
    document.getElementById('modal-overlay').addEventListener('click', function (e) {
      if (e.target === e.currentTarget) close();
    });
    document.addEventListener('keydown', function onEsc(e) {
      if (e.key === 'Escape') { close(); document.removeEventListener('keydown', onEsc); }
    });
    document.getElementById('c-name').focus();

    document.getElementById('consult-form').addEventListener('submit', async function (e) {
      e.preventDefault();
      const errEl = document.getElementById('c-error');
      errEl.hidden = true;
      const btn = e.target.querySelector('button[type=submit]');
      btn.disabled = true;
      try {
        await api('POST', '/api/leads', {
          sessionId: state.sessionId,
          type: 'consultation',
          name: document.getElementById('c-name').value.trim(),
          company: document.getElementById('c-company').value.trim(),
          website: document.getElementById('c-website').value.trim(),
          email: document.getElementById('c-email').value.trim(),
          messenger: document.getElementById('c-msg').value.trim(),
          interest: document.getElementById('c-interest').value,
          sourceRiskCode: riskCode || undefined,
        });
        document.querySelector('.modal').innerHTML =
          '<h2>Спасибо</h2><p style="margin-top:14px;color:var(--ink-soft)">Ваши результаты переданы вместе с запросом, поэтому повторно описывать ситуацию не потребуется. Нариман свяжется с вами по указанным контактам.</p>' +
          '<div style="margin-top:24px"><button class="btn btn-secondary" id="thanks-close">Вернуться к отчёту</button></div>';
        document.getElementById('thanks-close').addEventListener('click', close);
      } catch (err) {
        btn.disabled = false;
        errEl.textContent = 'Не удалось отправить. Проверьте данные и попробуйте ещё раз.';
        errEl.hidden = false;
      }
    });
  }

  // ---------------------------------------------------------------------
  // Router
  // ---------------------------------------------------------------------

  async function route() {
    await ensureBank();
    const hash = location.hash || '#/';
    const reportMatch = /^#\/report\/([A-Za-z0-9-]+)$/.exec(hash);
    if (reportMatch) {
      if (!state.sessionId) { state.sessionId = reportMatch[1]; saveState(); }
      screenFullReport(reportMatch[1]);
      return;
    }
    if (hash === '#/intro') { screenIntro(); return; }
    if (hash === '#/diagnostic') { screenQuestion(); return; }
    if (hash === '#/results') { screenResults(); return; }
    screenLanding();
  }

  window.addEventListener('hashchange', route);
  route();
})();
