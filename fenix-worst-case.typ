
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
      #text(size: 8.5pt, fill: rgb("#8E9BAE"))[Проект: Phoenix Test Startup] \
      #text(size: 8.5pt, fill: rgb("#8E9BAE"))[Дата: 13.08.2026] \
      #text(size: 8pt, fill: rgb("#59C2FF"))[Официальный отчёт Fenix Law]
    ]
  ]
)

#v(8pt)
#line(length: 100%, stroke: 1.5pt + rgb("#243042"))
#v(12pt)


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
      #text(size: 38pt, weight: "bold", fill: rgb("#FF5964"))[12#text(size: 20pt, fill: rgb("#8E9BAE"))[/100]]
    ],
    [
      #text(size: 16pt, weight: "bold", fill: rgb("#FFFFFF"))[Структурные вопросы] \
      #v(4pt)
      #text(size: 9.5pt, fill: rgb("#A0AEC0"))[Юридическая основа бизнеса пока сформирована фрагментарно.]
      #v(8pt)
      #grid(
        columns: (auto, auto, auto),
        gutter: 12pt,
        [#rect(fill: rgb("#3D1A24"), inset: (x: 8pt, y: 4pt), radius: 4pt)[#text(size: 8.5pt, weight: "bold", fill: rgb("#FF5964"))[🔴 3 Критических]]],
        [#rect(fill: rgb("#3D2B1A"), inset: (x: 8pt, y: 4pt), radius: 4pt)[#text(size: 8.5pt, weight: "bold", fill: rgb("#FF9F43"))[🟠 2 Высоких]]],
        [#rect(fill: rgb("#38321A"), inset: (x: 8pt, y: 4pt), radius: 4pt)[#text(size: 8.5pt, weight: "bold", fill: rgb("#F5A623"))[🟡 0 Умеренных]]]
      )
    ]
  )
]


#v(16pt)
#text(size: 14pt, weight: "bold", fill: rgb("#FFFFFF"))[📊 Оценка по 8 ключевым разделам]
#v(6pt)
#grid(
  columns: (1fr, 1fr),
  gutter: 10pt,


  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[1. Сооснователи] ], [#text(fill: rgb("#FF5964"), weight: "bold")[7%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 7%, height: 4pt, fill: rgb("#FF5964"), radius: 2pt)]
    #v(2pt)
    #text(size: 8pt, fill: rgb("#8E9BAE"))[Статус: Критический риск]
  ],

  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[2. Корпоративная структура] ], [#text(fill: rgb("#FF5964"), weight: "bold")[27%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 27%, height: 4pt, fill: rgb("#FF5964"), radius: 2pt)]
    #v(2pt)
    #text(size: 8pt, fill: rgb("#8E9BAE"))[Статус: Критический риск]
  ],

  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[3. Интеллектуальная собственность] ], [#text(fill: rgb("#FF5964"), weight: "bold")[12%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 12%, height: 4pt, fill: rgb("#FF5964"), radius: 2pt)]
    #v(2pt)
    #text(size: 8pt, fill: rgb("#8E9BAE"))[Статус: Критический риск]
  ],

  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[4. Команда и подрядчики] ], [#text(fill: rgb("#FF5964"), weight: "bold")[11%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 11%, height: 4pt, fill: rgb("#FF5964"), radius: 2pt)]
    #v(2pt)
    #text(size: 8pt, fill: rgb("#8E9BAE"))[Статус: Критический риск]
  ],

  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[5. Продукт и пользователи] ], [#text(fill: rgb("#FF5964"), weight: "bold")[6%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 6%, height: 4pt, fill: rgb("#FF5964"), radius: 2pt)]
    #v(2pt)
    #text(size: 8pt, fill: rgb("#8E9BAE"))[Статус: Критический риск]
  ],

  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[6. Данные, privacy и AI] ], [#text(fill: rgb("#FF5964"), weight: "bold")[12%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 12%, height: 4pt, fill: rgb("#FF5964"), radius: 2pt)]
    #v(2pt)
    #text(size: 8pt, fill: rgb("#8E9BAE"))[Статус: Критический риск]
  ],

  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[7. Коммерческие договоры] ], [#text(fill: rgb("#FF5964"), weight: "bold")[6%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 6%, height: 4pt, fill: rgb("#FF5964"), radius: 2pt)]
    #v(2pt)
    #text(size: 8pt, fill: rgb("#8E9BAE"))[Статус: Критический риск]
  ],

  rect(width: 100%, fill: rgb("#141B26"), stroke: 0.5pt + rgb("#243042"), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: "bold")[8. Инвестиционная готовность] ], [#text(fill: rgb("#FF5964"), weight: "bold")[17%]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb("#243042"), radius: 2pt)[#rect(width: 17%, height: 4pt, fill: rgb("#FF5964"), radius: 2pt)]
    #v(2pt)
    #text(size: 8pt, fill: rgb("#8E9BAE"))[Статус: Критический риск]
  ],
)


