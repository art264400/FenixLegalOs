// Fenix SLS — Premium Pitch Deck / Presentation in Typst
#set page(
  paper: "presentation-16-9",
  margin: (x: 1.8cm, top: 1.1cm, bottom: 0.9cm),
  fill: rgb("#060A13")
)

#set text(
  font: ("Georgia", "Times New Roman"),
  size: 11.5pt,
  fill: rgb("#E2E8F0"),
  lang: "ru"
)

#let sans = ("Segoe UI", "Arial")
#let c-card = rgb("#0D1628")
#let c-card-border = rgb("#1E2D4A")
#let c-gold = rgb("#E5C07B")
#let c-blue = rgb("#38BDF8")
#let c-red = rgb("#F87171")
#let c-yellow = rgb("#FBBF24")
#let c-green = rgb("#34D399")
#let c-muted = rgb("#94A3B8")
#let c-white = rgb("#FFFFFF")

#let logo-hdr = image("logo_cropped.png", width: 2.4cm)

// --- Header Helper ---
#let slide-header(category: "") = {
  grid(
    columns: (1fr, auto),
    align: (left + horizon, right + horizon),
    logo-hdr,
    if category != "" {
      text(font: sans, size: 9.5pt, fill: c-muted, tracking: 1.5pt, weight: "medium", upper(category))
    }
  )
  v(0.35cm)
}

// --- Footer Helper ---
#let slide-footer() = {
  place(bottom + left, dy: -0.05cm)[
    #line(length: 100%, stroke: 0.5pt + rgb("#1E2D4A"))
    #v(3pt)
    #grid(
      columns: (1fr, 1fr),
      align: (left, right),
      text(font: sans, size: 8pt, fill: rgb("#64748B"), tracking: 0.8pt)[FENIX SLS · SMART LEGAL SCREENING],
      text(font: sans, size: 8pt, fill: rgb("#64748B"))[by Fenix Law]
    )
  ]
}

// ==========================================
// SLIDE 1: COVER
// ==========================================
#align(center + horizon)[
  #image("logo_cropped.png", width: 5.2cm)
  #v(0.35cm)
  #text(size: 29pt, weight: "bold", fill: c-white)[Что такое FENIX SLS?]
  #v(0.15cm)
  #text(font: sans, size: 15pt, fill: c-blue, weight: "medium")[Smart Legal Screening для технологических компаний]
  #v(0.35cm)
  
  #line(length: 14%, stroke: 1.2pt + c-blue)
  #v(0.35cm)
  
  #grid(
    columns: (1fr, 1fr, 1fr),
    gutter: 0.8cm,
    align: center,
    [
      #text(font: sans, size: 12pt, weight: "bold", fill: c-white)[Увидеть реальные риски]\
      #text(font: sans, size: 9pt, fill: c-muted)[до прихода инвестора]
    ],
    [
      #text(font: sans, size: 12pt, weight: "bold", fill: c-gold)[Понять приоритеты]\
      #text(font: sans, size: 9pt, fill: c-muted)[что критично сейчас]
    ],
    [
      #text(font: sans, size: 12pt, weight: "bold", fill: c-green)[Подготовиться к росту]\
      #text(font: sans, size: 9pt, fill: c-muted)[и венчурным сделкам]
    ]
  )
]
#slide-footer()

#pagebreak()

// ==========================================
// SLIDE 2: PROBLEMS
// ==========================================
#slide-header(category: "Проблематика")

#text(size: 22pt, weight: "bold", fill: c-white)[Юридические проблемы редко появляются внезапно]
#v(0.08cm)
#text(font: sans, size: 11.5pt, fill: c-muted)[Обычно они долго остаются незаметными в повседневной операционной рутине.]
#v(0.4cm)

