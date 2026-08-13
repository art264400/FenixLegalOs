# 📋 Fenix Legal OS — Product & Technical Backlog

Сюда заносятся все побочные архитектурные или продуктовые замечания, обнаруженные во время выполнения точечных задач, чтобы не раздувать текущий scope.

---

## 📌 Список Наблюдений и Нераспределенных Задач (Backlog Items)

| Priority | Группа | Описание проблемы / идеи | Оценка времени | Затронутый агент |
|---|---|---|---|---|
| **P1** | **PDF Layout** | **Orphan Section Headings (Page Breaks)**: Заголовки «Реестр рисков» и «Дорожная карта» остаются нанизу страниц 1 и 2 без карточек под ними. Добавить `#v(0pt)` или `show heading: set block(below: ...)` для защиты от разрыва страницы перед блоком. | ~1 час | `04_pdf_report_designer.md` |
| **P1** | **Legal Content** | **Расширение `DataBank.Risks`**: Добавить исчерпывающие описания для остальных 30+ опциональных рисков (сейчас зафиксировано 5 базовых референсных определений в `DataBank.Risks`). | ~2 часа | `05_legal_product_expert.md` |
| **P2** | **SMALL** | **Admin CRM Search & Filter**: Быстрый поиск лидов по имени/email/мессенджеру и фильтр по Lead Heat Index в админке `/#/admin`. | ~1.5 часа | `02_frontend_engineer.md` |
| **P2** | **SMALL** | **PDF Executive Callout Box**: Акцентный информационный блок на 1-й странице Typst PDF с выжимкой Top-3 срочных действий. | ~1 час | `04_pdf_report_designer.md` |
| **P3** | **SMALL** | **Questionnaire Keyboard Navigation**: Горячие клавиши `1-4` на десктопе для мгновенного выбора варианта ответа. | ~1 час | `02_frontend_engineer.md` |
| **P4** | **MEDIUM** | **Kaspi QR Modal Image**: Автогенерация нативного QR-кода Kaspi Pay в модале оплаты для мгновенной оплаты телефоном. | ~4 часа | `03_backend_engineer.md` |
| **P5** | **MEDIUM** | **Session Magic Link**: Возможность возобновить пройденную диагностику по уникальной ссылке на email. | ~3 часа | `03_backend_engineer.md` |
| **P6** | **LATER** | **Telegram Bot Instant Alerts**: Мгновенные уведомления о новых оплаченных отчётах ($20) в Telegram-чат Наримана Исанова. | Post-MVP | `03_backend_engineer.md` |
