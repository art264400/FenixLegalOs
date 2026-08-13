// =============================================================================
// FENIX LEGAL OS — Typst Official Legal Score Report Template
// =============================================================================

#set page(
  paper: "a4",
  fill: rgb("#0B0F16"),
  margin: (x: 1.8cm, top: 2.2cm, bottom: 2.2cm),
  header: context {
    if counter(page).get().first() > 1 [
      #grid(
        columns: (1fr, auto),
        align(left)[
          #text(size: 8.5pt, fill: rgb("#59C2FF"), weight: "bold")[FENIX LAW]
          #text(size: 8.5pt, fill: rgb("#6C7A8E"))[ · Юридический отчет диагностики]
        ],
        align(right)[
          #text(size: 8.5pt, fill: rgb("#6C7A8E"))[Fenix Legal Score OS]
        ]
      )
      #v(-4pt)
      #line(length: 100%, stroke: 0.5pt + rgb("#243042"))
    ]
  },
  footer: context [
    #line(length: 100%, stroke: 0.5pt + rgb("#243042"))
    #v(2pt)
    #grid(
      columns: (1fr, auto),
      align(left)[
        #text(size: 8pt, fill: rgb("#6C7A8E"))[Конфиденциально · Подготовлено Fenix Legal OS]
      ],
      align(right)[
        #text(size: 8pt, fill: rgb("#8E9BAE"))[Страница #counter(page).display() из #counter(page).final().first()]
      ]
    )
  ]
)

#set text(font: ("Liberation Sans", "DejaVu Sans", "Roboto"), fill: rgb("#E6EDF8"), size: 10pt)
#set par(justify: true, leading: 0.6em)

// -----------------------------------------------------------------------------
// 1. БРЕНДИРОВАННЫЙ ЗАГОЛОВОК
// -----------------------------------------------------------------------------

#grid(
  columns: (52pt, 1fr, auto),
  gutter: 14pt,
  align: (left + horizon, left + horizon, right + horizon),
  image("logo.png", width: 48pt),
  [
    #text(size: 22pt, weight: "bold", fill: rgb("#FFFFFF"))[Fenix Law] \
    #v(-2pt)
    #text(size: 9.5pt, weight: "semibold", fill: rgb("#59C2FF"))[LEGAL TECH SMART SYSTEM · ЮРИДИЧЕСКАЯ ДИАГНОСТИКА]
  ],
  [
    #align(right)[
      #text(size: 8.5pt, fill: rgb("#8E9BAE"))[Дата: #datetime.today().display("[day].[month].[year]")] \
      #text(size: 8pt, fill: rgb("#59C2FF"))[Статус: Оплаченный отчёт]
    ]
  ]
)

#v(8pt)
#line(length: 100%, stroke: 1.5pt + rgb("#243042"))
#v(12pt)

// -----------------------------------------------------------------------------
// 2. HERO SCORE BANNER
// -----------------------------------------------------------------------------

#rect(
  width: 100%,
  fill: rgb("#141B26"),
  stroke: 1pt + rgb("#243042"),
  radius: 10pt,
  inset: 18pt,
)[
  #grid(
    columns: (120pt, 1fr),
    gutter: 18pt,
    align: (center + horizon, left + horizon),
    [
      #text(size: 9pt, weight: "bold", fill: rgb("#8E9BAE"))[LEGAL SCORE] \
      #v(2pt)
      #text(size: 38pt, weight: "bold", fill: rgb("#FF5964"))[64#text(size: 20pt, fill: rgb("#8E9BAE"))[/100]]
    ],
    [
      #text(size: 16pt, weight: "bold", fill: rgb("#FFFFFF"))[Есть вопросы, требующие внимания] \
      #v(4pt)
      #text(size: 9.5pt, fill: rgb("#A0AEC0"))[
        Юридическая основа стартапа сформирована частично. Выявлены ключевые риски в блоках «Сооснователи» и «Интеллектуальная собственность», которые требуют урегулирования.
      ]
      #v(8pt)
      #grid(
        columns: (auto, auto, auto),
        gutter: 12pt,
        [#rect(fill: rgb("#3D1A24"), inset: (x: 8pt, y: 4pt), radius: 4pt)[#text(size: 8.5pt, weight: "bold", fill: rgb("#FF5964"))[🔴 3 Критических]]],
        [#rect(fill: rgb("#3D2B1A"), inset: (x: 8pt, y: 4pt), radius: 4pt)[#text(size: 8.5pt, weight: "bold", fill: rgb("#FF9F43"))[🟠 7 Высоких]]],
        [#rect(fill: rgb("#38321A"), inset: (x: 8pt, y: 4pt), radius: 4pt)[#text(size: 8.5pt, weight: "bold", fill: rgb("#F5A623"))[🟡 5 Умеренных]]]
      )
    ]
  )
]

