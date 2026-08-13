# 🤖 Fenix Legal OS AI Agent Team Architecture

Архитектурный регламент и операционная модель AI-команды разработки и развития платформы **Fenix Legal OS**.

## 🏗 Текущий технологический стек и контекст проекта

- **Backend**: C# / .NET 8, ASP.NET Core Minimal APIs
- **ORM & DB**: Dapper ORM + SQLite (`fenix.db`)
- **Frontend**: Vanilla JavaScript (ES6+), Vanilla CSS3 (Dark mode `#0B0F16`, accent `#59C2FF`), Single Page Application (SPA) на Hash Router
- **PDF Engine**: Векторный верстальщик Typst CLI (v0.15+, синтаксис `context`)
- **Диагностический движок**: 58 профильных вопросов, 8 зон юридических рисков (Risk Domains), алгоритм вычисления Fenix Legal Score (0–100)
- **Монетизация**: Paywall ($20 / ~9 900 ₸ за отчёт и PDF, $150 / ~75 000 ₸ за консультацию)
- **CRM Администратора**: Пароль `fenix2026`, вычисление Lead Heat Index, аудит-лог событий (`Events`)

---

## 📜 10 Общих правил для всех AI-агентов

1. **Проект уже существует и работает**: Категорически запрещено переписывать проект с нуля или менять основной стек без явного решения `00_product_lead.md`.
2. **Анализ перед действием**: Перед внесением любых изменений агент ОБЯЗАН изучить исходные файлы своего слоя (`Program.cs`, `DataBank.cs`, `app.js`, `style.css`, `report_template.typ` и т.д.).
3. **Принцип минимально эффективного изменения**: Делать точечные, изолированные изменения. Не занимать overengineering-ом и преждевременными абстракциями.
4. **Сохранение стека**: Не предлагать миграции на React/Vue/Svelte, ORM Entity Framework, MediatR или микросервисы.
5. **Цепочка взаимодействия**: Все UI-задачи обязательно проходят через `01_ui_ux_designer.md`, юр-логика — через `05_legal_product_expert.md`, а финальная приёмка — через `06_qa_security_engineer.md`.
6. **Тестирование и валидация**: Ни одна задача не считается выполненной без компиляции/сборки и проверки QA-сценариев.
7. **Изоляция изменений**: Не править несвязанные части проекта в рамках одной текущей задачи.
8. **Фиксация техдолга**: Если обнаружена архитектурная проблема, не мешающая текущей задаче — записать её в Бэклог / Техдолг, но не начинать спонтанный рефакторинг.
9. **Формат ошибок QA**: Оформлять все выявленные баги по строгому шаблону (Severity, Scenario, Steps, Expected, Actual, Fix).
10. **Приоритеты MVP (P0–P6)**:
    ```text
    P0 — Broken flows (Критические баги, сломанный квиз, упавший сервер)
    P1 — UX / Paid report quality (Качество и полнота PDF и веб-отчёта)
    P2 — Paywall / Conversion (Конверсия модалок оплаты и пэйвола)
    P3 — Payments (Интеграция Kaspi QR, карт, верификация транзакций)
    P4 — Security (Авторизация админки, XSS, защита от обхода оплаты)
    P5 — Deployment (Docker, HTTPS, переменные окружения)
    P6 — Scaling / Architecture (Масштабирование, рефакторинг)
    ```

---

## 🔄 Стандартные Маршруты Делегирования (Workflows)

```text
UI/UX Задача:
Product Lead -> UI/UX Designer -> Frontend Engineer -> QA/Security Engineer

Backend / API Задача:
Product Lead -> Backend Engineer -> QA/Security Engineer

PDF Отчёт Задача:
Product Lead -> PDF Designer -> Legal Product Expert -> Backend Engineer -> QA/Security Engineer

Оплата / Безопасность:
Product Lead -> Backend Engineer -> QA/Security Engineer

Деплоймент / Инфраструктура:
Product Lead -> DevOps Engineer -> QA/Security Engineer
```

---

## 👥 Состав Команды Агентов

* [`00_product_lead.md`](00_product_lead.md) — Главный оркестратор команды, декомпозиция задач и контрольscope.
* [`01_ui_ux_designer.md`](01_ui_ux_designer.md) — Senior UI/UX Designer (Премиальный LegalTech/FinTech стиль).
* [`02_frontend_engineer.md`](02_frontend_engineer.md) — Senior Frontend Engineer (Vanilla JS, CSS, Hash Router).
* [`03_backend_engineer.md`](03_backend_engineer.md) — Senior .NET Backend Engineer (C#, Minimal APIs, Dapper, SQLite).
* [`04_pdf_report_designer.md`](04_pdf_report_designer.md) — Специалист по Typst PDF и информационному дизайну.
* [`05_legal_product_expert.md`](05_legal_product_expert.md) — Legal Product Expert (Юридическая продуктовая логика).
* [`06_qa_security_engineer.md`](06_qa_security_engineer.md) — QA + Security Engineer (Поиск уязвимостей и тестирование).
* [`07_devops_engineer.md`](07_devops_engineer.md) — DevOps Engineer (Production, Docker, переменные окружения).
