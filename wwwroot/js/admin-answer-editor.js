/* Admin answer corrections: draft, preview and explicit publication. */
(function (root) {
  'use strict';
  const jurisdictions = [
    ['kz', 'Казахстан'], ['aifc', 'МФЦА'], ['us', 'США'], ['uae', 'ОАЭ'],
    ['uk', 'Великобритания'], ['cy', 'Кипр'], ['sg', 'Сингапур'], ['eu', 'ЕС'],
    ['kg', 'Кыргызстан'], ['uz', 'Узбекистан'], ['other', 'Другая страна'],
  ];
  const statuses = { draft: 'Черновик', generating: 'Формируется', ready: 'Готов к публикации',
    published: 'Опубликован', failed: 'Ошибка формирования', original: 'Исходный отчёт',
    superseded: 'Предыдущая редакция', discarded: 'Черновик удалён' };
  function esc(value) {
    return String(value == null ? '' : value).replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }
  function clone(value) { return value == null ? value : JSON.parse(JSON.stringify(value)); }
  function stable(value) {
    if (Array.isArray(value)) return value.map(stable);
    if (value && typeof value === 'object') return Object.keys(value).sort().reduce(function (out, key) {
      out[key] = stable(value[key]); return out;
    }, {});
    return value;
  }
  function same(a, b) { return JSON.stringify(stable(a)) === JSON.stringify(stable(b)); }
  function hasAnswer(value) {
    if (value == null || typeof value === 'string' && value.trim() === '') return false;
    if (Array.isArray(value)) return value.length > 0;
    return typeof value !== 'object' || Object.keys(value).length > 0;
  }
  function toggleMultiple(current, selected, options) {
    const values = Array.isArray(current) ? current.slice() : hasAnswer(current) ? [String(current)] : [];
    const option = options.find(function (o) { return o.id === selected; });
    if (values.includes(selected)) return values.filter(function (id) { return id !== selected; });
    if (option && (option.exclusive || option.id === 'none')) return [selected];
    return values.filter(function (id) {
      const old = options.find(function (o) { return o.id === id; });
      return id !== 'none' && !(old && old.exclusive);
    }).concat(selected);
  }
  function answerText(question, value) {
    if (!hasAnswer(value)) return 'Нет ответа';
    const label = function (id) {
      const option = (question.options || []).find(function (o) { return String(o.id) === String(id); });
      return option ? option.label : String(id);
    };
    if (question.type === 'equity_inputs' && value && typeof value === 'object' && !Array.isArray(value))
      return Object.keys(value).map(function (key, i) { return 'Основатель ' + (i + 1) + ': ' + value[key] + '%'; }).join('; ');
    if (question.type === 'entity_builder' && Array.isArray(value)) return value.map(function (entity, i) {
      const jurisdiction = jurisdictions.find(function (j) { return j[0] === entity.jurisdiction; });
      return 'Компания ' + (i + 2) + ': ' + (jurisdiction ? jurisdiction[1] : entity.jurisdiction || 'Страна не указана') +
        ' — ' + (Array.isArray(entity.roles) && entity.roles.length ? entity.roles.map(label).join(', ') : 'Роли не указаны');
    }).join('; ');
    return Array.isArray(value) ? value.map(label).join('; ') : label(value);
  }
  function pdfUrl(sessionId, revisionId) {
    return '/api/admin/sessions/' + encodeURIComponent(sessionId) + '/answer-revisions/' + encodeURIComponent(revisionId) + '/pdf';
  }
  function dateText(value) { return value ? new Date(value).toLocaleString('ru-RU') : '—'; }

  async function open(el, leadId, callbacks) {
    callbacks = callbacks || {};
    let state, answers = {}, savedAnswers = {}, reason = '', savedReason = '';
    let busy = false, busyAction = '', notice = '', noticeKind = '', closed = false, modalClose = null;
    const collapsedSections = new Set();
    const beforeUnload = function (event) { if (dirty() && !closed) { event.preventDefault(); event.returnValue = ''; } };
    root.addEventListener('beforeunload', beforeUnload);
    const active = function () { return !closed && el.isConnected; };
    const dirty = function () { return !same(answers, savedAnswers) || reason !== savedReason; };
    const questions = function () { return (state.sections || []).flatMap(function (s) { return s.questions || []; }); };
    const missing = function () { return questions().filter(function (q) { return !hasAnswer(answers[q.id]); }); };
    const url = function (suffix) { return '/api/admin/sessions/' + encodeURIComponent(state.sessionId) + suffix; };
    const body = function () { return { revisionId: state.draft ? state.draft.id : null,
      version: state.draft ? state.draft.version : 0, baseRevisionId: state.publishedRevisionId || null, answers: answers, reason: reason }; };
    function accept(data) {
      state = data; answers = clone(data.answers || {}); savedAnswers = clone(answers);
      reason = data.draft ? data.draft.reason || '' : ''; savedReason = reason;
    }
    function close() { closed = true; root.removeEventListener('beforeunload', beforeUnload); if (modalClose) modalClose(); }
    async function request(method, path, payload) {
      const response = await fetch(path, { method: method, credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json' }, body: payload ? JSON.stringify(payload) : undefined });
      const data = await response.json().catch(function () { return {}; });
      if (response.status === 401) {
        close(); if (callbacks.onUnauthorized) callbacks.onUnauthorized();
        throw new Error('Время входа администратора истекло. Войдите снова.');
      }
      if (!response.ok) {
        const error = new Error(data.message || (response.status === 409
          ? 'Редакция изменилась. Обновите список перед продолжением.'
          : 'Не удалось выполнить действие. Попробуйте ещё раз.'));
        error.status = response.status; throw error;
      }
      return data;
    }
    function statusHtml() {
      const generating = state && state.draft && state.draft.status === 'generating';
      if (busy || generating) {
        const title = busyAction === 'recalculate' || generating ? 'Формируем новую редакцию отчёта'
          : busyAction === 'publish' ? 'Публикуем исправленную редакцию'
          : busyAction === 'save' ? 'Сохраняем черновик' : 'Обновляем список';
        return '<div class="answer-editor-status is-busy" role="status"><span class="answer-editor-spinner" aria-hidden="true"></span><div><strong>' + title + '</strong>' +
          (busyAction === 'recalculate' || generating ? '<p>Пересчитываем результаты и готовим пояснения и PDF. Обычно это занимает около 2 минут.</p>' : '') + '</div></div>';
      }
      return notice ? '<div class="answer-editor-status ' + esc(noticeKind) + '" role="' + (noticeKind === 'is-error' ? 'alert' : 'status') + '">' + esc(notice) + '</div>' : '';
    }
    function render() {
      if (!active() || !state) return;
      const qs = questions(), changed = qs.filter(function (q) { return !same(answers[q.id], (state.originalAnswers || {})[q.id]); });
      const draft = state.draft, required = missing();
      const generating = draft && draft.status === 'generating';
      const locked = busy || generating;
      const ready = draft && draft.status === 'ready' && draft.pdfAvailable && !dirty();
      const disabled = locked ? ' disabled' : '';
      let html = '<section class="answer-editor" aria-labelledby="answer-editor-title">' +
        '<div class="answer-editor-heading"><div><button type="button" class="btn-ghost answer-editor-back" data-action="back"' + disabled + '>← Карточка отчёта</button>' +
        '<h2 id="answer-editor-title">Исправление ответов</h2><p>' + esc(state.companyName || 'Отчёт проекта') + '</p></div>' +
        '<div class="answer-editor-published"><span>Действующий отчёт</span><strong>' + (state.publishedRevisionNumber ? 'Редакция ' + state.publishedRevisionNumber : 'Исходная версия') + '</strong></div></div>' +
        '<div class="answer-editor-intro">Измените ответы и проверьте новую редакцию перед публикацией. Пользователю доступен действующий отчёт, пока вы не опубликуете исправления.</div>' +
        '<div class="answer-editor-topline"><div class="answer-editor-counters"><span><strong>' + qs.length + '</strong> вопросов</span><span><strong>' + changed.length + '</strong> изменений</span><span class="' +
        (required.length ? 'answer-editor-missing-count' : '') + '"><strong>' + required.length + '</strong> нужно заполнить</span></div>' +
        '<button type="button" class="btn-ghost" data-action="reload"' + (busy ? ' disabled' : '') + '>Обновить список</button></div><div id="answer-editor-status">' + statusHtml() + '</div>' +
        '<div class="field answer-editor-reason"><label for="answer-editor-reason">Причина исправления</label><textarea id="answer-editor-reason" rows="2" maxlength="2000"' + disabled +
        ' placeholder="Например: уточнили срок инвестиционного раунда после разговора с клиентом">' + esc(reason) + '</textarea><small>Сохраняется в истории. Укажите причину перед пересчётом отчёта.</small></div>';
      html += (state.sections || []).map(function (section) {
        const rows = (section.questions || []).map(function (q) {
          const value = answers[q.id], original = (state.originalAnswers || {})[q.id];
          const changedAnswer = !same(value, original), needsAnswer = !hasAnswer(value);
          return '<article class="answer-editor-row' + (changedAnswer ? ' is-changed' : '') + (needsAnswer ? ' is-missing' : '') + '">' +
            '<div class="answer-editor-question"><span class="answer-editor-question-id">' + esc(q.id) + '</span><h4>' + esc(q.question) + '</h4>' +
            (needsAnswer ? '<span class="answer-editor-tag is-required">Нужно ответить</span>' : changedAnswer ? '<span class="answer-editor-tag">Изменено</span>' : '') + '</div>' +
            '<div class="answer-editor-answer">' + (changedAnswer
              ? '<div class="answer-editor-old"><span>Было</span>' + esc(answerText(q, original)) + '</div><div class="answer-editor-new"><span>Стало</span>' + esc(answerText(q, value)) + '</div>'
              : '<span class="answer-editor-answer-caption">Текущий ответ</span><div>' + esc(answerText(q, value)) + '</div>') +
            '</div><button type="button" class="btn-ghost answer-editor-change" data-edit="' + esc(q.id) + '"' + disabled + '>' + (needsAnswer ? 'Ответить' : 'Изменить') + '</button></article>';
        }).join('');
        return '<details class="answer-editor-section" data-section="' + esc(section.id) + '"' + (collapsedSections.has(section.id) ? '' : ' open') + '><summary><span>' + esc(section.order) + '. ' + esc(section.title) + '</span><span>' + (section.questions || []).length +
          ' вопросов</span></summary><div>' + rows + '</div></details>';
      }).join('');
      html += '<div class="answer-editor-toolbar"><div><strong id="answer-editor-draft-label">' + (dirty() ? 'Есть несохранённые изменения' : draft ? statuses[draft.status] || draft.status : 'Черновик ещё не создан') +
        '</strong><p>Баллы и PDF обновятся после пересчёта.</p></div><div class="answer-editor-toolbar-actions"><button type="button" class="btn-ghost" data-action="save"' + disabled +
        '>Сохранить черновик</button><button type="button" class="btn" data-action="recalculate"' + (locked || required.length ? ' disabled' : '') + '>Пересчитать отчёт</button>' +
        (ready ? '<a class="btn-ghost answer-editor-preview-link" href="' + pdfUrl(state.sessionId, draft.id) +
          '" target="_blank" rel="noopener">Посмотреть PDF</a><button type="button" class="btn answer-editor-publish" data-action="publish">Опубликовать редакцию</button>' : '') + '</div></div>';
      if (draft && !generating) html += '<div class="answer-editor-discard"><button type="button" class="btn-ghost" data-action="discard"' + (busy ? ' disabled' : '') + '>Удалить черновик</button><span>Действующий отчёт и история опубликованных редакций сохранятся.</span></div>';
      if (draft && draft.error && !notice) html += '<div class="answer-editor-status is-error" role="alert">' + esc(draft.error) + '</div>';
      if (ready && draft.result) html += '<div class="answer-editor-result"><div><span>Новая редакция</span><strong>' + esc(draft.result.overall) +
        '<small> / 100</small></strong></div><p>Выявлено рисков: <strong>' + (draft.result.risks || []).length + '</strong>. Проверьте PDF перед публикацией.</p></div>';
      html += '<div class="answer-editor-history"><h3>История редакций</h3>' + (state.revisions || []).map(function (revision) {
        return '<div class="answer-editor-history-row"><div><strong>' + (revision.number ? 'Редакция ' + revision.number : 'Исходный отчёт') + '</strong><span>' +
          esc(statuses[revision.status] || revision.status) + ' · ' + esc(dateText(revision.publishedAt || revision.createdAt)) + '</span>' +
          (revision.reason ? '<p>' + esc(revision.reason) + '</p>' : '') + '</div>' + (revision.pdfAvailable
            ? '<a href="' + pdfUrl(state.sessionId, revision.id) + '" target="_blank" rel="noopener">Открыть PDF ↗</a>' : '') + '</div>';
      }).join('') + '</div></section><div id="answer-editor-modal"></div>';
      el.innerHTML = html;
      el.querySelectorAll('[data-section]').forEach(function (section) { section.addEventListener('toggle', function () {
        if (section.open) collapsedSections.delete(section.getAttribute('data-section')); else collapsedSections.add(section.getAttribute('data-section'));
      }); });
      el.querySelectorAll('[data-edit]').forEach(function (button) { button.addEventListener('click', function () { editQuestion(button.getAttribute('data-edit')); }); });
      el.querySelectorAll('[data-action]').forEach(function (button) { button.addEventListener('click', function () { runAction(button.getAttribute('data-action')); }); });
      el.querySelector('#answer-editor-reason').addEventListener('input', function (event) {
        reason = event.target.value;
        el.querySelector('#answer-editor-draft-label').textContent = dirty() ? 'Есть несохранённые изменения' : draft ? statuses[draft.status] || draft.status : 'Черновик ещё не создан';
        const publish = el.querySelector('[data-action="publish"]');
        if (publish) publish.disabled = dirty();
      });
    }
    async function saveDraft() {
      const data = await request('PUT', url('/answer-editor'), body());
      if (active()) accept(data);
    }
    async function runAction(action) {
      if (busy) return;
      if (state.draft && state.draft.status === 'generating' && !['back', 'reload'].includes(action)) return;
      if (action === 'discard' && (!state.draft || !root.confirm('Удалить черновик исправлений? Действующий отчёт и опубликованные редакции сохранятся.'))) return;
      if (action === 'back') {
        if (dirty() && !root.confirm('У вас есть несохранённые изменения. Выйти из редактора?')) return;
        close(); if (callbacks.onBack) callbacks.onBack(); return;
      }
      if (action === 'reload' && dirty() && !root.confirm('Обновление заменит несохранённые изменения сохранённым черновиком. Продолжить?')) return;
      if (action === 'recalculate' && reason.trim().length < 3) {
        notice = 'Укажите причину исправления, чтобы пересчитать отчёт.'; noticeKind = 'is-error'; render();
        el.querySelector('#answer-editor-reason').focus(); return;
      }
      if (action === 'recalculate' && missing().length) { notice = 'Заполните вопросы с пометкой «Нужно ответить».'; noticeKind = 'is-error'; render(); return; }
      if (action === 'publish' && (dirty() || !state.draft || state.draft.status !== 'ready' || !state.draft.pdfAvailable)) return;
      busy = true; busyAction = action; notice = ''; render();
      try {
        if (action === 'reload') { accept(await request('GET', url('/answer-editor'))); notice = 'Список обновлён.'; }
        if (action === 'save') { await saveDraft(); notice = 'Черновик сохранён. Действующий отчёт пока прежний.'; }
        if (action === 'recalculate') {
          if (dirty() || !state.draft) await saveDraft();
          if (!active()) return;
          const data = await request('POST', url('/answer-revisions/' + encodeURIComponent(state.draft.id) + '/recalculate'), { version: state.draft.version });
          if (!active()) return;
          accept(data);
          if (!state.draft || state.draft.status !== 'ready') throw new Error(state.draft && state.draft.error || 'PDF пока не готов. Проверьте состояние черновика.');
          notice = 'Новая редакция готова. Посмотрите PDF, затем опубликуйте исправления.';
        }
        if (action === 'discard') {
          accept(await request('POST', url('/answer-revisions/' + encodeURIComponent(state.draft.id) + '/discard'), { version: state.draft.version }));
          notice = 'Черновик удалён.';
        }
        if (action === 'publish') {
          const data = await request('POST', url('/answer-revisions/' + encodeURIComponent(state.draft.id) + '/publish'), { version: state.draft.version });
          if (!active()) return;
          accept(data); notice = 'Исправленная редакция опубликована и доступна пользователю.';
        }
        noticeKind = 'is-success';
      } catch (error) {
        if (active() && action === 'recalculate' && !dirty()) {
          try { const latest = await request('GET', url('/answer-editor')); if (active()) accept(latest); } catch (refreshError) { /* Keep the original error. */ }
        }
        if (active()) { notice = error.message; noticeKind = 'is-error'; }
      }
      finally { busy = false; render(); }
    }

    function editQuestion(questionId) {
      const question = questions().find(function (q) { return q.id === questionId; });
      if (!question || busy || state.draft && state.draft.status === 'generating') return;
      const opener = el.querySelector('[data-edit="' + questionId + '"]'), container = el.querySelector('#answer-editor-modal');
      let value = clone(answers[questionId]), applying = false;
      if (question.type === 'multiple' && typeof value === 'string') value = [value];
      const overflow = document.body.style.overflow;
      function closeModal() {
        document.removeEventListener('keydown', onKey); document.body.style.overflow = overflow;
        container.innerHTML = ''; modalClose = null; if (opener && opener.isConnected) opener.focus({ preventScroll: true });
      }
      function onKey(event) {
        if (event.key === 'Escape' && !applying) { event.preventDefault(); closeModal(); }
        if (event.key !== 'Tab') return;
        const controls = Array.from(container.querySelectorAll('button:not([disabled]),input:not([disabled]),textarea:not([disabled]),select:not([disabled])'));
        const first = controls[0], last = controls[controls.length - 1];
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus({ preventScroll: true }); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus({ preventScroll: true }); }
      }
      modalClose = closeModal; document.body.style.overflow = 'hidden'; document.addEventListener('keydown', onKey);
      function editorHtml() {
        if (question.type === 'equity_inputs') {
          if (!value || typeof value !== 'object' || Array.isArray(value)) {
            const count = answers['FND-C01'] === '3' ? 3 : answers['FND-C01'] === '4plus' ? 4 : 2; value = {};
            for (let i = 1; i <= count; i++) value['founder_' + i] = '';
          }
          return '<div class="answer-editor-equity">' + Object.keys(value).map(function (key, i) {
            return '<div class="answer-editor-equity-row"><label for="share-' + i + '">Основатель ' + (i + 1) + '</label><div><input id="share-' + i +
              '" type="number" min="0.01" max="100" step="any" data-share="' + esc(key) + '" value="' + esc(value[key]) + '"><span>%</span><button type="button" class="btn-ghost" data-remove-share="' +
              esc(key) + '" aria-label="Удалить основателя ' + (i + 1) + '"' + (Object.keys(value).length <= 2 ? ' disabled' : '') + '>×</button></div></div>';
          }).join('') + '<div class="answer-editor-equity-actions"><button type="button" class="btn-ghost" data-add-share>+ Добавить основателя</button><button type="button" class="btn-ghost" data-equal-shares>Разделить поровну</button></div><p class="answer-editor-share-total">Сумма долей: <strong data-share-total>' +
            Object.values(value).reduce(function (sum, v) { return sum + (Number(v) || 0); }, 0) + '%</strong></p></div>';
        }
        if (question.type === 'entity_builder') {
          if (!Array.isArray(value) || !value.length) value = [{ index: 2, jurisdiction: '', roles: [] }];
          return '<div class="answer-editor-entities">' + value.map(function (entity, i) {
            return '<div class="answer-editor-entity"><div class="answer-editor-entity-heading"><strong>Компания ' + (i + 2) + '</strong><button type="button" class="btn-ghost" data-remove-entity="' + i +
              '"' + (value.length <= 1 ? ' disabled' : '') + '>Удалить</button></div><div class="field"><label for="entity-jur-' + i + '">Страна регистрации</label><select id="entity-jur-' + i +
              '" data-jurisdiction="' + i + '"><option value="">Выберите страну</option>' + jurisdictions.map(function (j) { return '<option value="' + j[0] + '"' +
              (entity.jurisdiction === j[0] ? ' selected' : '') + '>' + esc(j[1]) + '</option>'; }).join('') + '</select></div><fieldset><legend>Для чего используется компания</legend>' +
              (question.options || []).map(function (option) { return '<label class="answer-editor-role"><input type="checkbox" data-entity-role="' + i + '" value="' + esc(option.id) +
                '"' + (Array.isArray(entity.roles) && entity.roles.includes(option.id) ? ' checked' : '') + '><span>' + esc(option.label) + '</span></label>'; }).join('') + '</fieldset></div>';
          }).join('') + '<button type="button" class="btn-ghost" data-add-entity>+ Добавить компанию</button></div>';
        }
        if (question.type === 'text' && !(question.options || []).length) return '<div class="field"><label for="answer-editor-text">Ответ</label><textarea id="answer-editor-text" rows="4" maxlength="10000">' + esc(value || '') + '</textarea></div>';
        if (question.type === 'number' && !(question.options || []).length) return '<div class="field"><label for="answer-editor-number">Ответ</label><input id="answer-editor-number" type="number" step="any" value="' + esc(value == null ? '' : value) + '"></div>';
        return '<div class="q-options answer-editor-options">' + (question.options || []).map(function (option) {
          const selected = question.type === 'multiple' ? Array.isArray(value) && value.includes(option.id) : String(value) === String(option.id);
          return '<button type="button" class="q-option' + (selected ? ' selected' : '') + '" aria-pressed="' + selected + '" data-option="' + esc(option.id) + '">' + esc(option.label) + '</button>';
        }).join('') + '</div>';
      }
      function draw() {
        container.innerHTML = '<div class="admin-modal-backdrop answer-editor-modal-backdrop"><section class="admin-modal-box answer-editor-modal" role="dialog" aria-modal="true" aria-labelledby="answer-editor-modal-title"><div class="answer-editor-modal-heading"><span>' +
          esc(question.id) + '</span><button type="button" class="btn-ghost" data-close aria-label="Закрыть редактор">×</button></div><h3 id="answer-editor-modal-title">' + esc(question.question) + '</h3>' +
          (question.explanation ? '<p class="answer-editor-explanation">' + esc(question.explanation) + '</p>' : '') + '<div class="answer-editor-current"><span>В действующем отчёте</span><p>' +
          esc(answerText(question, (state.originalAnswers || {})[question.id])) + '</p></div>' + editorHtml() +
          '<div class="answer-editor-modal-error" role="alert" hidden></div><div class="modal-actions"><button type="button" class="btn-ghost" data-close>Отмена</button><button type="button" class="btn" data-apply>Применить изменение</button></div></section></div>';
        container.querySelectorAll('[data-close]').forEach(function (button) { button.addEventListener('click', closeModal); });
        container.querySelectorAll('[data-option]').forEach(function (button) { button.addEventListener('click', function () {
          const selected = button.getAttribute('data-option'); value = question.type === 'multiple' ? toggleMultiple(value, selected, question.options || []) : selected;
          container.querySelectorAll('[data-option]').forEach(function (other) {
            const picked = question.type === 'multiple' ? value.includes(other.getAttribute('data-option')) : value === other.getAttribute('data-option');
            other.classList.toggle('selected', picked); other.setAttribute('aria-pressed', String(picked));
          });
        }); });
        container.querySelectorAll('[data-share]').forEach(function (input) { input.addEventListener('input', function () {
          value[input.getAttribute('data-share')] = input.value === '' ? '' : Number(input.value);
          container.querySelector('[data-share-total]').textContent = (Math.round(Object.values(value).reduce(function (sum, v) { return sum + (Number(v) || 0); }, 0) * 10000) / 10000) + '%';
        }); });
        container.querySelectorAll('[data-remove-share]').forEach(function (button) { button.addEventListener('click', function () {
          delete value[button.getAttribute('data-remove-share')];
          value = Object.values(value).reduce(function (out, share, i) { out['founder_' + (i + 1)] = share; return out; }, {}); draw();
        }); });
        const addShare = container.querySelector('[data-add-share]'), equal = container.querySelector('[data-equal-shares]');
        if (addShare) addShare.addEventListener('click', function () { value['founder_' + (Object.keys(value).length + 1)] = ''; draw(); });
        if (equal) equal.addEventListener('click', function () {
          const keys = Object.keys(value), share = Math.floor(10000 / keys.length) / 100;
          keys.forEach(function (key, i) { value[key] = i === keys.length - 1 ? Math.round((100 - share * i) * 100) / 100 : share; }); draw();
        });
        container.querySelectorAll('[data-jurisdiction]').forEach(function (select) { select.addEventListener('change', function () { value[Number(select.getAttribute('data-jurisdiction'))].jurisdiction = select.value; }); });
        container.querySelectorAll('[data-entity-role]').forEach(function (input) { input.addEventListener('change', function () {
          const i = input.getAttribute('data-entity-role'), entity = value[Number(i)]; entity.roles = toggleMultiple(entity.roles, input.value, question.options || []);
          container.querySelectorAll('[data-entity-role="' + i + '"]').forEach(function (other) { other.checked = entity.roles.includes(other.value); });
        }); });
        container.querySelectorAll('[data-remove-entity]').forEach(function (button) { button.addEventListener('click', function () { value.splice(Number(button.getAttribute('data-remove-entity')), 1); draw(); }); });
        const addEntity = container.querySelector('[data-add-entity]');
        if (addEntity) addEntity.addEventListener('click', function () { value.push({ index: value.length + 2, jurisdiction: '', roles: [] }); draw(); });
        const text = container.querySelector('#answer-editor-text'), number = container.querySelector('#answer-editor-number');
        if (text) text.addEventListener('input', function () { value = text.value; });
        if (number) number.addEventListener('input', function () { value = number.value === '' ? '' : Number(number.value); });
        container.querySelector('[data-apply]').addEventListener('click', apply);
      }
      async function apply() {
        if (applying) return;
        const errorBox = container.querySelector('.answer-editor-modal-error'); let error = '';
        if (!hasAnswer(value)) error = 'Выберите или заполните ответ.';
        else if (question.type === 'equity_inputs' && Object.values(value).some(function (v) { return !Number.isFinite(v) || v <= 0 || v > 100; })) error = 'Каждая доля должна быть больше 0 и не больше 100%.';
        else if (question.type === 'entity_builder') {
          if (value.some(function (e) { return !e.jurisdiction || !Array.isArray(e.roles) || !e.roles.length; })) error = 'Для каждой компании выберите страну и хотя бы одну роль.';
          value.forEach(function (entity, i) { entity.index = i + 2; });
        } else if (question.type === 'number' && !Number.isFinite(value)) error = 'Введите корректное число.';
        if (error) { errorBox.textContent = error; errorBox.hidden = false; return; }
        applying = true;
        container.querySelectorAll('button,input,select,textarea').forEach(function (control) { control.disabled = true; });
        container.querySelector('[data-apply]').textContent = 'Проверяем вопросы…';
        try {
          const candidate = clone(answers); candidate[question.id] = clone(value);
          const payload = body(); payload.answers = candidate;
          const preview = await request('POST', url('/answer-editor/preview'), payload);
          if (!active()) { closeModal(); return; }
          state = preview; answers = clone(preview.answers || {}); closeModal();
          notice = 'Изменение применено. Сохраните черновик или продолжите исправления.'; noticeKind = 'is-success'; render();
          const button = el.querySelector('[data-edit="' + question.id + '"]'); if (button) button.focus();
        } catch (error) {
          if (!active()) { closeModal(); return; }
          errorBox.textContent = error.message; errorBox.hidden = false;
          container.querySelectorAll('button,input,select,textarea').forEach(function (control) { control.disabled = false; });
          container.querySelector('[data-apply]').textContent = 'Применить изменение';
        } finally { applying = false; }
      }
      draw();
      const first = container.querySelector('[data-option],input,textarea,select,[data-close]'); if (first) first.focus({ preventScroll: true });
    }
    el.innerHTML = '<div class="admin-loading" role="status">Загрузка ответов отчёта…</div>';
    try {
      const data = await request('GET', '/api/admin/leads/' + encodeURIComponent(leadId) + '/answer-editor');
      if (active()) { accept(data); render(); }
    } catch (error) {
      if (active()) {
        el.innerHTML = '<div class="answer-editor-status is-error" role="alert">' + esc(error.message) + '</div><button type="button" class="btn-ghost" id="answer-editor-error-back">← Карточка отчёта</button>';
        el.querySelector('#answer-editor-error-back').addEventListener('click', function () { close(); if (callbacks.onBack) callbacks.onBack(); });
      }
    }
    return { close: close, hasUnsavedChanges: dirty };
  }
  const api = { open: open, answerText: answerText, toggleMultiple: toggleMultiple, same: same, hasAnswer: hasAnswer };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.AdminAnswerEditor = api;
})(typeof window !== 'undefined' ? window : globalThis);
