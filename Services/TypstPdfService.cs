using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Report;
using Microsoft.AspNetCore.Hosting;

namespace FenixLegalOs.Services;

public class TypstPdfService
{
    private readonly string _contentRootPath;
    private readonly AiReportService _aiReportService;
    private readonly FenixLegalOs.Repositories.SettingsRepository? _settings;

    public TypstPdfService(
        IWebHostEnvironment env,
        AiReportService aiReportService,
        FenixLegalOs.Repositories.SettingsRepository? settings = null)
    {
        _contentRootPath = env.ContentRootPath;
        _aiReportService = aiReportService;
        _settings = settings;
    }

    public async Task<byte[]?> GeneratePdfAsync(
        ScoreResult result,
        SharedFactStore facts,
        string sessionId,
        string? companyName = null)
    {
        var reportCtx = ReportEngine.AssembleReportContext(result, facts, sessionId, companyName);

        if (_settings != null)
        {
            var contacts = _settings.GetContacts();
            reportCtx.FenixLaw.Telegram = contacts.Telegram;
            reportCtx.FenixLaw.Website = contacts.Website;
            reportCtx.FenixLaw.Phone = contacts.Phone;
            reportCtx.FenixLaw.Email = contacts.Email;
        }

        try
        {
            var rawNarratives = await _aiReportService.GenerateReportNarrativesAsync(reportCtx);
            var sanitizedNarratives = ReportQualityGate.ValidateAndSanitize(rawNarratives, reportCtx);

            if (!string.IsNullOrWhiteSpace(sanitizedNarratives.ProjectProfileNarrative))
                reportCtx.Profile.ConfigurationNarrative = sanitizedNarratives.ProjectProfileNarrative;

            if (!string.IsNullOrWhiteSpace(sanitizedNarratives.ExecutiveConclusion))
                reportCtx.ExecutiveConclusion = sanitizedNarratives.ExecutiveConclusion;

            foreach (var top in reportCtx.TopFindings)
            {
                if (sanitizedNarratives.RootCauseSummaries.TryGetValue(top.RootCauseCode, out var sum) ||
                    sanitizedNarratives.RootCauseSummaries.TryGetValue(top.FindingCode, out sum))
                {
                    top.ShortSummary = sum;
                }
            }

            foreach (var focus in reportCtx.FocusModules)
            {
                if (sanitizedNarratives.ModuleNarratives.TryGetValue(focus.SectionId, out var mNarrative))
                {
                    if (!string.IsNullOrWhiteSpace(mNarrative.Summary)) focus.SubtitleNarrative = mNarrative.Summary;
                    if (!string.IsNullOrWhiteSpace(mNarrative.PracticalMeaning)) focus.PracticalMeaning = mNarrative.PracticalMeaning;

                    foreach (var f in focus.Findings)
                    {
                        if (mNarrative.FindingNarratives.TryGetValue(f.FindingCode, out var fNarrative))
                        {
                            if (!string.IsNullOrWhiteSpace(fNarrative.WhyFound)) f.WhyFound = fNarrative.WhyFound;
                            if (!string.IsNullOrWhiteSpace(fNarrative.WhyItMatters)) f.WhyItMatters = fNarrative.WhyItMatters;
                            if (!string.IsNullOrWhiteSpace(fNarrative.Recommendation)) f.Recommendation = fNarrative.Recommendation;
                        }
                    }
                }
            }

            foreach (var action in reportCtx.ActionPlan)
            {
                if (sanitizedNarratives.ActionNarratives.TryGetValue(action.ActionId, out var aNarrative))
                {
                    if (!string.IsNullOrWhiteSpace(aNarrative.WhyNow)) action.WhyNow = aNarrative.WhyNow;
                    if (!string.IsNullOrWhiteSpace(aNarrative.ExpectedResult)) action.ExpectedResult = aNarrative.ExpectedResult;
                }
            }

            if (!string.IsNullOrWhiteSpace(sanitizedNarratives.FenixLawRecommendation))
                reportCtx.FenixLaw.SummaryText = sanitizedNarratives.FenixLawRecommendation;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[AiReportService Exception] " + ex.Message);
        }

        var typstContent = BuildTypstMarkup(reportCtx);

        var tempTypFile = Path.Combine(_contentRootPath, $"report_{Guid.NewGuid():N}.typ");
        var tempPdfFile = Path.Combine(_contentRootPath, $"report_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempTypFile, typstContent, Encoding.UTF8);

            var typstExePath = Path.Combine(_contentRootPath, "typst.exe");
            if (!File.Exists(typstExePath))
            {
                var candidateInBase = Path.Combine(AppContext.BaseDirectory, "typst.exe");
                var candidateInSolution = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "typst.exe");
                if (File.Exists(candidateInBase)) typstExePath = candidateInBase;
                else if (File.Exists(candidateInSolution)) typstExePath = Path.GetFullPath(candidateInSolution);
                else typstExePath = "typst";
            }

            var psi = new ProcessStartInfo
            {
                FileName = typstExePath,
                Arguments = $"compile --root \"{_contentRootPath}\" \"{tempTypFile}\" \"{tempPdfFile}\"",
                WorkingDirectory = _contentRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var err = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && File.Exists(tempPdfFile))
                {
                    return await File.ReadAllBytesAsync(tempPdfFile);
                }
                else
                {
                    Console.WriteLine("[Typst Error] " + err);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Typst Exception] " + ex.Message);
        }
        finally
        {
            if (File.Exists(tempTypFile)) try { File.Delete(tempTypFile); } catch {}
            if (File.Exists(tempPdfFile)) try { File.Delete(tempPdfFile); } catch {}
        }

        return null;
    }

    public string BuildTypstMarkup(ReportContext ctx)
    {
        var sb = new StringBuilder();
        int secNum = 1;

        // 1. Global Document Setup & Premium Editorial Design System (Derived from FENIX SLS Presentation)
        sb.AppendLine(@"
#set document(title: ""FENIX SLS — Отчет первичного юридического скрининга"", author: ""Fenix Law"")
#set page(
  paper: ""a4"",
  fill: rgb(""#060A13""),
  margin: (x: 1.4cm, top: 1.4cm, bottom: 1.4cm),
  header: context [
    #grid(
      columns: (1fr, auto),
      align: (left + bottom, right + bottom),
      [
        #grid(
          columns: (auto, auto),
          gutter: 8pt,
          [#text(font: (""Georgia"", ""Times New Roman""), size: 9pt, fill: rgb(""#E5C07B""), weight: ""bold"")[FENIX SLS]],
          [#text(font: (""Segoe UI"", ""Arial""), size: 7.5pt, fill: rgb(""#64748B""), tracking: 1.2pt)[SMART LEGAL SCREENING]]
        )
      ],
      [
        #text(font: (""Segoe UI"", ""Arial""), size: 7.5pt, fill: rgb(""#94A3B8""))[Отчет: " + ctx.ReportNumber + @"  ·  " + ctx.GeneratedDate + (string.IsNullOrWhiteSpace(ctx.ProjectName) || ctx.ProjectName is "Проект" or "Стартап" ? @"  ·  " + EscapeTypst(ctx.ProjectStage) : @"  ·  " + EscapeTypst(ctx.ProjectName) + @" (" + EscapeTypst(ctx.ProjectStage) + @")") + @"]
      ]
    )
    #v(3pt)
    #line(length: 100%, stroke: 0.5pt + rgb(""#1E2D4A""))
  ],
  footer: context [
    #line(length: 100%, stroke: 0.5pt + rgb(""#1E2D4A""))
    #v(3pt)
    #grid(
      columns: (1fr, auto),
      align: (left, right),
      [
        #text(font: (""Segoe UI"", ""Arial""), size: 7.5pt, fill: rgb(""#64748B""), tracking: 0.8pt)[Конфиденциально · Fenix SLS · Smart Legal Screening by Fenix Law]
      ],
      [
        #text(font: (""Segoe UI"", ""Arial""), size: 7.5pt, fill: rgb(""#94A3B8""))[Страница #counter(page).display() из #counter(page).final().first()]
      ]
    )
  ]
)

#set text(font: (""Segoe UI"", ""Arial"", ""Liberation Sans""), size: 8.5pt, fill: rgb(""#E2E8F0""), lang: ""ru"")

#let serif = (""Georgia"", ""Times New Roman"")
#let sans = (""Segoe UI"", ""Arial"", ""Liberation Sans"")

#let card(body, fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), radius: 6pt, inset: 11pt) = {
  rect(
    width: 100%,
    fill: fill,
    stroke: 0.75pt + stroke,
    radius: radius,
    inset: inset,
    body
  )
}

#let section-header(num, title, category: """") = [
  #if category != """" [
    #text(font: sans, size: 7.5pt, fill: rgb(""#94A3B8""), tracking: 1.5pt, weight: ""medium"")[#upper(category)]
    #v(2pt)
  ]
  #grid(
    columns: (auto, 1fr),
    gutter: 8pt,
    align: horizon,
    [#text(font: serif, size: 14pt, weight: ""bold"", fill: rgb(""#E5C07B""))[#num]],
    [#text(font: serif, size: 14pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[#title]]
  )
  #v(6pt)
]