#v(16pt)
#text(size: 14pt, weight: "bold", fill: rgb("#FFFFFF"))[🔴 Полный реестр выявленных рисков и рекомендаций]
#v(6pt)


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
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Что обнаружено:* Доли фаундеров зафиксированы только устно или не распределены.] \
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Почему это критично:* Устная договорённость работает, пока всё хорошо. При первом разногласии юридически считается, что компании нет или доли не принадлежат никому.] \
  #v(4pt)
  #text(size: 9.5pt, weight: "medium", fill: rgb("#59C2FF"))[*Рекомендация по исправлению:* Закрепить доли документально в Корпоративном соглашении / SHA.]
]
#v(10pt)


#rect(
  width: 100%,
  fill: rgb("#141B26"),
  stroke: (left: 4pt + rgb("#FF5964"), rest: 0.5pt + rgb("#243042")),
  radius: (right: 6pt),
  inset: 14pt,
)[
  #grid(
    columns: (1fr, auto),
    [#text(weight: "bold", size: 11pt, fill: rgb("#FFFFFF"))[2. Официальные доли рассылаются с фактическими]],
    [#rect(fill: rgb("#3D1A24"), inset: (x: 6pt, y: 2pt), radius: 3pt)[#text(size: 8pt, weight: "bold", fill: rgb("#FF5964"))[ТРЕБУЕТСЯ ЮРИСТ]]]
  )
  #v(6pt)
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Что обнаружено:* Зарегистрированные доли в юрлице отличатся от устных договоренностей.] \
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Почему это критично:* При инвестиционном Due Diligence инвестор проверяет только официальный устав.] \
  #v(4pt)
  #text(size: 9.5pt, weight: "medium", fill: rgb("#59C2FF"))[*Рекомендация по исправлению:* Привести официальную структуру в соответствие с фактом.]
]
#v(10pt)


#rect(
  width: 100%,
  fill: rgb("#141B26"),
  stroke: (left: 4pt + rgb("#FF5964"), rest: 0.5pt + rgb("#243042")),
  radius: (right: 6pt),
  inset: 14pt,
)[
  #grid(
    columns: (1fr, auto),
    [#text(weight: "bold", size: 11pt, fill: rgb("#FFFFFF"))[3. Отсутствуют договоры с разработчиками]],
    [#rect(fill: rgb("#3D1A24"), inset: (x: 6pt, y: 2pt), radius: 3pt)[#text(size: 8pt, weight: "bold", fill: rgb("#FF5964"))[ТРЕБУЕТСЯ ЮРИСТ]]]
  )
  #v(6pt)
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Что обнаружено:* Продукт создавался внешними специалистами без письменных договоров.] \
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Почему это критично:* По закону авторские права принадлежат создателю. Код юридически принадлежит фрилансерам.] \
  #v(4pt)
  #text(size: 9.5pt, weight: "medium", fill: rgb("#59C2FF"))[*Рекомендация по исправлению:* Подписать договоры уступки прав (IP Assignment) прошлым числом.]
]
#v(10pt)