#grid(
  columns: (1fr, 1fr),
  column-gutter: 0.8cm,
  row-gutter: 0.35cm,
  rect(width: 100%, fill: c-card, stroke: 1pt + c-card-border, radius: 6pt, inset: (x: 12pt, y: 11pt))[
    #grid(
      columns: (auto, 1fr),
      gutter: 10pt,
      align: horizon,
      circle(radius: 5pt, stroke: 2pt + c-blue),
      [
        #text(font: sans, size: 12pt, weight: "bold", fill: c-white)[Продукт не на компании]\
        #v(1pt)
        #text(font: sans, size: 9.5pt, fill: c-muted)[Код и IP оформлены на разработчиков или третьих лиц]
      ]
    )
  ],
  rect(width: 100%, fill: c-card, stroke: 1pt + c-card-border, radius: 6pt, inset: (x: 12pt, y: 11pt))[
    #grid(
      columns: (auto, 1fr),
      gutter: 10pt,
      align: horizon,
      circle(radius: 5pt, stroke: 2pt + c-red),
      [
        #text(font: sans, size: 12pt, weight: "bold", fill: c-white)[Со-основатель ушел]\
        #v(1pt)
        #text(font: sans, size: 9.5pt, fill: c-muted)[Отсутствие вестинга блокирует компанию при тупике (Deadlock)]
      ]
    )
  ],
  rect(width: 100%, fill: c-card, stroke: 1pt + c-card-border, radius: 6pt, inset: (x: 12pt, y: 11pt))[
    #grid(
      columns: (auto, 1fr),
      gutter: 10pt,
      align: horizon,
      circle(radius: 5pt, stroke: 2pt + c-yellow),
      [
        #text(font: sans, size: 12pt, weight: "bold", fill: c-white)[Пришел инвестор]\
        #v(1pt)
        #text(font: sans, size: 9.5pt, fill: c-muted)[Due Diligence вскрывает пробелы в Cap Table и решениях]
      ]
    )
  ],
  rect(width: 100%, fill: c-card, stroke: 1pt + c-card-border, radius: 6pt, inset: (x: 12pt, y: 11pt))[
    #grid(
      columns: (auto, 1fr),
      gutter: 10pt,
      align: horizon,
      circle(radius: 5pt, stroke: 2pt + c-green),
      [
        #text(font: sans, size: 12pt, weight: "bold", fill: c-white)[Компания начала масштабироваться]\
        #v(1pt)
        #text(font: sans, size: 9.5pt, fill: c-muted)[Рост выручки многократно увеличивает скрытые риски]
      ]
    )
  ]
)
#slide-footer()

#pagebreak()

// ==========================================
// SLIDE 3: HOW IT WORKS
// ==========================================
#slide-header(category: "Как работает система")

#text(size: 21pt, weight: "bold", fill: c-white)[FENIX SLS — первичная диагностика вашей компании]
#v(0.08cm)
#text(font: sans, size: 11.5pt, fill: c-muted)[Ответьте на несколько понятных вопросов и увидите юридические слабые места вашего бизнеса.]
#v(0.4cm)