#v(16pt)

// -----------------------------------------------------------------------------
// 3. ЗДОРОВЬЕ БИЗНЕСА ПО 8 РАЗДЕЛАМ
// -----------------------------------------------------------------------------

#text(size: 14pt, weight: "bold", fill: rgb("#FFFFFF"))[📊 Оценка по 8 ключевым разделам]
#v(6pt)

#grid(
  columns: (1fr, 1fr),
  gutter: 10pt,
  
  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[1. Сооснователи] ], [#text(fill: rgb("#FF5964"), weight: "bold")[40%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 40%, height: 4pt, fill: rgb("#FF5964"), radius: 2pt)]
  ],
  
  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[2. Корпоративная структура] ], [#text(fill: rgb("#FF9F43"), weight: "bold")[65%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 65%, height: 4pt, fill: rgb("#FF9F43"), radius: 2pt)]
  ],

  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[3. Интеллектуальная собственность] ], [#text(fill: rgb("#FF5964"), weight: "bold")[35%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 35%, height: 4pt, fill: rgb("#FF5964"), radius: 2pt)]
  ],

  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[4. Команда и подрядчики] ], [#text(fill: rgb("#2ED573"), weight: "bold")[85%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 85%, height: 4pt, fill: rgb("#2ED573"), radius: 2pt)]
  ],
  
  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[5. Продукт и пользователи] ], [#text(fill: rgb("#FF9F43"), weight: "bold")[60%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 60%, height: 4pt, fill: rgb("#FF9F43"), radius: 2pt)]
  ],

  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[6. Данные, Privacy и AI] ], [#text(fill: rgb("#FF5964"), weight: "bold")[45%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 45%, height: 4pt, fill: rgb("#FF5964"), radius: 2pt)]
  ],

  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[7. Коммерческие договоры] ], [#text(fill: rgb("#2ED573"), weight: "bold")[90%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 90%, height: 4pt, fill: rgb("#2ED573"), radius: 2pt)]
  ],

  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[8. Инвестиционная готовность] ], [#text(fill: rgb("#FF9F43"), weight: "bold")[70%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 70%, height: 4pt, fill: rgb("#FF9F43"), radius: 2pt)]
  ]
)

#v(16pt)

// -----------------------------------------------------------------------------
// 4. ДЕТАЛЬНАЯ КАРТА РИСКОВ
// -----------------------------------------------------------------------------

#text(size: 14pt, weight: "bold", fill: rgb("#FFFFFF"))[🔴 Критические зоны риска]
#v(6pt)

// Карточка Риска 1
#rect(
  width: 100%,
  fill: rgb("#141B26"),
  stroke: (left: 4pt + rgb("#FF5964"), rest: 0.5pt + rgb("#243042")),
  radius: (right: 6pt),
  inset: 14pt,
)[
  #grid(
    columns: (1fr, auto),
    [#text(weight: "bold", size: 11pt, fill: rgb("#FFFFFF"))[1. Доли сооснователей не зафиксированы документально]],
    [#rect(fill: rgb("#3D1A24"), inset: (x: 6pt, y: 2pt), radius: 3pt)[#text(size: 8pt, weight: "bold", fill: rgb("#FF5964"))[ТРЕБУЕТСЯ ЮРИСТ]]]
  )
  #v(6pt)
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Что обнаружено:* Устная договорённость о долях без корпоративного соглашения.] \
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Почему это критично:* При уходе сооснователя возникает "мёртвый капитал" — человек забирает долю и не работает, а инвесторы отказываются финансировать проект.] \
  #v(4pt)
  #text(size: 9.5pt, weight: "medium", fill: rgb("#59C2FF"))[*Рекомендация юриста:* Разработать Founder Agreement с правилами Vesting и выкупа долей.]
]