#rect(
  width: 100%,
  fill: rgb("#141B26"),
  stroke: (left: 4pt + rgb("#FF9F43"), rest: 0.5pt + rgb("#243042")),
  radius: (right: 6pt),
  inset: 14pt,
)[
  #grid(
    columns: (1fr, auto),
    [#text(weight: "bold", size: 11pt, fill: rgb("#FFFFFF"))[4. Отсутствует соглашение сооснователей]],
    [#rect(fill: rgb("#3D2B1A"), inset: (x: 6pt, y: 2pt), radius: 3pt)[#text(size: 8pt, weight: "bold", fill: rgb("#FF9F43"))[ТРЕБУЕТСЯ ЮРИСТ]]]
  )
  #v(6pt)
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Что обнаружено:* Между фаундерами нет письменных правил распределения долей, ролей и выходя.] \
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Почему это критично:* При уходе сооснователя возникает 'мёртвый капитал' — человек забирает долю и не работает.] \
  #v(4pt)
  #text(size: 9.5pt, weight: "medium", fill: rgb("#59C2FF"))[*Рекомендация по исправлению:* Разработать Founder Agreement с правилами принятия решений и Vesting.]
]
#v(10pt)


#rect(
  width: 100%,
  fill: rgb("#141B26"),
  stroke: (left: 4pt + rgb("#FF9F43"), rest: 0.5pt + rgb("#243042")),
  radius: (right: 6pt),
  inset: 14pt,
)[
  #grid(
    columns: (1fr, auto),
    [#text(weight: "bold", size: 11pt, fill: rgb("#FFFFFF"))[5. Отсутствует Privacy Policy]],
    [#rect(fill: rgb("#3D2B1A"), inset: (x: 6pt, y: 2pt), radius: 3pt)[#text(size: 8pt, weight: "bold", fill: rgb("#FF9F43"))[ЖЕЛАТЕЛЬНО С ЮРИСТОМ]]]
  )
  #v(6pt)
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Что обнаружено:* Продукт собирает персональные данные, но не имеет Политики конфиденциальности.] \
  #text(size: 9pt, fill: rgb("#A0AEC0"))[*Почему это критично:* Штрафы регуляторов и блокировка приложении в App Store / Google Play.] \
  #v(4pt)
  #text(size: 9.5pt, weight: "medium", fill: rgb("#59C2FF"))[*Рекомендация по исправлению:* Подготовить Privacy Policy под реальные потоки данных.]
]
#v(10pt)


#v(16pt)
#text(size: 14pt, weight: "bold", fill: rgb("#FFFFFF"))[🗺 Пошаговая дорожная карта устранения рисков (Roadmap)]
#v(6pt)
#rect(
  width: 100%,
  fill: rgb("#141B26"),
  stroke: 0.5pt + rgb("#243042"),
  radius: 8pt,
  inset: 14pt,
)[

  #text(weight: "bold", fill: rgb("#FF5964"))[🚨 1. Первоочередные задачи (Сделать прямо сейчас)] \
  #v(4pt)
  - Закрепить доли документально в Корпоративном соглашении / SHA. \
  - Привести официальную структуру в соответствие с фактом. \
  - Подписать договоры уступки прав (IP Assignment) прошлым числом. \
  #v(10pt)
  #text(weight: "bold", fill: rgb("#FF9F43"))[📅 2. В течение 30 дней (Закрепление основы)] \
  #v(4pt)
  - Разработать Founder Agreement с правилами принятия решений и Vesting. \
  - Подготовить Privacy Policy под реальные потоки данных. \
  #v(10pt)
  #text(weight: "bold", fill: rgb("#59C2FF"))[🚀 3. Перед инвестиционным раундом (Data Room)] \
  #v(4pt)
  - Сформировать юридический Data Room (Cap Table, ИП/ТОО/МФЦА структуры, лицензии). \
  - Провести финальный Due Diligence с венчурным юристом. \
]


#v(16pt)
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
      #v(4pt)
      #text(size: 9.5pt, fill: rgb("#A0AEC0"))[
        Венчурный юрист *Нариман Исанов* проведёт 60-минутную индивидуальную сессию по результатам вашей диагностики, поможет составить договоры фаундеров, уступить права на IT-продукт и подготовить стартап к инвестициям.
      ]
    ],
    [
      #align(right + horizon)[
        #rect(fill: rgb("#59C2FF"), radius: 6pt, inset: (x: 14pt, y: 8pt))[
          #text(weight: "bold", fill: rgb("#0B0F16"), size: 9.5pt)[Записаться на разбор]
        ]
      ]
    ]
  )
]