#grid(
  columns: (1fr, 24pt, 1.25fr, 24pt, 1fr),
  align: horizon,
  rect(width: 100%, fill: c-card, stroke: 1pt + c-card-border, radius: 6pt, inset: 12pt)[
    #text(font: sans, size: 9pt, fill: c-blue, weight: "bold")[ШАГ 1]\
    #v(2pt)
    #text(size: 13pt, weight: "bold", fill: c-white)[Ваши ответы]\
    #v(2pt)
    #text(font: sans, size: 9pt, fill: c-muted)[10 минут без юристов и документов]
  ],
  align(center)[#text(size: 16pt, fill: c-blue)[→]],
  rect(width: 100%, fill: rgb("#0E223D"), stroke: 1.5pt + c-blue, radius: 6pt, inset: 12pt)[
    #text(font: sans, size: 9pt, fill: c-gold, weight: "bold")[ШАГ 2 · АЛГОРИТМ]\
    #v(2pt)
    #text(size: 13pt, weight: "bold", fill: c-white)[SLS анализирует связи]\
    #v(2pt)
    #text(font: sans, size: 9pt, fill: rgb("#CBD5E1"))[Синтез всей конструкции и вероятность споров]
  ],
  align(center)[#text(size: 16pt, fill: c-blue)[→]],
  rect(width: 100%, fill: c-card, stroke: 1pt + c-card-border, radius: 6pt, inset: 12pt)[
    #text(font: sans, size: 9pt, fill: c-green, weight: "bold")[ШАГ 3]\
    #v(2pt)
    #text(size: 13pt, weight: "bold", fill: c-white)[Картина рисков]\
    #v(2pt)
    #text(font: sans, size: 9pt, fill: c-muted)[Legal Score, Action Plan и разбор]
  ]
)

#v(0.35cm)

#rect(
  width: 100%,
  fill: rgb("#0A1424"),
  stroke: (left: 3pt + c-blue),
  inset: (x: 14pt, y: 10pt)
)[
  #grid(
    columns: (auto, 1fr),
    gutter: 12pt,
    align: horizon,
    text(size: 16pt)[⚖️],
    [
      #text(font: sans, size: 10.5pt, fill: c-white, weight: "semibold")[
        Система анализирует сочетание факторов и выявляет юридические риски на основе практики Fenix Law.
      ]\
      #text(font: sans, size: 9pt, fill: c-muted)[
        Не шаблонный ответ · Не обычный чек-лист · Не generic ИИ-юрист
      ]
    ]
  )
]
#slide-footer()

#pagebreak()

// ==========================================
// SLIDE 4: 8 DOMAINS
// ==========================================
#slide-header(category: "Зоны проверки")

#text(size: 21pt, weight: "bold", fill: c-white)[Проверка 8 ключевых зон бизнеса]
#v(0.08cm)
#text(font: sans, size: 11pt, fill: c-muted)[Скрининг покрывает всю юридическую конфигурацию технологической компании.]
#v(0.3cm)

#let domain-item(num, title, subtitle, color) = [
  #rect(
    width: 100%,
    fill: c-card,
    stroke: 1pt + c-card-border,
    radius: 5pt,
    inset: (x: 10pt, y: 8pt)
  )[
    #grid(
      columns: (auto, 1fr),
      gutter: 8pt,
      align: horizon,
      text(font: sans, size: 11pt, weight: "bold", fill: color)[#num],
      [
        #text(font: sans, size: 10.5pt, weight: "bold", fill: c-white)[#title]\
        #text(font: sans, size: 8.5pt, fill: c-muted)[#subtitle]
      ]
    )
  ]
]

#grid(
  columns: (1fr, 1fr),
  column-gutter: 0.6cm,
  row-gutter: 0.25cm,
  domain-item("01", "Основатели", "Доли · Роли · Решения · Вестинг", c-blue),
  domain-item("02", "Корпоративная структура", "Владение · Полномочия · Юрисдикция · Cap Table", c-blue),
  domain-item("03", "Интеллектуальная собственность", "Код · Разработки · Бренд · Исключительные права", c-gold),
  domain-item("04", "Команда и подрядчики", "Сотрудники · Подрядчики · NDA / NCA · Доступы", c-gold),
  domain-item("05", "Продукт и пользователи", "Публичная оферта · Оплаты · Ответственность", c-yellow),
  domain-item("06", "Данные и ИИ", "Персональные данные · Сбор · Модели ИИ · Обработка", c-yellow),
  domain-item("07", "Коммерческие договоры", "Клиенты · Партнеры · Обязательства · Лимиты", c-green),
  domain-item("08", "Готовность к инвестициям", "Раунд · SAFE / КИС · Документы к сделке", c-green)
)
#slide-footer()

#pagebreak()

// ==========================================
// SLIDE 5: RESULT / SCORE
// ==========================================
#slide-header(category: "Результат диагностики")

#text(size: 21pt, weight: "bold", fill: c-white)[Вы получаете не просто балл]
#v(0.08cm)
#text(font: sans, size: 11pt, fill: c-muted)[А структурированную карту юридической устойчивости и приоритизированный Action Plan.]
#v(0.35cm)