#let badge(txt, fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), text-color: rgb(""#F8FAFC"")) = {
  box(
    fill: fill,
    stroke: 0.5pt + stroke,
    radius: 3pt,
    inset: (x: 5pt, y: 2.5pt),
    text(font: sans, size: 7pt, weight: ""bold"", fill: text-color)[#txt]
  )
}

#let nav-marker() = {
  box(
    fill: rgb(""#0369A1"").lighten(80%),
    stroke: 0.5pt + rgb(""#38BDF8""),
    radius: 3pt,
    inset: (x: 5pt, y: 2pt),
    text(font: sans, size: 6.5pt, weight: ""bold"", fill: rgb(""#38BDF8""))[ПОДРОБНЫЙ РАЗБОР →]
  )
}
");

        // =========================================================================
        // SECTION 01: Executive Dashboard (Cover & Legal Readiness)
        // =========================================================================
        var topDriverCards = new List<ModuleCardDto>();
        if (ctx.Overall.TopDrivers.Count > 0)
        {
            foreach (var td in ctx.Overall.TopDrivers)
            {
                var card = ctx.ModuleCards.FirstOrDefault(m => m.Title.Equals(td, StringComparison.OrdinalIgnoreCase));
                if (card != null) topDriverCards.Add(card);
            }
        }
        if (topDriverCards.Count == 0)
        {
            topDriverCards = ctx.ModuleCards
                .Where(m => m.RenderMode != ReportRenderMode.NotApplicable && m.Score.HasValue)
                .OrderBy(m => m.Score!.Value)
                .Take(3)
                .ToList();
        }

        var crestImageSnippet = File.Exists(Path.Combine(_contentRootPath, "wwwroot", "img", "logo_mark.png"))
            ? "#image(\"/wwwroot/img/logo_mark.png\", width: 1.8cm)"
            : File.Exists(Path.Combine(_contentRootPath, "wwwroot", "img", "fenix_law_crest.png"))
                ? "#image(\"/wwwroot/img/fenix_law_crest.png\", width: 1.8cm)"
                : "#text(font: serif, size: 20pt, weight: \"bold\", fill: rgb(\"#E5C07B\"))[FENIX]";

        var coverProjectIntro = string.IsNullOrWhiteSpace(ctx.ProjectName) || ctx.ProjectName is "Проект" or "Стартап"
            ? "Экспресс-оценка правовой готовности, ключевых уязвимостей структуры и дорожная карта действий."
            : $"Экспресс-оценка правовой готовности, ключевых уязвимостей структуры и дорожная карта действий для проекта «{EscapeTypst(ctx.ProjectName)}».";

        sb.AppendLine($@"
#v(0.2cm)
#grid(
  columns: (auto, 1fr),
  gutter: 14pt,
  align: horizon,
  [
    {crestImageSnippet}
  ],
  [
    #text(font: serif, size: 22pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[FENIX SLS]
    #v(2pt)
    #text(font: sans, size: 8.5pt, fill: rgb(""#E5C07B""), tracking: 1.2pt, weight: ""medium"")[SMART LEGAL SCREENING #text(fill: rgb(""#94A3B8""))[· BY FENIX LAW]]
  ]
)
#v(0.3cm)
#text(font: serif, size: 10.5pt, fill: rgb(""#CBD5E1""))[{coverProjectIntro}]
#v(0.5cm)

#section-header(""01"", ""ОЦЕНКА ЮРИДИЧЕСКОЙ ГОТОВНОСТИ"", category: ""Общая оценка"")

#grid(
  columns: (1.2fr, 1fr),
  gutter: 14pt,
  [
    #card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 14pt)[
      #text(font: sans, size: 8pt, fill: rgb(""#94A3B8""), tracking: 1.2pt, weight: ""medium"")[ИНДЕКС ГОТОВНОСТИ]
      #v(6pt)
      #grid(
        columns: (auto, 1fr),
        gutter: 12pt,
        align: horizon,
        [#text(font: serif, size: 42pt, weight: ""bold"", fill: rgb(""" + GetScoreColor(ctx.Overall.Score) + @"""))[" + ctx.Overall.Score + @"]],
        [
          #text(font: sans, size: 14pt, fill: rgb(""#64748B""))[\/ 100]
          #v(2pt)
          #text(font: sans, size: 11.5pt, weight: ""bold"", fill: rgb(""" + GetScoreColor(ctx.Overall.Score) + @"""))[" + EscapeTypst(ctx.Overall.LevelTitle) + @"]
        ]
      )
      #v(10pt)
      #line(length: 100%, stroke: 0.5pt + rgb(""#1E2D4A""))
      #v(8pt)
      #text(font: sans, size: 8.5pt, fill: rgb(""#E2E8F0""))[" + EscapeTypst(ctx.Overall.LevelText) + @"]
    ]
  ],
  [
    #card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 14pt)[
      #text(font: sans, size: 8pt, fill: rgb(""#94A3B8""), tracking: 1.2pt, weight: ""medium"")[КАНОНИЧЕСКАЯ ШКАЛА SLS]
      #v(8pt)
      #table(
        columns: (auto, auto, 1fr),
        stroke: none,
        inset: (x: 4pt, y: 4.5pt),
        align: (left + horizon, left + horizon, left + horizon),
        [#circle(radius: 3pt, fill: rgb(""#34D399""))], [#text(font: sans, size: 8.5pt, weight: ""bold"")[80–100]], [#text(font: sans, size: 8.5pt, fill: rgb(""#CBD5E1""))[Хорошая готовность]],
        [#circle(radius: 3pt, fill: rgb(""#FBBF24""))], [#text(font: sans, size: 8.5pt, weight: ""bold"")[60–79]], [#text(font: sans, size: 8.5pt, fill: rgb(""#CBD5E1""))[Требует внимания]],
        [#circle(radius: 3pt, fill: rgb(""#FB923C""))], [#text(font: sans, size: 8.5pt, weight: ""bold"")[40–59]], [#text(font: sans, size: 8.5pt, fill: rgb(""#CBD5E1""))[Существенные пробелы]],
        [#circle(radius: 3pt, fill: rgb(""#F87171""))], [#text(font: sans, size: 8.5pt, weight: ""bold"")[0–39]], [#text(font: sans, size: 8.5pt, fill: rgb(""#CBD5E1""))[Критические пробелы]]
      )
    ]
  ]
)
");

        if (topDriverCards.Count > 0)
        {
            sb.AppendLine(@"
#v(0.4cm)
#text(font: serif, size: 11pt, weight: ""bold"", fill: rgb(""#E5C07B""))[КЛЮЧЕВЫЕ ФАКТОРЫ ТЕКУЩЕЙ ОЦЕНКИ]
#v(0.2cm)
#grid(
  columns: (" + string.Join(", ", Enumerable.Repeat("1fr", topDriverCards.Count)) + @"),
  gutter: 12pt,
");
            foreach (var drv in topDriverCards)
            {
                var drvColor = GetScoreColor(drv.Score ?? 0);
                sb.AppendLine($@"
  card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 11pt)[
    #grid(
      columns: (1fr, auto),
      [#text(font: serif, size: 18pt, weight: ""bold"", fill: rgb(""{drvColor}""))[{drv.Score ?? 0} #text(font: sans, size: 8.5pt, fill: rgb(""#64748B""))[\/ 100]]],
      [#circle(radius: 4pt, stroke: 1.5pt + rgb(""{drvColor}""))]
    )
    #v(4pt)
    #text(font: sans, size: 9pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[{EscapeTypst(drv.Title)}]
    #v(2pt)
    #text(font: sans, size: 8pt, fill: rgb(""{drvColor}""))[{EscapeTypst(drv.StatusText)}]
  ],");
            }
            sb.AppendLine(")\n");
        }

        sb.AppendLine(@"
#v(0.3cm)
#card(fill: rgb(""#0D1628""), inset: 11pt)[
  #grid(
    columns: (1fr, auto),
    [
      #text(font: sans, size: 8pt, fill: rgb(""#94A3B8""), tracking: 1.2pt, weight: ""medium"")[ПРИНЦИП РАСЧЕТА]
      #v(2pt)
      #text(font: sans, size: 8pt, fill: rgb(""#CBD5E1""))[" + EscapeTypst(ctx.Overall.BottomExplanation) + @"]
    ],
    align(right + horizon)[
      #text(font: sans, size: 8pt, fill: rgb(""#64748B""))[Полнота данных: *" + ctx.Overall.Confidence + @"%*]
    ]
  )
]
#pagebreak()
");

        // =========================================================================
        // SECTION 02: Project Profile (Structured Fact Clusters)
        // =========================================================================
        secNum = 2;
        var fMap = ctx.Profile.KeyFacts.ToDictionary(f => f.Key, f => f, StringComparer.OrdinalIgnoreCase);

        sb.AppendLine(@"
#section-header(""" + secNum++.ToString("D2") + @""", ""ВВОДНЫЕ ДАННЫЕ И ПРОФИЛЬ ПРОЕКТА"", category: ""Контекст анализа"")
#text(font: sans, size: 8.5pt, fill: rgb(""#94A3B8""))[Факты зафиксированы на основе ваших ответов и определяют контекст юридической оценки.]
#v(0.4cm)

#grid(
  columns: (1fr, 1fr),
  gutter: 12pt,
  [
    #card(fill: rgb(""#0D1628""), inset: 11pt)[
      #text(font: serif, size: 10pt, weight: ""bold"", fill: rgb(""#E5C07B""))[КОМПАНИЯ И СТРУКТУРА]
      #v(6pt)
      #table(
        columns: (1fr, 1.2fr),
        stroke: (x, y) => if y > 0 { (top: 0.5pt + rgb(""#1E2D4A"")) } else { none },
        inset: (x: 2pt, y: 5pt),
        align: (left + horizon, left + horizon),
        [#text(font: sans, size: 8pt, fill: rgb(""#94A3B8""))[Юридическое лицо:]], [#text(font: sans, size: 8pt, weight: ""medium"", fill: rgb(""#FFFFFF""))[" + EscapeTypst(fMap.GetValueOrDefault("entity")?.Value ?? "Не указано") + @"]],
        [#text(font: sans, size: 8pt, fill: rgb(""#94A3B8""))[Юрисдикция:]], [#text(font: sans, size: 8pt, weight: ""medium"", fill: rgb(""#FFFFFF""))[" + EscapeTypst(fMap.GetValueOrDefault("jurisdiction")?.Value ?? "Не указано") + @"]]
      )
    ]
  ],
  [
    #card(fill: rgb(""#0D1628""), inset: 11pt)[
      #text(font: serif, size: 10pt, weight: ""bold"", fill: rgb(""#E5C07B""))[ОСНОВАТЕЛИ]
      #v(6pt)
      #table(
        columns: (1fr, 1.2fr),
        stroke: (x, y) => if y > 0 { (top: 0.5pt + rgb(""#1E2D4A"")) } else { none },
        inset: (x: 2pt, y: 5pt),
        align: (left + horizon, left + horizon),
        [#text(font: sans, size: 8pt, fill: rgb(""#94A3B8""))[Состав:]], [#text(font: sans, size: 8pt, weight: ""medium"", fill: rgb(""#FFFFFF""))[" + EscapeTypst(fMap.GetValueOrDefault("founders")?.Value ?? "Не указано") + @"]],
        [#text(font: sans, size: 8pt, fill: rgb(""#94A3B8""))[Распределение долей:]], [#text(font: sans, size: 8pt, weight: ""medium"", fill: rgb(""#FFFFFF""))[" + EscapeTypst(fMap.GetValueOrDefault("equity")?.Value ?? "Не указано") + @"]]
      )
    ]
  ]
)
#v(0.3cm)
#grid(
  columns: (1fr, 1fr),
  gutter: 12pt,
  [
    #card(fill: rgb(""#0D1628""), inset: 11pt)[
      #text(font: serif, size: 10pt, weight: ""bold"", fill: rgb(""#E5C07B""))[ПРОДУКТ И ПОЛЬЗОВАТЕЛИ]
      #v(6pt)
      #table(
        columns: (1fr, 1.2fr),
        stroke: (x, y) => if y > 0 { (top: 0.5pt + rgb(""#1E2D4A"")) } else { none },
        inset: (x: 2pt, y: 5pt),
        align: (left + horizon, left + horizon),
        [#text(font: sans, size: 8pt, fill: rgb(""#94A3B8""))[Стадия продукта:]], [#text(font: sans, size: 8pt, weight: ""medium"", fill: rgb(""#FFFFFF""))[" + EscapeTypst(fMap.GetValueOrDefault("stage")?.Value ?? "Не указано") + @"]],
        [#text(font: sans, size: 8pt, fill: rgb(""#94A3B8""))[Пользователи:]], [#text(font: sans, size: 8pt, weight: ""medium"", fill: rgb(""#FFFFFF""))[" + EscapeTypst(fMap.GetValueOrDefault("users")?.Value ?? "Не указано") + @"]]
      )
    ]
  ],
  [
    #card(fill: rgb(""#0D1628""), inset: 11pt)[
      #text(font: serif, size: 10pt, weight: ""bold"", fill: rgb(""#E5C07B""))[РАЗРАБОТКА И IP]
      #v(6pt)
      #table(
        columns: (1fr, 1.2fr),
        stroke: (x, y) => if y > 0 { (top: 0.5pt + rgb(""#1E2D4A"")) } else { none },
        inset: (x: 2pt, y: 5pt),
        align: (left + horizon, left + horizon),
        [#text(font: sans, size: 8pt, fill: rgb(""#94A3B8""))[Кто создает продукт:]], [#text(font: sans, size: 8pt, weight: ""medium"", fill: rgb(""#FFFFFF""))[" + EscapeTypst(fMap.GetValueOrDefault("creators")?.Value ?? "Не указано") + @"]],
        [#text(font: sans, size: 8pt, fill: rgb(""#94A3B8""))[Права на результаты:]], [#text(font: sans, size: 8pt, weight: ""medium"", fill: rgb(""#FFFFFF""))[" + EscapeTypst(fMap.GetValueOrDefault("ip_rights")?.Value ?? "Не указано") + @"]]
      )
    ]
  ]
)

#v(0.4cm)
#card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 13pt)[
  #text(font: sans, size: 8pt, fill: rgb(""#94A3B8""), tracking: 1.2pt, weight: ""medium"")[СИНТЕЗ ТЕКУЩЕЙ КОНФИГУРАЦИИ]
  #v(4pt)
  #text(font: serif, size: 9.5pt, fill: rgb(""#E2E8F0""), style: ""italic"")[" + EscapeTypst(ctx.Profile.ConfigurationNarrative) + @"]
]
#pagebreak()
");

        // =========================================================================
        // PRECOMPUTE DYNAMIC PAGE NUMBERING FOR 8-ZONE MAP & NAVIGATION
        // =========================================================================
        var pageMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int curPage = 7; // Sections 01..06 occupy pages 1..6

        foreach (var focus in ctx.FocusModules)
        {
            pageMap[focus.SectionId] = curPage;
            curPage += 1;
        }

        int compactPage = curPage;
        if (ctx.CompactModules.Count > 0 || ctx.NotApplicableModules.Count > 0)
        {
            curPage += 1;
        }

        int invPage = curPage;
        if (ctx.InvestmentReadiness != null && ctx.InvestmentReadiness.IsApplicable)
        {
            pageMap["investment"] = invPage;
            curPage += 1;
        }

        // =========================================================================
        // SECTION 03: Executive Conclusion (Итоговый вывод)
        // =========================================================================
        sb.AppendLine(@"
#section-header(""" + secNum++.ToString("D2") + @""", ""ИТОГОВЫЙ ВЫВОД"", category: ""Резюме ситуации"")
#v(0.3cm)

#card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 14pt)[
  #text(font: serif, size: 11pt, weight: ""bold"", fill: rgb(""#E5C07B""))[СИСТЕМНЫЙ СИНТЕЗ ЮРИДИЧЕСКОЙ СИТУАЦИИ]
  #v(8pt)
  #text(font: sans, size: 9.5pt, fill: rgb(""#FFFFFF""), style: ""normal"")[" + EscapeTypst(ctx.ExecutiveConclusion) + @"]
]

#v(0.4cm)
#card(fill: rgb(""#0D1628""), inset: 12pt)[
  #text(font: serif, size: 10pt, weight: ""bold"", fill: rgb(""#38BDF8""))[ПРИОРИТЕТ ВНИМАНИЯ ОСНОВАТЕЛЕЙ]
  #v(4pt)
  #text(font: sans, size: 8.5pt, fill: rgb(""#CBD5E1""))[Первоочередные усилия команды должны быть направлены на устранение критических юридических блокеров (структурирование долей, оформление прав на ключевые разработки и фиксация отношений с командой). Полный пошаговый план устранения представлен в разделе «Единый план действий».]
]
#pagebreak()
");

        // =========================================================================
        // SECTION 04: Key Risks Full Map (Ключевые выявленные риски)
        // =========================================================================
        var allCriticalBlockers = ctx.AllFindings
            .Where(f => f.Severity is RiskSeverity.Blocker or RiskSeverity.Critical or RiskSeverity.High)
            .OrderByDescending(f => f.Severity switch
            {
                RiskSeverity.Blocker => 4,
                RiskSeverity.Critical => 3,
                RiskSeverity.High => 2,
                _ => 1
            })
            .ThenBy(f => f.Priority switch
            {
                RiskPriority.Now => 0,
                RiskPriority.ThirtyDays => 1,
                RiskPriority.BeforeRound => 2,
                _ => 3
            })
            .ToList();

        sb.AppendLine(@"
#section-header(""" + secNum++.ToString("D2") + @""", ""КЛЮЧЕВЫЕ ВЫЯВЛЕННЫЕ РИСКИ"", category: ""Сводный реестр рисков"")
#text(font: sans, size: 8.5pt, fill: rgb(""#94A3B8""))[Сводная таблица блокирующих, критических и высоких рисков по всем направлениям юридического скрининга.]
#v(0.4cm)
");
        if (allCriticalBlockers.Count > 0)
        {
            sb.AppendLine(@"
#table(
  columns: (2.2fr, 1.2fr, 1.1fr, 1.1fr),
  stroke: (x, y) => if y > 0 { (top: 0.5pt + rgb(""#1E2D4A"")) } else { none },
  inset: (x: 5pt, y: 7pt),
  align: (left + horizon, left + horizon, center + horizon, right + horizon),
  [#text(font: sans, size: 8pt, weight: ""bold"", fill: rgb(""#94A3B8""))[ВЫЯВЛЕННЫЙ РИСК]],
  [#text(font: sans, size: 8pt, weight: ""bold"", fill: rgb(""#94A3B8""))[НАПРАВЛЕНИЕ]],
  [#text(font: sans, size: 8pt, weight: ""bold"", fill: rgb(""#94A3B8""))[УРОВЕНЬ РИСКА]],
  [#text(font: sans, size: 8pt, weight: ""bold"", fill: rgb(""#94A3B8""))[ПРИОРИТЕТ]],
");
            foreach (var r in allCriticalBlockers.Take(8))
            {
                var fColor = r.Severity is RiskSeverity.Blocker or RiskSeverity.Critical ? "#F87171" : "#FB923C";
                var sevLabel = r.Severity switch
                {
                    RiskSeverity.Blocker => "Блокирующий",
                    RiskSeverity.Critical => "Критический",
                    RiskSeverity.High => "Высокий",
                    _ => "Умеренный"
                };
                var prioLabel = r.Priority switch
                {
                    RiskPriority.Now => "В 1-ю очередь",
                    RiskPriority.ThirtyDays => "До 30 дней",
                    RiskPriority.BeforeRound => "До раунда",
                    _ => "Планово"
                };
                var secTitle = ctx.ModuleCards.FirstOrDefault(m => m.SectionId.Equals(r.SectionId, StringComparison.OrdinalIgnoreCase))?.Title ?? r.SectionId;

                sb.AppendLine($@"  [#text(font: sans, size: 8pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[{EscapeTypst(r.Title)}]],");
                sb.AppendLine($@"  [#text(font: sans, size: 8pt, fill: rgb(""#CBD5E1""))[{EscapeTypst(secTitle)}]],");
                sb.AppendLine($@"  [#badge(""{sevLabel}"", stroke: rgb(""{fColor}""), text-color: rgb(""{fColor}""))],");
                sb.AppendLine($@"  [#text(font: sans, size: 7.5pt, fill: rgb(""#E5C07B""))[{prioLabel}]],");
            }
            sb.AppendLine(")\n");
        }
        else
        {
            sb.AppendLine(@"
#card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 14pt)[
  #text(font: sans, size: 9pt, fill: rgb(""#34D399""))[Критических и блокирующих рисков по результатам скрининга не выявлено. Текущие задачи носят плановый поддерживающий характер.]
]
");
        }
        sb.AppendLine("#pagebreak()\n");

        // =========================================================================
        // SECTION 05: What is already built (Что уже выстроено)
        // =========================================================================
        sb.AppendLine(@"
#section-header(""" + secNum++.ToString("D2") + @""", ""ЧТО УЖЕ ВЫСТРОЕНО"", category: ""Подтвержденные преимущества"")
#text(font: sans, size: 8.5pt, fill: rgb(""#94A3B8""))[Юридические элементы и практики, которые уже корректно сформированы в проекте и защищают бизнес.]
#v(0.4cm)
");
        if (ctx.PositiveFactors.Count > 0)
        {
            sb.AppendLine(@"#grid(
  columns: (1fr, 1fr),
  gutter: 12pt,");
            foreach (var pos in ctx.PositiveFactors.Take(6))
            {
                sb.AppendLine($@"  card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 11pt)[
    #grid(
      columns: (auto, 1fr),
      gutter: 10pt,
      align: horizon,
      [#circle(radius: 4pt, fill: rgb(""#34D399""))],
      [
        #text(font: sans, size: 8.5pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[{EscapeTypst(pos.Title)}]
        #v(2pt)
        #text(font: sans, size: 7.5pt, fill: rgb(""#94A3B8""))[{EscapeTypst(pos.Category)}]
      ]
    )
  ],");
            }
            sb.AppendLine(")\n");
        }
        else
        {
            sb.AppendLine(@"#card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 13pt)[
  #text(font: sans, size: 8.5pt, fill: rgb(""#94A3B8""))[На текущем этапе ключевые правовые элементы требуют первичной формализации. По мере реализации плана действий здесь будут закрепляться проверенные преимущества.]
]
");
        }
        sb.AppendLine("#pagebreak()\n");

        // =========================================================================
        // SECTION 06: 8-Zone Map (Strict equal card geometry with dynamic page references)
        // =========================================================================
        sb.AppendLine(@"
#section-header(""" + secNum++.ToString("D2") + @""", ""КАРТА ЮРИДИЧЕСКОЙ ГОТОВНОСТИ"", category: ""Диагностическая матрица"")
#text(font: sans, size: 8.5pt, fill: rgb(""#94A3B8""))[Сводная оценка всех 8 направлений SLS с прямыми ссылками на страницы подробного разбора.]
#v(0.5cm)

#grid(
  columns: (1fr, 1fr, 1fr, 1fr),
  column-gutter: 10pt,
  row-gutter: 12pt,
");
        int cardNum = 1;
        foreach (var card in ctx.ModuleCards)
        {
            var isDetailed = card.RenderMode == ReportRenderMode.Focus ||
                             (card.SectionId.Equals("investment", StringComparison.OrdinalIgnoreCase) && card.RenderMode != ReportRenderMode.NotApplicable);
            var isNa = card.RenderMode == ReportRenderMode.NotApplicable;

            var scoreText = isNa ? "—" : $"{card.Score ?? 0}";
            var scoreColor = isNa ? "#64748B" : GetScoreColor(card.Score ?? 0);
            var statusColor = isNa ? "#64748B" : scoreColor;

            string navLinkText;
            if (isNa)
            {
                navLinkText = "#text(font: sans, size: 6.5pt, fill: rgb(\"#475569\"))[Не применимо]";
            }
            else if (card.RenderMode == ReportRenderMode.Focus)
            {
                var targetP = pageMap.GetValueOrDefault(card.SectionId, 7);
                navLinkText = $"#text(font: sans, size: 6.5pt, weight: \"bold\", fill: rgb(\"#38BDF8\"))[Подробный разбор — стр. {targetP}]";
            }
            else if (card.SectionId.Equals("investment", StringComparison.OrdinalIgnoreCase))
            {
                var targetP = pageMap.GetValueOrDefault("investment", compactPage + 1);
                navLinkText = $"#text(font: sans, size: 6.5pt, weight: \"bold\", fill: rgb(\"#38BDF8\"))[Инвест-срез — стр. {targetP}]";
            }
            else
            {
                navLinkText = $"#text(font: sans, size: 6.5pt, fill: rgb(\"#64748B\"))[Краткий обзор — стр. {compactPage}]";
            }

            sb.AppendLine($@"
  rect(
    width: 100%,
    fill: rgb(""#0D1628""),
    stroke: 0.75pt + rgb(""#1E2D4A""),
    radius: 6pt,
    inset: (x: 10pt, y: 11pt)
  )[
    #text(font: serif, size: 9pt, weight: ""bold"", fill: rgb(""#E5C07B""))[0{cardNum++}]
    #v(3pt)
    #block(height: 24pt)[
      #text(font: sans, size: 7.5pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[{EscapeTypst(card.Title.ToUpperInvariant())}]
    ]
    #v(6pt)
    #block(height: 26pt)[
      #text(font: serif, size: 22pt, weight: ""bold"", fill: rgb(""{scoreColor}""))[{scoreText}]
      {(isNa ? "" : "#text(font: sans, size: 8pt, fill: rgb(\"#64748B\"))[\\/ 100]")}
    ]
    #v(4pt)
    #block(height: 14pt)[
      #text(font: sans, size: 7.5pt, weight: ""medium"", fill: rgb(""{statusColor}""))[{EscapeTypst(card.StatusText)}]
    ]
    #v(6pt)
    #block(height: 14pt)[
      {navLinkText}
    ]
  ],");
        }
        sb.AppendLine(@"
)
#pagebreak()
");

        // =========================================================================
        // SECTIONS 05..N: FOCUS Modules (Lossless Action-First Layout)
        // =========================================================================
        foreach (var focus in ctx.FocusModules)
        {
            var scoreColor = GetScoreColor(focus.Score);

            sb.AppendLine(@"
#grid(
  columns: (1fr, auto),
  align: horizon,
  [
    #section-header(""" + secNum++.ToString("D2") + @""", """ + EscapeTypst(focus.Title.ToUpperInvariant()) + @""", category: ""Глубокий анализ зоны"")
  ],
  [#nav-marker()]
)

#grid(
  columns: (auto, 1fr),
  gutter: 12pt,
  align: horizon,
  [
    #text(font: serif, size: 28pt, weight: ""bold"", fill: rgb(""" + scoreColor + @"""))[" + focus.Score + @"]
    #text(font: sans, size: 12pt, fill: rgb(""#64748B""))[\/ 100]
    #text(font: sans, size: 10pt, weight: ""bold"", fill: rgb(""" + scoreColor + @"""))[ — " + EscapeTypst(focus.ScoreBand) + @"]
  ],
  [
    #text(font: sans, size: 8.5pt, fill: rgb(""#CBD5E1""))[" + EscapeTypst(focus.SubtitleNarrative) + @"]
  ]
)
#v(0.3cm)
");
            if (focus.Findings.Count > 0)
            {
                var fullFindings = focus.Findings.Take(2).ToList();
                var remainingFindings = focus.Findings.Skip(2).ToList();

                sb.AppendLine(@"#text(font: serif, size: 11pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[КЛЮЧЕВЫЕ РИСКИ]");
                sb.AppendLine(@"#v(0.2cm)");

                foreach (var finding in fullFindings)
                {
                    var fColor = finding.Severity is RiskSeverity.Critical or RiskSeverity.Blocker ? "#F87171" : "#FB923C";
                    sb.AppendLine($@"
#card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 11pt)[
  #grid(
    columns: (auto, 1fr, auto),
    gutter: 8pt,
    align: horizon,
    [#circle(radius: 4pt, stroke: 1.5pt + rgb(""{fColor}""))],
    [#text(font: sans, size: 9pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[{EscapeTypst(finding.Title)}]],
    [#badge(""{finding.SeverityLabel}"", stroke: rgb(""{fColor}""), text-color: rgb(""{fColor}""))]
  )
  #v(5pt)
  #text(font: sans, size: 8pt, fill: rgb(""#94A3B8""))[*Почему выявлено:* {EscapeTypst(finding.WhyFound)}]
  #v(3pt)
  #text(font: sans, size: 8pt, fill: rgb(""#CBD5E1""))[*Почему важно:* {EscapeTypst(finding.WhyItMatters)}]
  #v(3pt)
  #text(font: sans, size: 8pt, fill: rgb(""#E5C07B""))[*Что рекомендуется сделать:* {EscapeTypst(finding.Recommendation)}]
  #v(6pt)
  #line(length: 100%, stroke: 0.5pt + rgb(""#1E2D4A""))
  #v(4pt)
  #grid(
    columns: (1fr, 1fr),
    [#text(font: sans, size: 7.5pt, fill: rgb(""#94A3B8""))[Срок: *{EscapeTypst(finding.PriorityLabel)}*]],
    align(right)[#text(font: sans, size: 7.5pt, fill: rgb(""#38BDF8""))[{EscapeTypst(finding.ResolutionFormat)}]]
  )
]
#v(0.2cm)
");
                }

                if (remainingFindings.Count > 0)
                {
                    sb.AppendLine(@"#v(0.1cm)");
                    sb.AppendLine(@"#text(font: serif, size: 10pt, weight: ""bold"", fill: rgb(""#E5C07B""))[ДРУГИЕ ВЫЯВЛЕННЫЕ РИСКИ]");
                    sb.AppendLine(@"#v(0.15cm)");

                    foreach (var finding in remainingFindings)
                    {
                        var fColor = finding.Severity is RiskSeverity.Critical or RiskSeverity.Blocker ? "#F87171" : "#FB923C";
                        sb.AppendLine($@"
#card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 9pt)[
  #grid(
    columns: (auto, 1fr),
    gutter: 8pt,
    align: horizon,
    [#badge(""{finding.SeverityLabel}"", stroke: rgb(""{fColor}""), text-color: rgb(""{fColor}""))],
    [#text(font: sans, size: 8.5pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[{EscapeTypst(finding.Title)}]]
  )
  #v(3pt)
  #text(font: sans, size: 7.5pt, fill: rgb(""#94A3B8""))[{EscapeTypst(finding.WhyFound)}]
  #v(2pt)
  #text(font: sans, size: 7.5pt, fill: rgb(""#E5C07B""))[→ {EscapeTypst(finding.Recommendation)}]
]
#v(0.15cm)
");
                    }
                }
            }
            else
            {
                sb.AppendLine(@"
#text(font: serif, size: 11pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[ВЫЯВЛЕННЫЕ РИСКИ И РЕКОМЕНДАЦИИ]
#v(0.2cm)
#card(fill: rgb(""#0D1628""), inset: 10pt)[
  #text(font: sans, size: 8pt, fill: rgb(""#94A3B8""))[Прямых критических блокеров в данном направлении не выявлено. Текущая оценка сформирована совокупностью факторов правовой структуры.]
]
#v(0.2cm)
");
            }

            sb.AppendLine(@"
#v(0.2cm)
#card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 10pt)[
  #text(font: sans, size: 7.5pt, fill: rgb(""#38BDF8""), tracking: 1.2pt, weight: ""medium"")[ПРАКТИЧЕСКОЕ ЗНАЧЕНИЕ ДЛЯ БИЗНЕСА]
  #v(3pt)
  #text(font: sans, size: 8pt, fill: rgb(""#E2E8F0""))[" + EscapeTypst(focus.PracticalMeaning) + @"]
]

#v(0.3cm)
#grid(
  columns: (1.1fr, 1.1fr),
  gutter: 12pt,
  [
    #card(fill: rgb(""#0D1628""), inset: 10pt)[
      #text(font: serif, size: 9.5pt, weight: ""bold"", fill: rgb(""#E5C07B""))[ПОЧЕМУ СФОРМИРОВАНА ТАКАЯ ОЦЕНКА]
      #v(4pt)
");
            if (focus.NegativeDrivers.Count > 0)
            {
                sb.AppendLine("      #text(font: sans, size: 7.5pt, weight: \"bold\", fill: rgb(\"#F87171\"))[↓ Сильно снижает]\n      #v(2pt)");
                foreach (var d in focus.NegativeDrivers)
                {
                    sb.AppendLine($"      #text(font: sans, size: 7.5pt, fill: rgb(\"#E2E8F0\"))[· {EscapeTypst(d)}]\n      #v(1pt)");
                }
            }

            if (focus.AttentionDrivers.Count > 0)
            {
                sb.AppendLine("      #v(3pt)\n      #text(font: sans, size: 7.5pt, weight: \"bold\", fill: rgb(\"#FBBF24\"))[△ Требует внимания]\n      #v(2pt)");
                foreach (var d in focus.AttentionDrivers)
                {
                    sb.AppendLine($"      #text(font: sans, size: 7.5pt, fill: rgb(\"#E2E8F0\"))[· {EscapeTypst(d)}]\n      #v(1pt)");
                }
            }

            if (focus.PositiveDrivers.Count > 0)
            {
                sb.AppendLine("      #v(3pt)\n      #text(font: sans, size: 7.5pt, weight: \"bold\", fill: rgb(\"#34D399\"))[✓ Уже выстроено]\n      #v(2pt)");
                foreach (var d in focus.PositiveDrivers)
                {
                    sb.AppendLine($"      #text(font: sans, size: 7.5pt, fill: rgb(\"#E2E8F0\"))[· {EscapeTypst(d)}]\n      #v(1pt)");
                }
            }

            sb.AppendLine(@"
    ]
  ],
  [
    #card(fill: rgb(""#0D1628""), inset: 10pt)[
      #text(font: serif, size: 9.5pt, weight: ""bold"", fill: rgb(""#E5C07B""))[ДЕТАЛИЗАЦИЯ ПО ФАКТОРАМ]
      #v(4pt)
      #table(
        columns: (1fr, auto),
        stroke: (x, y) => if y > 0 { (top: 0.5pt + rgb(""#1E2D4A"")) } else { none },
        inset: (x: 2pt, y: 4pt),
        align: (left + horizon, right + horizon),
");
            foreach (var row in focus.FactorBreakdown)
            {
                var rowColor = row.IsPositive ? "#34D399" : row.Severity.HasValue ? "#F87171" : "#FBBF24";
                sb.AppendLine($"        [#text(font: sans, size: 7.5pt, fill: rgb(\"#CBD5E1\"))[{EscapeTypst(row.FactorName)}]], [#badge(\"{EscapeTypst(row.StatusText)}\", stroke: rgb(\"{rowColor}\"), text-color: rgb(\"{rowColor}\"))],");
            }
            sb.AppendLine(@"
      )
    ]
  ]
)
#pagebreak()
");
        }

        // =========================================================================
        // SECTION N+1: Compact & N/A Modules (Excludes Investment)
        // =========================================================================
        if (ctx.CompactModules.Count > 0 || ctx.NotApplicableModules.Count > 0)
        {
            sb.AppendLine(@"
#section-header(""" + secNum++.ToString("D2") + @""", ""ОСТАЛЬНЫЕ НАПРАВЛЕНИЯ (КОМПАКТНО)"", category: ""Обзорный срез"")
#text(font: sans, size: 8.5pt, fill: rgb(""#94A3B8""))[Короткая расшифровка оценок по направлениям, не вошедшим в основной фокус отчета, и неприменимым блокам.]
#v(0.4cm)

#grid(
  columns: (1fr, 1fr),
  gutter: 12pt,
");
            foreach (var comp in ctx.CompactModules)
            {
                var sc = GetScoreColor(comp.Score);
                sb.AppendLine("  card(fill: rgb(\"#0D1628\"), inset: 10pt)[");
                sb.AppendLine("    #grid(");
                sb.AppendLine("      columns: (1fr, auto),");
                sb.AppendLine($"      [#text(font: sans, size: 9pt, weight: \"bold\", fill: rgb(\"#FFFFFF\"))[{EscapeTypst(comp.Title)}]],");
                sb.AppendLine($"      [#text(font: serif, size: 12pt, weight: \"bold\", fill: rgb(\"{sc}\"))[{comp.Score} #text(font: sans, size: 7.5pt, fill: rgb(\"#64748B\"))[\\/ 100]]]");
                sb.AppendLine("    )");
                sb.AppendLine("    #v(2pt)");
                sb.AppendLine($"    #text(font: sans, size: 7.5pt, fill: rgb(\"{sc}\"))[{EscapeTypst(comp.StatusText)}]");
                sb.AppendLine("    #v(4pt)");
                sb.AppendLine($"    #text(font: sans, size: 8pt, fill: rgb(\"#E2E8F0\"))[{EscapeTypst(comp.Summary)}]");
                sb.AppendLine("  ],");
            }

            foreach (var na in ctx.NotApplicableModules)
            {
                sb.AppendLine("  card(fill: rgb(\"#0D1628\"), stroke: rgb(\"#1E2D4A\"), inset: 10pt)[");
                sb.AppendLine($"    #text(font: sans, size: 9pt, weight: \"bold\", fill: rgb(\"#64748B\"))[{EscapeTypst(na.Title)}]");
                sb.AppendLine("    #v(2pt)");
                sb.AppendLine("    #badge(\"Не применимо\", stroke: rgb(\"#475569\"), text-color: rgb(\"#64748B\"))");
                sb.AppendLine("    #v(4pt)");
                sb.AppendLine($"    #text(font: sans, size: 8pt, fill: rgb(\"#94A3B8\"))[{EscapeTypst(na.ReasonText)}]");
                sb.AppendLine("    #v(2pt)");
                sb.AppendLine($"    #text(font: sans, size: 7.5pt, fill: rgb(\"#64748B\"))[{EscapeTypst(na.TriggerEventText)}]");
                sb.AppendLine("  ],");
            }
            sb.AppendLine(@"
)
#pagebreak()
");
        }

        // =========================================================================
        // SECTION N+2: Dedicated Investment Readiness (if applicable)
        // =========================================================================
        if (ctx.InvestmentReadiness != null && ctx.InvestmentReadiness.IsApplicable)
        {
            var inv = ctx.InvestmentReadiness;
            var invColor = GetScoreColor(inv.ReadinessScore);

            sb.AppendLine(@"
#section-header(""" + secNum++.ToString("D2") + @""", ""ГОТОВНОСТЬ К ИНВЕСТИЦИЯМ"", category: ""Инвесторский срез"")
#text(font: sans, size: 8.5pt, fill: rgb(""#94A3B8""))[Специальный аналитический срез готовности компании к инвестиционному раунду и проверке Due Diligence.]
#v(0.4cm)

#card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 14pt)[
  #grid(
    columns: (auto, 1fr),
    gutter: 16pt,
    align: horizon,
    [
      #text(font: serif, size: 38pt, weight: ""bold"", fill: rgb(""" + invColor + @"""))[" + inv.ReadinessScore + @"]
      #text(font: sans, size: 14pt, fill: rgb(""#64748B""))[\/ 100]
    ],
    [
      #text(font: serif, size: 12pt, weight: ""bold"", fill: rgb(""#E5C07B""))[" + EscapeTypst(inv.Category) + @"]
      #v(3pt)
      #text(font: sans, size: 8.5pt, fill: rgb(""#E2E8F0""))[" + EscapeTypst(inv.SummaryDescription) + @"]
    ]
  )
]

#v(0.4cm)
#grid(
  columns: (1fr, 1fr),
  gutter: 14pt,
  [
    #card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 12pt)[
      #text(font: serif, size: 10pt, weight: ""bold"", fill: rgb(""#F87171""))[ЧТО МОЖЕТ ЗАДЕРЖАТЬ РАУНД]
      #v(8pt)
");
            if (inv.BlockerTitles.Count > 0)
            {
                foreach (var b in inv.BlockerTitles)
                {
                    sb.AppendLine($"      #text(font: sans, size: 8pt, fill: rgb(\"#FCA5A5\"))[● {EscapeTypst(b)}]\n      #v(3pt)");
                }
            }
            else
            {
                sb.AppendLine("      #text(font: sans, size: 8pt, fill: rgb(\"#CBD5E1\"))[Критичных блокеров для раунда не обнаружено.]\n");
            }

            sb.AppendLine(@"
    ]
  ],
  [
    #card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 12pt)[
      #text(font: serif, size: 10pt, weight: ""bold"", fill: rgb(""#34D399""))[ГОТОВО К ЮРИДИЧЕСКОЙ ПРОВЕРКЕ (DUE DILIGENCE)]
      #v(8pt)
");
            if (inv.CatalystTitles.Count > 0)
            {
                foreach (var c in inv.CatalystTitles)
                {
                    sb.AppendLine($"      #text(font: sans, size: 8pt, fill: rgb(\"#86EFAC\"))[● {EscapeTypst(c)}]\n      #v(3pt)");
                }
            }
            else
            {
                sb.AppendLine("      #text(font: sans, size: 8pt, fill: rgb(\"#CBD5E1\"))[Базовые параметры проекта зафиксированы.]\n");
            }
            sb.AppendLine(@"
    ]
  ]
)
#pagebreak()
");
        }

        // =========================================================================
        // SECTION N+3: Unified Action Plan (Project Roadmap)
        // =========================================================================
        sb.AppendLine(@"
#section-header(""" + secNum++.ToString("D2") + @""", ""ЕДИНЫЙ ПЛАН ДЕЙСТВИЙ · ДОРОЖНАЯ КАРТА"", category: ""Исполнительный план"")
#text(font: sans, size: 8.5pt, fill: rgb(""#94A3B8""))[Порядок шагов выстроен по реальному влиянию на защиту бизнеса и сроки заключения сделок.]
#v(0.4cm)
");
        if (ctx.ActionPlan.Count == 0)
        {
            sb.AppendLine(@"
#card(fill: rgb(""#0D1628""), inset: 14pt)[
  #text(font: sans, size: 8.5pt, fill: rgb(""#94A3B8""))[По результатам текущего скрининга первоочередных действий не выявлено.]
]
");
        }
        else
        {
            var groupedActions = ctx.ActionPlan.GroupBy(a => a.PriorityGroup);
            foreach (var group in groupedActions)
            {
                var grpColor = group.Key.Contains("ПЕРВУЮ") ? "#F87171" : group.Key.Contains("СЛЕДУЮЩИМ") ? "#FB923C" : "#38BDF8";

                sb.AppendLine($@"
#text(font: serif, size: 10.5pt, weight: ""bold"", fill: rgb(""{grpColor}""))[{EscapeTypst(group.Key.ToUpperInvariant())}]
#v(0.2cm)
");
                foreach (var action in group)
                {
                    sb.AppendLine($@"
#card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 10pt)[
  #grid(
    columns: (auto, 1fr, auto),
    gutter: 10pt,
    align: (top, top, top),
    [#text(font: serif, size: 13pt, weight: ""bold"", fill: rgb(""#E5C07B""))[{action.Number:D2}]],
    [
      #text(font: sans, size: 9pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[{EscapeTypst(action.Title)}]
      #v(3pt)
      #text(font: sans, size: 8pt, fill: rgb(""#E2E8F0""))[*Что сделать:* {EscapeTypst(action.WhatToDo)}]
      #v(2pt)
      #text(font: sans, size: 8pt, fill: rgb(""#94A3B8""))[*Почему сейчас:* {EscapeTypst(action.WhyNow)}]
      #v(2pt)
      #text(font: sans, size: 8pt, fill: rgb(""#34D399""))[*Результат:* {EscapeTypst(action.ExpectedResult)}]
    ],
    [#badge(""{EscapeTypst(action.ResolutionFormat)}"", stroke: rgb(""#1E2D4A""), text-color: rgb(""#38BDF8""))]
  )
]
#v(0.25cm)
");
                }
            }
        }
        sb.AppendLine("#pagebreak()\n");

        // =========================================================================
        // SECTION N+4: Recommended Next Step / Fenix Law
        // =========================================================================
        var secNextSubtitle = ctx.FenixLaw.RequiresLegalWork
            ? "На основании результатов скрининга определена целесообразность точечного юридического сопровождения."
            : "Оценка необходимости привлечения профессиональных юристов и форматы самостоятельного решения.";

        sb.AppendLine(@"
#section-header(""" + secNum++.ToString("D2") + @""", ""РЕКОМЕНДОВАННЫЙ СЛЕДУЮЩИЙ ШАГ"", category: ""Юридическое сопровождение"")
#text(font: sans, size: 8.5pt, fill: rgb(""#94A3B8""))[" + secNextSubtitle + @"]
#v(0.4cm)

#card(fill: rgb(""#0D1628""), stroke: rgb(""#1E2D4A""), inset: 13pt)[
  #text(font: sans, size: 9pt, fill: rgb(""#E2E8F0""))[" + EscapeTypst(ctx.FenixLaw.SummaryText) + @"]
]
#v(0.4cm)
");
        var tgUser = string.IsNullOrWhiteSpace(ctx.FenixLaw.Telegram) ? "@fenixlaw" : ctx.FenixLaw.Telegram.Trim();
        var tgLink = "https://t.me/" + tgUser.TrimStart('@');
        var webUrl = string.IsNullOrWhiteSpace(ctx.FenixLaw.Website) ? "www.fenixlaw.org" : ctx.FenixLaw.Website.Trim();
        var webLink = webUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? webUrl : "https://" + webUrl;
        var phoneVal = string.IsNullOrWhiteSpace(ctx.FenixLaw.Phone) ? "+7-700-559-1377" : ctx.FenixLaw.Phone.Trim();
        var phoneDigits = System.Text.RegularExpressions.Regex.Replace(phoneVal, @"[^\d+]", "");
        var phoneLink = "tel:" + phoneDigits;

        if (ctx.FenixLaw.RequiresLegalWork && ctx.FenixLaw.ServiceCards.Count > 0)
        {
            sb.AppendLine(@"
#text(font: serif, size: 11pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[Для вашего проекта Fenix Law может помочь с:]
#v(0.2cm)
#grid(
  columns: (1fr, 1fr),
  gutter: 12pt,
");
            foreach (var card in ctx.FenixLaw.ServiceCards)
            {
                sb.AppendLine($@"
  card(fill: rgb(""#0D1628""), inset: 11pt)[
    #text(font: serif, size: 10pt, weight: ""bold"", fill: rgb(""#E5C07B""))[{EscapeTypst(card.Title)}]
    #v(4pt)
    #text(font: sans, size: 8pt, fill: rgb(""#E2E8F0""))[{EscapeTypst(card.Description)}]
  ],");
            }
            sb.AppendLine(@"
)
");
        }
        else
        {
            sb.AppendLine(@"
#card(fill: rgb(""#0D1628""), inset: 11pt)[
  #text(font: sans, size: 8.5pt, fill: rgb(""#94A3B8""))[Все выявленные рекомендации носят характер базовой правовой гигиены и могут быть закрыты силами команды без привлечения внешних юристов.]
]
");
        }

        sb.AppendLine($@"
#v(0.4cm)
#card(fill: rgb(""#0D1628""), stroke: rgb(""#E5C07B""), inset: 13pt)[
  #text(font: serif, size: 10.5pt, weight: ""bold"", fill: rgb(""#E5C07B""))[СВЯЗАТЬСЯ С FENIX LAW]
  #v(3pt)
  #text(font: sans, size: 8.5pt, fill: rgb(""#94A3B8""))[Для обсуждения устранения выявленных рисков, подготовки документов и индивидуального сопровождения:]
  #v(0.35cm)
  #grid(
    columns: (1fr, 1fr, 1fr),
    gutter: 12pt,
    [
      #text(font: sans, size: 7.5pt, fill: rgb(""#94A3B8""))[Телеграм]\
      #v(2pt)
      #link(""{tgLink}"")[#text(font: sans, size: 9pt, weight: ""bold"", fill: rgb(""#38BDF8""))[{EscapeTypst(tgUser)}]]
    ],
    [
      #text(font: sans, size: 7.5pt, fill: rgb(""#94A3B8""))[Сайт]\
      #v(2pt)
      #link(""{webLink}"")[#text(font: sans, size: 9pt, weight: ""bold"", fill: rgb(""#E2E8F0""))[{EscapeTypst(webUrl)}]]
    ],
    [
      #text(font: sans, size: 7.5pt, fill: rgb(""#94A3B8""))[Телефон]\
      #v(2pt)
      #link(""{phoneLink}"")[#text(font: sans, size: 9pt, weight: ""bold"", fill: rgb(""#E2E8F0""))[{EscapeTypst(phoneVal)}]]
    ]
  )
]
");
        sb.AppendLine("#pagebreak()\n");

        // =========================================================================
        // SECTION N+5: Methodology & How to Read Score (Editorial Decomposition)
        // =========================================================================
        sb.AppendLine(@"
#section-header(""" + secNum++.ToString("D2") + @""", ""КАК ЧИТАТЬ ОЦЕНКУ"", category: ""Справочный блок"")
#v(0.3cm)

#text(font: serif, size: 11pt, weight: ""bold"", fill: rgb(""#E5C07B""))[КАК ФОРМИРУЕТСЯ РЕЗУЛЬТАТ]
#v(4pt)
#text(font: sans, size: 9pt, fill: rgb(""#CBD5E1""))[Оценка первичного юридического скрининга Fenix SLS формируется детерминированным экспертным движком на основании предоставленных вами ответов о конфигурации компании.]

#v(0.55cm)
#text(font: serif, size: 11pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[ШКАЛА ОЦЕНКИ И ДИАПАЗОНЫ ГОТОВНОСТИ]
#v(0.25cm)

#grid(
  columns: (1fr, 1fr, 1fr, 1fr),
  gutter: 10pt,
  rect(width: 100%, fill: rgb(""#0D1628""), stroke: 0.75pt + rgb(""#1E2D4A""), radius: 6pt, inset: (x: 10pt, y: 11pt))[
    #line(length: 100%, stroke: 2pt + rgb(""#34D399""))
    #v(5pt)
    #block(height: 22pt)[
      #text(font: serif, size: 15pt, weight: ""bold"", fill: rgb(""#34D399""))[80–100]
    ]
    #v(3pt)
    #block(height: 18pt)[
      #text(font: sans, size: 8.5pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[Хорошая готовность]
    ]
    #v(4pt)
    #block(height: 48pt)[
      #align(top)[
        #text(font: sans, size: 7.5pt, fill: rgb(""#94A3B8""))[Базовая юридическая конструкция в целом выстроена; остаются отдельные вопросы для поддержания готовности.]
      ]
    ]
  ],
  rect(width: 100%, fill: rgb(""#0D1628""), stroke: 0.75pt + rgb(""#1E2D4A""), radius: 6pt, inset: (x: 10pt, y: 11pt))[
    #line(length: 100%, stroke: 2pt + rgb(""#FBBF24""))
    #v(5pt)
    #block(height: 22pt)[
      #text(font: serif, size: 15pt, weight: ""bold"", fill: rgb(""#FBBF24""))[60–79]
    ]
    #v(3pt)
    #block(height: 18pt)[
      #text(font: sans, size: 8.5pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[Требует внимания]
    ]
    #v(4pt)
    #block(height: 48pt)[
      #align(top)[
        #text(font: sans, size: 7.5pt, fill: rgb(""#94A3B8""))[Базовые элементы юридической конструкции сформированы, но отдельные направления требуют доработки.]
      ]
    ]
  ],
  rect(width: 100%, fill: rgb(""#0D1628""), stroke: 0.75pt + rgb(""#1E2D4A""), radius: 6pt, inset: (x: 10pt, y: 11pt))[
    #line(length: 100%, stroke: 2pt + rgb(""#FB923C""))
    #v(5pt)
    #block(height: 22pt)[
      #text(font: serif, size: 15pt, weight: ""bold"", fill: rgb(""#FB923C""))[40–59]
    ]
    #v(3pt)
    #block(height: 18pt)[
      #text(font: sans, size: 8.5pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[Существенные пробелы]
    ]
    #v(4pt)
    #block(height: 48pt)[
      #align(top)[
        #text(font: sans, size: 7.5pt, fill: rgb(""#94A3B8""))[Обнаружены существенные пробелы в юридической конструкции, требующие последовательного устранения.]
      ]
    ]
  ],
  rect(width: 100%, fill: rgb(""#0D1628""), stroke: 0.75pt + rgb(""#1E2D4A""), radius: 6pt, inset: (x: 10pt, y: 11pt))[
    #line(length: 100%, stroke: 2pt + rgb(""#F87171""))
    #v(5pt)
    #block(height: 22pt)[
      #text(font: serif, size: 15pt, weight: ""bold"", fill: rgb(""#F87171""))[0–39]
    ]
    #v(3pt)
    #block(height: 18pt)[
      #text(font: sans, size: 8.5pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[Критические пробелы]
    ]
    #v(4pt)
    #block(height: 48pt)[
      #align(top)[
        #text(font: sans, size: 7.5pt, fill: rgb(""#94A3B8""))[Системные пробелы в юридической готовности, требующие первоочередной проработки.]
      ]
    ]
  ]
)

#v(0.65cm)
#text(font: serif, size: 11pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[ПРИНЦИПЫ РАСЧЕТА И ПРОЗРАЧНОСТИ]
#v(0.35cm)

#grid(
  columns: (1fr, 1fr, 1fr),
  gutter: 14pt,
  [
    #text(font: serif, size: 15pt, weight: ""bold"", fill: rgb(""#E5C07B""))[01]
    #v(2pt)
    #text(font: sans, size: 8.5pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[ДЕТЕРМИНИРОВАННОСТЬ]
    #v(4pt)
    #text(font: sans, size: 8pt, fill: rgb(""#CBD5E1""))[Все оценки, уровни риска и приоритеты рассчитываются по строгим правилам без субъективных оценок.]
  ],
  [
    #text(font: serif, size: 15pt, weight: ""bold"", fill: rgb(""#E5C07B""))[02]
    #v(2pt)
    #text(font: sans, size: 8.5pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[СРЕДНЕВЗВЕШЕННЫЙ БАЛЛ]
    #v(4pt)
    #text(font: sans, size: 8pt, fill: rgb(""#CBD5E1""))[Общий балл формируется как сумма оценок применимых направлений с учетом их веса в бизнес-модели.]
  ],
  [
    #text(font: serif, size: 15pt, weight: ""bold"", fill: rgb(""#E5C07B""))[03]
    #v(2pt)
    #text(font: sans, size: 8.5pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[ИСКЛЮЧЕНИЕ НЕПРИМЕНИМЫХ ЗОН]
    #v(4pt)
    #text(font: sans, size: 8pt, fill: rgb(""#CBD5E1""))[Если направление неприменимо к текущей стадии проекта, оно полностью исключается из расчета и не снижает итоговую оценку.]
  ]
)
#pagebreak()
");

        // =========================================================================
        // SECTION N+6: Legal Terms & Disclaimer + Fenix Law Expertise Block
        // =========================================================================
        var fenixLawLogoSnippet = File.Exists(Path.Combine(_contentRootPath, "wwwroot", "img", "fenix_law_crest.png"))
            ? "#image(\"/wwwroot/img/fenix_law_crest.png\", width: 1.3cm)"
            : "#text(font: serif, size: 12pt, weight: \"bold\", fill: rgb(\"#E5C07B\"))[FENIX LAW]";

        var sec14Num = secNum++.ToString("D2");
        sb.AppendLine($@"
#section-header(""{sec14Num}"", ""УСЛОВИЯ И ОГОВОРКА"", category: ""Правовая оговорка"")
#v(0.25cm)

#text(font: serif, size: 10pt, fill: rgb(""#CBD5E1""))[Настоящий отчет сформирован Fenix SLS на основании исключительно тех сведений и ответов, которые были предоставлены пользователем в процессе скрининга.]

#v(0.45cm)

#grid(
  columns: (auto, 1fr),
  gutter: 14pt,
  align: (top, top),
  [#text(font: serif, size: 15pt, weight: ""bold"", fill: rgb(""#E5C07B""))[01]],
  [
    #text(font: serif, size: 11pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[ХАРАКТЕР ДОКУМЕНТА]
    #v(3pt)
    #text(font: sans, size: 8.5pt, fill: rgb(""#E2E8F0""))[Скрининг носит предварительный информационно-аналитический характер и предназначен для первичной диагностики юридической структуры и выявления типовых рисков.]
  ]
)

#v(0.3cm)
#line(length: 100%, stroke: 0.5pt + rgb(""#1E2D4A""))
#v(0.3cm)

#grid(
  columns: (auto, 1fr),
  gutter: 14pt,
  align: (top, top),
  [#text(font: serif, size: 15pt, weight: ""bold"", fill: rgb(""#E5C07B""))[02]],
  [
    #text(font: serif, size: 11pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[ОГРАНИЧЕНИЯ И ПРЕДЕЛЫ ПРИМЕНИМОСТИ]
    #v(3pt)
    #text(font: sans, size: 8.5pt, fill: rgb(""#E2E8F0""))[Настоящий отчет не является официальным юридическим заключением (Legal Opinion), аудиторским отчетом или исчерпывающим заключением юридической проверки (Due Diligence). Выводы и рекомендации сформированы без изучения оригиналов правоустанавливающих документов, договоров, судебных реестров и фактических обстоятельств, не отраженных в ответах.]
  ]
)

#v(0.3cm)
#line(length: 100%, stroke: 0.5pt + rgb(""#1E2D4A""))
#v(0.3cm)

#grid(
  columns: (auto, 1fr),
  gutter: 14pt,
  align: (top, top),
  [#text(font: serif, size: 15pt, weight: ""bold"", fill: rgb(""#E5C07B""))[03]],
  [
    #text(font: serif, size: 11pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[РЕКОМЕНДАЦИИ ПО ИСПОЛЬЗОВАНИЮ]
    #v(3pt)
    #text(font: sans, size: 8.5pt, fill: rgb(""#E2E8F0""))[Отчет предназначен для внутреннего планирования основателей и определения приоритетов при подготовке к раундам финансирования, масштабированию и корпоративному структурированию. Для совершения юридически значимых действий, заключения сделок или разрешения спорных ситуаций рекомендуется индивидуальная правовая экспертиза специалистами Fenix Law.]
  ]
)

#v(0.5cm)
#line(length: 100%, stroke: 0.75pt + rgb(""#1E2D4A""))
#v(0.35cm)

#text(font: sans, size: 7.5pt, fill: rgb(""#E5C07B""), tracking: 1.5pt, weight: ""bold"")[ОБ ЭКСПЕРТИЗЕ]
#v(2pt)
#text(font: serif, size: 11.5pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[ЭКСПЕРТИЗА FENIX LAW В ОСНОВЕ СИСТЕМЫ]
#v(4pt)
#text(font: sans, size: 8pt, fill: rgb(""#CBD5E1""))[FENIX SLS создан на базе реальной практики FENIX LAW — бутиковой юридической фирмы по сопровождению технологических компаний, основателей и инвестиционных сделок.\
\
В основе системы настоящие юридические ситуации, с которыми сталкиваются стартапы и растущие компании: структура, права на продукт, команда, данные и подготовка к инвестициям. Этот опыт преобразован в системную диагностику, которая помогает увидеть слабые места бизнеса до того, как они станут проблемой.]

#v(0.35cm)
#grid(
  columns: (auto, 1fr, auto),
  gutter: 12pt,
  align: horizon,
  [
    {fenixLawLogoSnippet}
  ],
  [
    #text(font: serif, size: 10pt, weight: ""bold"", fill: rgb(""#E5C07B""))[FENIX LAW]
    #v(1pt)
    #text(font: sans, size: 7.5pt, fill: rgb(""#94A3B8""))[Legal expertise behind FENIX SLS]
  ],
  [
    #align(right)[
      #text(font: sans, size: 7.5pt, fill: rgb(""#94A3B8""))[
        #link(""{tgLink}"")[#text(fill: rgb(""#38BDF8""), weight: ""bold"")[{EscapeTypst(tgUser)}]] ·
        #link(""{webLink}"")[#text(fill: rgb(""#CBD5E1""))[{EscapeTypst(webUrl)}]] ·
        #link(""{phoneLink}"")[#text(fill: rgb(""#CBD5E1""))[{EscapeTypst(phoneVal)}]]
      ]
    ]
  ]
)
");

        return sb.ToString();
    }

    private static string GetScoreColor(int score)
    {
        return score >= 80 ? "#34D399"
            : score >= 60 ? "#FBBF24"
            : score >= 40 ? "#FB923C"
            : "#F87171";
    }

    private static string EscapeTypst(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("#", "\\#")
            .Replace("$", "\\$")
            .Replace("@", "\\@");
    }
}
