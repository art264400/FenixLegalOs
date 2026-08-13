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
    const a = answers[rule.questionId];
    switch (rule.op) {
      case 'answered': return a !== undefined && a !== null && a !== '' && !(Array.isArray(a) && !a.length);
      case 'eq': return a === rule.value;
      case 'neq': return a !== undefined && a !== rule.value;
      case 'in': return typeof a === 'string' && rule.value.indexOf(a) !== -1;
      case 'notIn': return typeof a === 'string' && rule.value.indexOf(a) === -1;
      case 'includes': return Array.isArray(a) && a.indexOf(rule.value) !== -1;
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

  const SEV_LABEL = { critical: 'Critical', high: 'High', medium: 'Medium' };
  const RESOLUTION_LABEL = {
    self: 'Можно решить самостоятельно',
    check_with_lawyer: 'Желательно проверить с юристом',
    lawyer_required: 'Требуется индивидуальная юридическая работа',
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
            '<div class="hero-badge">Fenix Legal Tech OS · Юридический аудит стартапов</div>' +
            '<h1>Насколько юридически готова ваша компания к росту?</h1>' +
            '<p class="sub">Пройдите профессиональную диагностику продукта, фаундеров, IP, данных, команды и инвестиционной готовности. Получите персональный Legal Score за 10–15 минут.</p>' +
            '<div class="cta-row">' +
              '<button class="btn" id="start-btn">Начать диагностику</button>' +
            '</div>' +
            '<div class="trust-row">' +
              '<span>⚡ Бесплатно</span><span>⏱ 10–15 минут</span><span>🔒 Без отправки коммерческих тайн</span>' +
            '</div>' +
          '</div>' +
          '<img class="hero-logo" src="/img/logo.png" alt="Fenix Law">' +
        '</div>' +
        '<div class="metrics-strip">' +
          '<div class="metric-item"><div class="val">8</div><div class="lbl">Зон риска</div></div>' +
          '<div class="metric-item"><div class="val">58</div><div class="lbl">Вопросов</div></div>' +
          '<div class="metric-item"><div class="val">0–100</div><div class="lbl">Legal Score</div></div>' +
          '<div class="metric-item"><div class="val">Roadmap</div><div class="lbl">План действий</div></div>' +
        '</div>' +
        '<div class="process-section">' +
          '<h2 class="section-heading">Как работает Fenix Legal OS</h2>' +
          '<div class="process-grid">' +
            '<div class="process-card"><div class="step-num">01</div><div class="step-title">Ответьте на вопросы</div><div class="step-desc">10–15 минут на ключевые аспекты бизнеса</div></div>' +
            '<div class="process-card"><div class="step-num">02</div><div class="step-title">Получите Score</div><div class="step-desc">Мгновенный балльный расчет защищённости</div></div>' +
            '<div class="process-card"><div class="step-num">03</div><div class="step-title">Узнайте риски</div><div class="step-desc">Выявление критических и высоких уязвимостей</div></div>' +
            '<div class="process-card"><div class="step-num">04</div><div class="step-title">План действий</div><div class="step-desc">Пошаговый юридический Roadmap</div></div>' +
          '</div>' +
        '</div>' +
        '<div class="hero-domains">' +
          '<h2 class="section-heading">Восемь областей диагностики</h2>' +
          '<div class="domain-grid">' +
            '<div class="domain-card"><div class="d-name">Founders</div><div class="d-tags">роли, доли, vesting, выход</div></div>' +
            '<div class="domain-card"><div class="d-name">Corporate</div><div class="d-tags">структура, ТОО/МФЦА, cap table</div></div>' +
            '<div class="domain-card"><div class="d-name">IP</div><div class="d-tags">права на код, дизайн, торговый знак</div></div>' +
            '<div class="domain-card"><div class="d-name">Team</div><div class="d-tags">сотрудники, подрядчики, NDA</div></div>' +
            '<div class="domain-card"><div class="d-name">Product</div><div class="d-tags">Terms of Use, оферта, пользователи</div></div>' +
            '<div class="domain-card"><div class="d-name">Data &amp; AI</div><div class="d-tags">privacy, GDPR, интеграции LLM</div></div>' +
            '<div class="domain-card"><div class="d-name">Contracts</div><div class="d-tags">B2B-договоры, SLA, лимиты</div></div>' +
            '<div class="domain-card"><div class="d-name">Investment</div><div class="d-tags">готовность к раунду, Data Room</div></div>' +
          '</div>' +
        '</div>' +
        '<div class="method">' +
          '<h2>Сначала диагноз. Потом документы.</h2>' +
          '<p>Fenix Law не просто готовит юридические документы. Сначала мы понимаем продукт, бизнес-модель, отношения между фаундерами, движение денег, IP и будущий рост компании — и затем выстраиваем юридическую архитектуру бизнеса.</p>' +
          '<p>Система поможет определить потенциальные юридические пробелы и понять, какие действия целесообразны дальше.</p>' +
        '</div>' +
      '</section>'
    );
    document.getElementById('start-btn').addEventListener('click', async function () {
      if (!state.sessionId) {
        try {
          const created = await api('POST', '/api/sessions');
          state.sessionId = created.id;
          saveState();
        } catch (e) { /* offline-tolerant */ }
      }
      state.idx = 0;
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
        '<h1 style="font-size:clamp(28px,4.5vw,40px)">Восемь областей вашей юридической конструкции</h1>' +
        '<div class="intro-list">' + rows + '</div>' +
        '<div class="note-quote">Здесь нет правильных и неправильных ответов. Задача диагностики — понять вашу текущую юридическую архитектуру и определить уязвимые места.</div>' +
        '<div class="q-nav">' +
          '<button class="btn" id="continue-btn">Продолжить</button>' +
          '<button class="btn-ghost" onclick="location.hash=\'#/\'">Назад</button>' +
        '</div>' +
      '</section>'
    );
    document.getElementById('continue-btn').addEventListener('click', async function () {
      if (!state.sessionId) {
        try {
          const created = await api('POST', '/api/sessions');
          state.sessionId = created.id;
          saveState();
        } catch (e) { /* offline-tolerant: продолжаем локально */ }
      }
      state.idx = 0;
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

    const optionsHtml = q.options.map(function (o) {
      const selected = isMultiple
        ? (Array.isArray(current) && current.indexOf(o.id) !== -1)
        : current === o.id;
      return '<button class="q-option' + (selected ? ' selected' : '') + '" data-opt="' + esc(o.id) + '">' + esc(o.label) + '</button>';
    }).join('');

    render(
      '<section class="q-screen">' +
        '<div class="q-meta-strip">' +
          '<span class="q-section-pill">Шаг ' + section.order + ' из ' + bank.sections.length + ' · ' + esc(section.title) + '</span>' +
          '<span class="q-count-pill">Вопрос ' + (state.idx + 1) + ' из ' + visible.length + '</span>' +
        '</div>' +
        '<h1 class="q-title">' + esc(q.question) + '</h1>' +
        '<div class="q-options">' + optionsHtml + '</div>' +
        (q.explanation
          ? '<div class="q-why"><button id="why-btn" aria-expanded="false">Почему мы это спрашиваем?</button>' +
            '<div class="why-text" id="why-text" hidden>' + esc(q.explanation) + '</div></div>'
          : '') +
        '<div class="q-nav">' +
          (state.idx > 0 ? '<button class="btn-ghost" id="back-btn">← Назад</button>' : '<div></div>') +
          (isMultiple ? '<button class="btn" id="next-btn">Продолжить</button>' : '') +
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
            if (i === -1) { arr.push(optId); arr = arr.filter(function (id) { const oo = q.options.find(function (o) { return o.id === id; }); return !(oo && oo.exclusive); }); }
            else arr.splice(i, 1);
          }
          state.answers[q.id] = arr;
          saveState();
          screenQuestion();
        } else {
          state.answers[q.id] = optId;
          saveState();
          btn.classList.add('selected');
          setTimeout(advance, 220);
        }
      });
    });

    const nextBtn = document.getElementById('next-btn');
    if (nextBtn) nextBtn.addEventListener('click', function () {
      if (!Array.isArray(state.answers[q.id]) || !state.answers[q.id].length) {
        state.answers[q.id] = [];
      }
      advance();
    });
  }

  async function finishDiagnostic() {
    setProgress(1);
    render('<section class="q-screen"><div class="q-meta">Диагностика завершена</div><h1 class="q-title">Считаем ваш Legal Score…</h1></section>');
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
      location.hash = '#/results';
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
    const stroke = Math.max(5, Math.round(size * 0.055));
    const r = (size - stroke) / 2;
    const c = 2 * Math.PI * r;
    const cls = value === null ? 'g-na'
      : value >= 75 ? 'g-good' : value >= 60 ? 'g-ok' : value >= 40 ? 'g-mid' : 'g-low';
    const dash = value === null ? 0 : c * value / 100;
    const half = size / 2;
    return '<svg class="gauge ' + cls + '" width="' + size + '" height="' + size + '" viewBox="0 0 ' + size + ' ' + size + '" role="img" aria-label="' + (value === null ? 'не применимо' : value + ' из 100') + '">' +
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
    const statusText = s.score === null ? 'Не оценен'
      : s.score >= 75 ? 'Устойчиво'
      : s.score >= 45 ? 'В зоне внимания'
      : 'Критический риск';
    const statusColor = s.score === null ? 'var(--ink-muted)'
      : s.score >= 75 ? 'var(--positive)'
      : s.score >= 45 ? 'var(--high)'
      : 'var(--critical)';

    return '<div class="sec-card">' +
      '<div class="mini-gauge">' + gaugeSvg(s.score, 76) +
        '<span class="gauge-value">' + (s.score === null ? '—' : s.score + '%') + '</span>' +
      '</div>' +
      '<div class="sec-info">' +
        '<div class="sec-name">' + esc(s.title) + '</div>' +
        '<div class="sec-status" style="color:' + statusColor + '">• ' + statusText + '</div>' +
      '</div>' +
      '</div>';
  }

  function riskCard(r, index, withCta) {
    const num = index !== undefined ? '<span class="risk-num">' + String(index + 1).padStart(2, '0') + '</span>' : '';
    return (
      '<article class="risk-card rc-' + r.severity + '">' +
        '<div class="head">' + num + '<h3>' + esc(r.title) + '</h3>' +
        '<span class="sev sev-' + r.severity + '">' + SEV_LABEL[r.severity] + '</span></div>' +
        '<p class="body">' + esc(r.finding) + '</p>' +
        '<div class="sub-label">Почему это важно</div>' +
        '<p class="why">' + esc(r.whyItMatters) + '</p>' +
        '<div class="sub-label">Что делать</div>' +
        '<p class="action">' + esc(r.recommendation) + '</p>' +
        '<div class="cta-row">' +
          (withCta && r.cta ? '<button class="btn btn-secondary risk-cta" data-code="' + esc(r.code) + '" data-cta="' + esc(r.cta) + '">' + esc(r.cta) + '</button>' : '') +
          '<span class="resolution-tag">' + RESOLUTION_LABEL[r.resolution] + '</span>' +
        '</div>' +
      '</article>'
    );
  }

  function heroBlock(r) {
    const summary = 'На основании ' + r.answeredCount + ' ответов' +
      (r.risks.length
        ? ' мы выявили вопросы, требующие внимания:'
        : ' существенных пробелов не выявлено — отдельные вопросы стоит проверить при следующем этапе роста.');
    const chips = [];
    if (r.criticalCount) chips.push('<span class="chip-critical">' + r.criticalCount + ' критических</span>');
    if (r.highCount) chips.push('<span class="chip-high">' + r.highCount + ' существенных</span>');
    if (r.mediumCount) chips.push('<span class="chip-medium">' + r.mediumCount + ' умеренных</span>');
    if (r.strengths.length) chips.push('<span class="chip-positive">' + r.strengths.length + ' сильных областей</span>');
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
            '<p class="score-sub">' + esc(summary) + '</p>' +
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

  function screenResults() {
    if (!lastResult) { loadResultFromServer(state.sessionId, '#/results'); return; }
    const r = lastResult;
    setProgress(1);
    track('score_viewed', { overall: r.overall });

    const top3 = r.risks.slice(0, 3);
    const topHtml = top3.length
      ? '<section class="risks-block"><h2>Что требует внимания в первую очередь</h2>' +
        '<p class="hint">Три самых значимых вопроса по результатам диагностики.</p>' +
        top3.map(function (risk, i) { return riskCard(risk, i, false); }).join('') + '</section>'
      : '';

    render(
      heroBlock(r) + topHtml +
      '<section class="gate" id="gate">' +
        '<h2>Получить полный персональный отчёт и roadmap</h2>' +
        '<p>Мы отправим вам полный отчёт, чтобы вы могли вернуться к нему позже. Внутри — полная карта рисков, сильные стороны и последовательность действий.</p>' +
        '<form id="gate-form">' +
          '<div class="field"><label for="g-name">Имя</label><input id="g-name" required maxlength="120" autocomplete="name"></div>' +
          '<div class="field"><label for="g-email">Email</label><input id="g-email" type="email" required maxlength="200" autocomplete="email"></div>' +
          '<div class="field"><label for="g-msg">WhatsApp / Telegram — необязательно</label><input id="g-msg" maxlength="120"></div>' +
          '<div class="form-error" id="g-error" hidden></div>' +
          '<button class="btn" type="submit">Открыть полный отчёт</button>' +
        '</form>' +
      '</section>' +
      '<div class="mobile-sticky-bar">' +
        '<div class="bar-info">' +
          '<span class="bar-title">Fenix Legal Score: <b>' + r.overall + '/100</b></span>' +
          '<span class="bar-sub">Разблокировать все 8 областей</span>' +
        '</div>' +
        '<button class="btn btn-sm" id="sticky-pay-btn">Разблокировать (9 900 ₸)</button>' +
      '</div>'
    );
    animateGauges();
    track('report_gate_viewed');

    const stickyBtn = document.getElementById('sticky-pay-btn');
    if (stickyBtn) {
      stickyBtn.addEventListener('click', function () {
        const nameIn = document.getElementById('g-name');
        if (nameIn && !nameIn.value) nameIn.focus();
        document.getElementById('gate').scrollIntoView({ behavior: 'smooth' });
      });
    }

    document.getElementById('gate-form').addEventListener('submit', async function (e) {
      e.preventDefault();
      const errEl = document.getElementById('g-error');
      errEl.hidden = true;
      const btn = e.target.querySelector('button[type=submit]');
      btn.disabled = true;
      try {
        await api('POST', '/api/leads', {
          sessionId: state.sessionId,
          type: 'report_gate',
          name: document.getElementById('g-name').value.trim(),
          email: document.getElementById('g-email').value.trim(),
          messenger: document.getElementById('g-msg').value.trim(),
        });
        unlocked = true;
        location.hash = '#/report/' + state.sessionId;
      } catch (err) {
        btn.disabled = false;
        errEl.textContent = 'Не удалось отправить. Проверьте данные и попробуйте ещё раз.';
        errEl.hidden = false;
      }
    });
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

  function openPaywallModal(sessionId) {
    let selectedAmount = 9900; // ~$20 USD
    let selectedPackage = 'standard';

    modalRoot.innerHTML =
      '<div class="paywall-modal-overlay" id="paywall-overlay">' +
        '<div class="paywall-modal fade-in" role="dialog" aria-modal="true">' +
          '<button class="close-btn" id="paywall-close" aria-label="Закрыть">×</button>' +
          '<h2>Разблокировать полный юридический отчёт и PDF</h2>' +
          '<p style="color:var(--ink-soft);margin-top:8px;font-size:14.5px">Доступный автоматический аудит бизнеса ($20). Экономит время и средства по сравнению с личной консультацией ($150).</p>' +
          '<div class="tariff-grid">' +
            '<div class="tariff-card popular selected" id="tariff-std">' +
              '<div class="tariff-badge">Рекомендуемый выбор</div>' +
              '<div class="t-title">Полный PDF-отчёт и Roadmap</div>' +
              '<div class="t-price">$20 <span style="font-size:16px;color:var(--ink-soft)">(~9 900 ₸)</span></div>' +
              '<ul class="tariff-checklist">' +
                '<li><span class="chk">✓</span> Разблокировка всех 40+ рисков и 8 секторов</li>' +
                '<li><span class="chk">✓</span> Брендированный векторный PDF-отчёт Fenix Law</li>' +
                '<li><span class="chk">✓</span> Пошаговая дорожная карта устранения уязвимостей</li>' +
                '<li><span class="chk">✓</span> Вечный доступ (в 7.5 раз дешевле консультации)</li>' +
              '</ul>' +
            '</div>' +
            '<div class="tariff-card" id="tariff-pro">' +
              '<div class="t-title">Отчёт + Личная консультация</div>' +
              '<div class="t-price">$150 <span style="font-size:16px;color:var(--ink-soft)">(~75 000 ₸)</span></div>' +
              '<ul class="tariff-checklist">' +
                '<li><span class="chk">✓</span> Всё из тарифа «Полный отчёт»</li>' +
                '<li><span class="chk">✓</span> 60-минутная сессия с Нариманом Исановым</li>' +
                '<li><span class="chk">✓</span> Индивидуальный аудит документов и Cap Table</li>' +
              '</ul>' +
            '</div>' +
          '</div>' +
          '<div style="margin-top:20px">' +
            '<label style="font-size:13px;color:var(--ink-faint);text-transform:uppercase;letter-spacing:0.05em;font-weight:600">Выберите способ оплаты</label>' +
            '<div class="payment-methods">' +
              '<button class="pay-btn-method kaspi" id="pay-kaspi">🔴 Kaspi QR / Pay (9 900 ₸)</button>' +
              '<button class="pay-btn-method" id="pay-card">💳 Карта ($20)</button>' +
              '<button class="pay-btn-method demo" id="pay-demo">⚡ Демо-оплата (1 клик)</button>' +
            '</div>' +
          '</div>' +
          '<div class="form-error" id="pay-error" hidden style="margin-top:16px"></div>' +
        '</div>' +
      '</div>';

    function close() { modalRoot.innerHTML = ''; }
    document.getElementById('paywall-close').addEventListener('click', close);
    document.getElementById('paywall-overlay').addEventListener('click', function (e) {
      if (e.target === e.currentTarget) close();
    });

    const cardStd = document.getElementById('tariff-std');
    const cardPro = document.getElementById('tariff-pro');

    cardStd.addEventListener('click', function () {
      cardStd.classList.add('selected');
      cardPro.classList.remove('selected');
      selectedAmount = 9900;
      selectedPackage = 'standard';
    });

    cardPro.addEventListener('click', function () {
      cardPro.classList.add('selected');
      cardStd.classList.remove('selected');
      selectedAmount = 75000;
      selectedPackage = 'pro';
    });

    async function executePayment(method) {
      const errEl = document.getElementById('pay-error');
      errEl.hidden = true;
      try {
        await api('POST', '/api/sessions/' + sessionId + '/pay', {
          amount: selectedAmount,
          method: method + '_' + selectedPackage
        });
        isPaid = true;
        close();
        screenFullReport(sessionId);
      } catch (err) {
        errEl.textContent = 'Ошибка при проведении платежа. Попробуйте ещё раз.';
        errEl.hidden = false;
      }
    }

    document.getElementById('pay-kaspi').addEventListener('click', function () { executePayment('kaspi'); });
    document.getElementById('pay-card').addEventListener('click', function () { executePayment('card'); });
    document.getElementById('pay-demo').addEventListener('click', function () { executePayment('demo'); });
  }

  function buildRoadmap(r) {
    const nowItems = r.risks.filter(function (x) { return x.severity === 'critical'; });
    const soonItems = r.risks.filter(function (x) { return x.severity === 'high'; });
    const laterItems = r.risks.filter(function (x) { return x.severity === 'medium'; });
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

  function founderOsBlock(r) {
    const founderRisks = r.risks.filter(function (x) { return x.sectionId === 'founders'; });
    if (!founderRisks.length) return '';
    return (
      '<section class="gate" style="margin-top:56px">' +
        '<h2>У вас не определены правила между сооснователями</h2>' +
        '<p>На основании ваших ответов часть правил между фаундерами — уход, доли, ключевые решения — не формализована. Это одна из областей, где договориться заранее многократно дешевле, чем в конфликте.</p>' +
        '<div class="cta-row" style="margin-top:22px">' +
          '<button class="btn risk-cta" data-code="FOUNDER_OS" data-cta="Обсудить правила между фаундерами" data-interest="Урегулировать отношения фаундеров">Обсудить правила между фаундерами</button>' +
        '</div>' +
      '</section>'
    );
  }

  function screenFullReport(sessionId) {
    if (!lastResult) { loadResultFromServer(sessionId, '#/report/' + sessionId); return; }
    const r = lastResult;
    setProgress(1);
    track('full_report_viewed');

    const bySeverity = { critical: [], high: [], medium: [] };
    r.risks.forEach(function (x) { (bySeverity[x.severity] || bySeverity.medium).push(x); });

    function block(title, items, hint) {
      if (!items.length) return '';
      return '<section class="risks-block"><h2>' + title + '</h2>' +
        (hint ? '<p class="hint">' + hint + '</p>' : '') +
        items.map(function (risk, i) { return riskCard(risk, i, true); }).join('') + '</section>';
    }

    const strengths = r.strengths.length
      ? '<section class="risks-block"><h2>Сильные стороны</h2>' +
        '<p class="hint">Области, где юридическая конструкция выглядит устойчиво по результатам ответов.</p>' +
        '<div class="strong-list">' + r.strengths.map(function (s) { return '<span>' + esc(s) + '</span>'; }).join('') + '</div></section>'
      : '';

    const statusBadgeHtml = isPaid
      ? '<div style="margin:20px 0;display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:12px">' +
          '<div class="paid-badge">✓ Оплаченный отчёт разблокирован</div>' +
          '<button class="btn btn-secondary" id="download-pdf-btn" style="padding:10px 18px;font-size:14px">📄 Скачать PDF-отчёт</button>' +
        '</div>'
      : '';

    const paywallBannerHtml = !isPaid
      ? '<section class="paywall-banner">' +
          '<h3>Разблокируйте полный отчёт и PDF ($20)</h3>' +
          '<p>Автоматизированный аудит вашего стартапа всего за $20 (~9 900 ₸) — в 7.5 раз дешевле личной консультации ($150). Вы получите полный разбор всех 40+ рисков, пошаговый план и скачивание брендированного PDF-отчёта.</p>' +
          '<div class="paywall-benefits">' +
            '<div class="paywall-benefit-item"><span class="icon">✓</span> Детальная карта 40+ рисков</div>' +
            '<div class="paywall-benefit-item"><span class="icon">✓</span> Выгрузка в PDF для инвесторов</div>' +
            '<div class="paywall-benefit-item"><span class="icon">✓</span> Пошаговый план исправления</div>' +
          '</div>' +
          '<button class="btn" id="unlock-paywall-btn" style="padding:16px 36px;font-size:16px">Разблокировать отчёт за $20 (~9 900 ₸)</button>' +
        '</section>'
      : '';

    const mainContent = statusBadgeHtml +
      heroBlock(r) +
      block('Критические вопросы', bySeverity.critical, 'Вопросы, которые могут влиять на контроль над компанией, принадлежность продукта или ближайшую сделку.') +
      block('Существенные вопросы', bySeverity.high, 'Пробелы, которые, вероятно, потребуется закрыть при росте или инвестиционном раунде.') +
      block('Умеренные вопросы', bySeverity.medium, 'Вопросы, требующие внимания в рабочем порядке.') +
      strengths +
      buildRoadmap(r) +
      founderOsBlock(r) +
      '<section class="gate" style="margin-top:56px">' +
        '<h2>Разберём конкретно вашу ситуацию</h2>' +
        '<p>Мы уже знаем основные результаты вашей диагностики. Не нужно заново объяснять историю компании: вместе с запросом будут переданы ваши ответы, выявленные риски и Legal Score.</p>' +
        '<div class="cta-row" style="margin-top:22px">' +
          '<button class="btn risk-cta" data-code="" data-cta="Разобрать мои результаты">Разобрать мои результаты с Fenix Law</button>' +
        '</div>' +
      '</section>';

    if (!isPaid) {
      render(
        statusBadgeHtml +
        heroBlock(r) +
        paywallBannerHtml +
        '<div class="blurred-wrapper">' +
          '<div class="blurred-content">' +
            block('Критические вопросы', bySeverity.critical) +
            block('Существенные вопросы', bySeverity.high) +
            buildRoadmap(r) +
          '</div>' +
          '<div class="blurred-overlay-card">' +
            '<h3 style="font-family:var(--serif);font-size:24px;color:#FFF;margin-bottom:10px">Подробный разбор скрыт</h3>' +
            '<p style="color:var(--ink-soft);max-width:480px;margin-bottom:20px;font-size:14.5px">Разблокируйте полный отчёт, чтобы увидеть детальный анализ рисков, их последствия и пошаговый план юридических действий.</p>' +
            '<button class="btn" id="unlock-paywall-overlay-btn">Разблокировать отчёт за $20 (~9 900 ₸)</button>' +
          '</div>' +
        '</div>'
      );
    } else {
      render(mainContent);
    }

    animateGauges();
    bindRiskCtas();

    const unlockBtn = document.getElementById('unlock-paywall-btn');
    if (unlockBtn) unlockBtn.addEventListener('click', function () { openPaywallModal(sessionId); });

    const unlockOverlayBtn = document.getElementById('unlock-paywall-overlay-btn');
    if (unlockOverlayBtn) unlockOverlayBtn.addEventListener('click', function () { openPaywallModal(sessionId); });

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