#grid(
  columns: (1.15fr, 1fr),
  gutter: 0.8cm,
  [
    #rect(
      width: 100%,
      fill: c-card,
      stroke: 1.5pt + rgb("#1E2D4A"),
      radius: 8pt,
      inset: 14pt
    )[
      #text(font: sans, size: 10pt, fill: c-muted, tracking: 1pt, weight: "bold")[LEGAL SCORE]
      #v(3pt)
      #grid(
        columns: (auto, 1fr),
        gutter: 12pt,
        align: horizon,
        [
          #text(font: sans, size: 34pt, weight: "bold", fill: c-white)[68] #text(font: sans, size: 15pt, fill: c-muted)[\/ 100]
        ],
        [
          #text(font: sans, size: 9.5pt, fill: c-yellow, weight: "bold")[ЮРИДИЧЕСКАЯ УСТОЙЧИВОСТЬ]\
          #v(2pt)
          #text(font: sans, size: 8.5pt, fill: c-muted)[Требуется точечная доработка до раунда]
        ]
      )
      #v(8pt)
      #grid(
        columns: (1fr, 1fr, 1fr),
        gutter: 6pt,
        [
          #rect(fill: rgb("#2A1215"), radius: 4pt, inset: 6pt, width: 100%)[
            #align(center)[
              #text(font: sans, size: 13pt, weight: "bold", fill: c-red)[1]\
              #text(font: sans, size: 8pt, fill: c-muted)[Критический]
            ]
          ]
        ],
        [
          #rect(fill: rgb("#2D220E"), radius: 4pt, inset: 6pt, width: 100%)[
            #align(center)[
              #text(font: sans, size: 13pt, weight: "bold", fill: c-yellow)[4]\
              #text(font: sans, size: 8pt, fill: c-muted)[Внимания]
            ]
          ]
        ],
        [
          #rect(fill: rgb("#0D281E"), radius: 4pt, inset: 6pt, width: 100%)[
            #align(center)[
              #text(font: sans, size: 13pt, weight: "bold", fill: c-green)[3]\
              #text(font: sans, size: 8pt, fill: c-muted)[Сильные зоны]
            ]
          ]
        ]
      )
    ]
  ],
  [
    #rect(
      width: 100%,
      fill: c-card,
      stroke: 1pt + c-card-border,
      radius: 8pt,
      inset: 12pt
    )[
      #text(font: sans, size: 10.5pt, weight: "bold", fill: c-gold)[ПЕРСОНАЛЬНЫЙ ОТЧЕТ:]
      #v(6pt)
      #list(
        marker: text(fill: c-blue)[•],
        [#text(font: sans, size: 9.5pt, fill: c-white)[Главные юридические риски и их вес]],
        [#text(font: sans, size: 9.5pt, fill: c-white)[Почему они важны для инвестора]],
        [#text(font: sans, size: 9.5pt, fill: c-white)[Сильные стороны структуры бизнеса]],
        [#text(font: sans, size: 9.5pt, fill: c-white)[Что исправить прямо сейчас]],
        [#text(font: sans, size: 9.5pt, fill: c-white)[Что сделать до инвестиционного раунда]]
      )
    ]
  ]
)
#slide-footer()

#pagebreak()

// ==========================================
// SLIDE 6: COMBINATORIAL LOGIC
// ==========================================
#slide-header(category: "Логика работы системы")

#text(size: 21pt, weight: "bold", fill: c-white)[FENIX SLS анализирует сочетание факторов]
#v(0.08cm)
#text(font: sans, size: 11pt, fill: c-muted)[Система видит не изолированные ответы, а целостную правовую конструкцию.]
#v(0.35cm)

#grid(
  columns: (1.05fr, 1.35fr),
  gutter: 0.8cm,
  [
    #rect(fill: c-card, stroke: 1pt + c-card-border, radius: 5pt, inset: (x: 10pt, y: 7pt), width: 100%)[
      #text(font: sans, size: 9.5pt, fill: c-muted)[Со-основателя:] #text(font: sans, size: 10.5pt, weight: "bold", fill: c-white)[2]
    ]
    #v(0.18cm)
    #rect(fill: c-card, stroke: 1pt + c-card-border, radius: 5pt, inset: (x: 10pt, y: 7pt), width: 100%)[
      #text(font: sans, size: 9.5pt, fill: c-muted)[Разделение долей:] #text(font: sans, size: 10.5pt, weight: "bold", fill: c-white)[50 / 50]
    ]
    #v(0.18cm)
    #rect(fill: c-card, stroke: 1pt + c-card-border, radius: 5pt, inset: (x: 10pt, y: 7pt), width: 100%)[
      #text(font: sans, size: 9.5pt, fill: c-muted)[Ключевые решения:] #text(font: sans, size: 10.5pt, weight: "bold", fill: c-white)[Совместно]
    ]
    #v(0.18cm)
    #rect(fill: c-card, stroke: 1pt + c-card-border, radius: 5pt, inset: (x: 10pt, y: 7pt), width: 100%)[
      #text(font: sans, size: 9.5pt, fill: c-muted)[Выход из тупика:] #text(font: sans, size: 10.5pt, weight: "bold", fill: c-red)[Отсутствует]
    ]
  ],
  [
    #rect(
      width: 100%,
      fill: rgb("#2D1216"),
      stroke: 1.2pt + c-red,
      radius: 6pt,
      inset: 12pt
    )[
      #text(font: sans, size: 9.5pt, fill: c-red, weight: "bold")[🔴 КРИТИЧЕСКИЙ ВЫВОД: РИСК DEADLOCK]
      #v(4pt)
      #text(size: 11.5pt, weight: "bold", fill: c-white)[
        Компания может оказаться неспособной принять ключевое решение при конфликте со-основателей.
      ]
    ]
    #v(0.3cm)
    #rect(
      width: 100%,
      fill: rgb("#0A1424"),
      stroke: 1pt + rgb("#1E3A5F"),
      radius: 6pt,
      inset: 10pt
    )[
      #text(font: sans, size: 9.5pt, fill: c-blue, weight: "semibold")[
        FENIX SLS не показывает четыре плохих ответа.\
        Он распознает одну юридическую конфигурацию.
      ]
    ]
  ]
)
#slide-footer()