#v(10pt)

// Карточка Риска 2
#rect(
  width: 100%,
  fill: rgb("#141B26"),
  stroke: (left: 4pt + rgb("#FF5964"), rest: 0.5pt + rgb("#243042")),
  radius: (right: 6pt),
  inset: 14pt,
)[
  #grid(
    columns: (1fr, auto),
    [#text(weight: "bold", size: 11pt, fill: rgb("#FFFFFF"))[2. Права на ключевой код не переданы от внешних разработчиков]],
    [#rect(fill: rgb("#3D1A24"), inset: (x: 6pt, y: 2pt), radius: 3pt)[#text(size: 8pt, weight: "bold", fill: rgb("#FF5964"))[ТРЕБУЕТСЯ ЮРИСТ]]]
  )
  #v(6pt)
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Что обнаружено:* Код создавался фрилансерами без подписания актов передачи прав (IP Assignment).] \
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Почему это критично:* По закону авторские права остаются у разработчика. Подрядчик может заблокировать работу компании или потребовать доплату.] \
  #v(4pt)
  #text(size: 9.5pt, weight: "medium", fill: rgb("#59C2FF"))[*Рекомендация юриста:* Подписать соглашения об уступке прав (IP Assignment) прошлым числом.]
]

#v(16pt)

// -----------------------------------------------------------------------------
// 5. ROADMAP УСТРАНЕНИЯ ПРОБЛЕМ
// -----------------------------------------------------------------------------

#text(size: 14pt, weight: "bold", fill: rgb("#FFFFFF"))[🗺 Дорожная карта действий (Roadmap)]
#v(6pt)

#rect(
  width: 100%,
  fill: rgb("#141B26"),
  stroke: 0.5pt + rgb("#243042"),
  radius: 8pt,
  inset: 14pt,
)[
  #text(weight: "bold", fill: rgb("#FF5964"))[🚨 1. Сделать прямо сейчас (Блокеры)]
  #v(4pt)
  - Подписать Founder Agreement с Vesting и правилами принятия решений.
  - Оформить IP Assignment со всеми внешними разработчиками и фрилансерами.

  #v(10pt)
  #text(weight: "bold", fill: rgb("#FF9F43"))[📅 2. В течение 30 дней (Основа)]
  - Разработать и разместить персонализированные Terms of Use и Privacy Policy.
  - Зафиксировать согласия пользователей на обработку персданных и передачу в AI-сервисы.

  #v(10pt)
  #text(weight: "bold", fill: rgb("#59C2FF"))[🚀 3. Перед инвестиционным раундом]
  - Сформировать готовый Data Room для инвесторов.
  - Привести официальный устав и cap table в 100% соответствие с фактом.
]

#v(20pt)

// -----------------------------------------------------------------------------
// 6. CONTACTS & CONSULTATION CTA
// -----------------------------------------------------------------------------

#rect(
  width: 100%,
  fill: rgb("#1C2433"),
  stroke: 1pt + rgb("#59C2FF"),
  radius: 10pt,
  inset: 16pt,
)[
  #grid(
    columns: (1fr, auto),
    gutter: 14pt,
    [
      #text(size: 13pt, weight: "bold", fill: rgb("#FFFFFF"))[Персональный разбор от Fenix Law] \
      #v(2pt)
      #text(size: 9.5pt, fill: rgb("#A0AEC0"))[
        Венчурный юрист *Нариман Исанов* проведёт 60-минутную индивидуальную сессию по результатам вашей диагностики и поможет подготовить необходимые документы.
      ]
    ],
    [
      #align(right + horizon)[
        #rect(fill: rgb("#59C2FF"), radius: 6pt, inset: (x: 14pt, y: 8pt))[
          #text(weight: "bold", fill: rgb("#0B0F16"), size: 9.5pt)[Записаться на сессию]
        ]
      ]
    ]
  )
]