#pagebreak()

// ==========================================
// SLIDE 7: TRUST / CALL TO ACTION
// ==========================================
#slide-header(category: "Доверие и экспертиза")

#text(size: 21pt, weight: "bold", fill: c-white)[Юридическая экспертиза, превращенная в систему]
#v(0.25cm)

#grid(
  columns: (1.35fr, 1fr),
  gutter: 0.8cm,
  align: horizon,
  [
    #text(font: sans, size: 10.5pt, fill: rgb("#CBD5E1"))[
      Методология и алгоритм *FENIX SLS* созданы на базе реальной юридической практики и методик бутиковой юридической фирмы *FENIX LAW*, которая работает с технологическими компаниями, инвестиционными сделками и понимает весь юридический контур проблем компаний.
    ]
    #v(0.4cm)
    #rect(
      width: 100%,
      fill: rgb("#111E33"),
      stroke: 1.5pt + c-blue,
      radius: 6pt,
      inset: 12pt
    )[
      #text(font: sans, size: 11pt, weight: "bold", fill: c-white)[
        Проверьте юридически слабые места своей компании раньше, чем их увидит инвестор, партнер или случится конфликт.
      ]
    ]
  ],
  [
    #align(center)[
      #image("logo_cropped.png", width: 4.8cm)
      #v(0.2cm)
      #text(font: sans, size: 11pt, weight: "bold", fill: c-gold)[FENIX LEGAL OS]\
      #text(font: sans, size: 8.5pt, fill: c-muted)[fenixlaw.kz · Smart Legal Screening]
    ]
  ]
)
#slide-footer()
