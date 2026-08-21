using System.Diagnostics;
using System.Text;
using FenixLegalOs.Models;

namespace FenixLegalOs.Services;

public class TypstPdfService
{
    private readonly string _contentRootPath;
    private readonly string _templatePath;
    private readonly string _typstBinaryPath;

    public TypstPdfService(IWebHostEnvironment env)
    {
        _contentRootPath = env.ContentRootPath;
        _templatePath = Path.Combine(env.ContentRootPath, "Templates", "report_template.typ");
        _typstBinaryPath = Path.Combine(env.ContentRootPath, "typst.exe");
    }

    public async Task<byte[]?> GeneratePdfAsync(ScoreResult result, string companyName = "Стартап")
    {
        var tempFolder = Path.Combine(_contentRootPath, "Templates");
        Directory.CreateDirectory(tempFolder);

        var logoInTemplates = Path.Combine(tempFolder, "logo.png");
        if (!File.Exists(logoInTemplates))
        {
            var srcLogo = Path.Combine(_contentRootPath, "wwwroot", "img", "logo.png");
            if (File.Exists(srcLogo))
            {
                File.Copy(srcLogo, logoInTemplates, true);
            }
        }

        var tempTypFile = Path.Combine(tempFolder, $"temp_report_{Guid.NewGuid():N}.typ");
        var tempPdfFile = Path.Combine(tempFolder, $"temp_report_{Guid.NewGuid():N}.pdf");

        try
        {
            var typstContent = BuildTypstMarkup(result, companyName);
            await File.WriteAllTextAsync(tempTypFile, typstContent, Encoding.UTF8);

            string binaryToUse = File.Exists(_typstBinaryPath) ? _typstBinaryPath : "typst";

            var psi = new ProcessStartInfo
            {
                FileName = binaryToUse,
                Arguments = $"compile --root \"{_contentRootPath}\" \"{tempTypFile}\" \"{tempPdfFile}\"",
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

    public string BuildTypstMarkup(ScoreResult result, string companyName)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"
#set page(
  paper: ""a4"",
  fill: rgb(""#0B0F16""),
  margin: (x: 1.8cm, top: 2.2cm, bottom: 2.2cm),
  header: context {
    if counter(page).get().first() > 1 [
      #grid(
        columns: (1fr, auto),
        align(left)[
          #text(size: 8.5pt, fill: rgb(""#59C2FF""), weight: ""bold"")[FENIX LAW]
          #text(size: 8.5pt, fill: rgb(""#6C7A8E""))[ · Юридический отчет диагностики v1.1]
        ],
        align(right)[
          #text(size: 8.5pt, fill: rgb(""#6C7A8E""))[Fenix Legal Score OS]
        ]
      )
      #v(-4pt)
      #line(length: 100%, stroke: 0.5pt + rgb(""#243042""))
    ]
  },
  footer: context [
    #line(length: 100%, stroke: 0.5pt + rgb(""#243042""))
    #v(2pt)
    #grid(
      columns: (1fr, auto),
      align(left)[
        #text(size: 8pt, fill: rgb(""#6C7A8E""))[Конфиденциально · Подготовлено Fenix Legal OS]
      ],
      align(right)[
        #text(size: 8pt, fill: rgb(""#8E9BAE""))[Страница #counter(page).display() из #counter(page).final().first()]
      ]
    )
  ]
)

#set text(font: (""Liberation Sans"", ""DejaVu Sans"", ""Roboto""), fill: rgb(""#E6EDF8""), size: 10pt)
#set par(justify: false, leading: 0.6em)
");

        // Header with Logo
        sb.AppendLine($@"
#grid(
  columns: (52pt, 1fr, auto),
  gutter: 14pt,
  align: (left + horizon, left + horizon, right + horizon),
  image(""logo.png"", width: 48pt),
  [
    #text(size: 22pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[Fenix Law] \
    #v(-2pt)
    #text(size: 9.5pt, weight: ""semibold"", fill: rgb(""#59C2FF""))[LEGAL TECH SMART SYSTEM · ЮРИДИЧЕСКАЯ ДИАГНОСТИКА v1.1]
  ],
  [
    #align(right)[
      #text(size: 8.5pt, fill: rgb(""#8E9BAE""))[Проект: {Sanitize(companyName)}] \
      #text(size: 8.5pt, fill: rgb(""#8E9BAE""))[Дата: {DateTime.Today:dd.MM.yyyy}] \
      #text(size: 8pt, fill: rgb(""#59C2FF""))[Официальный отчёт Fenix Law]
    ]
  ]
)

#v(8pt)
#line(length: 100%, stroke: 1.5pt + rgb(""#243042""))
#v(12pt)
");

        // Hero Score Box with Confidence Badge
        sb.AppendLine($@"
#rect(
  width: 100%,
  fill: rgb(""#141B26""),
  stroke: 1pt + rgb(""#243042""),
  radius: 10pt,
  inset: 18pt,
)[
  #grid(
    columns: (120pt, 1fr),
    gutter: 18pt,
    align: (center + horizon, left + horizon),
    [
      #text(size: 9pt, weight: ""bold"", fill: rgb(""#8E9BAE""))[LEGAL SCORE] \
      #v(2pt)
      #text(size: 38pt, weight: ""bold"", fill: rgb(""#FF5964""))[{result.Overall}#text(size: 20pt, fill: rgb(""#8E9BAE""))[/100]] \
      #v(4pt)
      #rect(fill: rgb(""#1C2B3A""), inset: (x: 6pt, y: 2pt), radius: 3pt)[#text(size: 7.5pt, weight: ""bold"", fill: rgb(""#59C2FF""))[Уверенность: {result.Confidence}%]]
    ],
    [
      #text(size: 16pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[{Sanitize(result.LevelTitle)}] \
      #v(4pt)
      #text(size: 9.5pt, fill: rgb(""#A0AEC0""))[{Sanitize(result.LevelText)}]
      #v(8pt)
      #grid(
        columns: (auto, auto, auto),
        gutter: 12pt,
        [#rect(fill: rgb(""#3D1A24""), inset: (x: 8pt, y: 4pt), radius: 4pt)[#text(size: 8.5pt, weight: ""bold"", fill: rgb(""#FF5964""))[• {result.CriticalCount} Критических]]],
        [#rect(fill: rgb(""#3D2B1A""), inset: (x: 8pt, y: 4pt), radius: 4pt)[#text(size: 8.5pt, weight: ""bold"", fill: rgb(""#FF9F43""))[• {result.HighCount} Высоких]]],
        [#rect(fill: rgb(""#38321A""), inset: (x: 8pt, y: 4pt), radius: 4pt)[#text(size: 8.5pt, weight: ""bold"", fill: rgb(""#F5A623""))[• {result.MediumCount} Умеренных]]]
      )
    ]
  )
]
");

        // 8 Key Sections Breakdown Grid
        if (result.Sections != null && result.Sections.Count > 0)
        {
            sb.AppendLine(@"
#v(16pt)
#text(size: 14pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[Оценка по 8 ключевым разделам]
#v(6pt)
#grid(
  columns: (1fr, 1fr),
  gutter: 10pt,
");
            int sectionIdx = 1;
            foreach (var s in result.Sections)
            {
                int score = s.Score ?? 0;
                bool isNa = s.Status == "N_A" || s.Score == null;
                string scoreColor = isNa ? "#8E9BAE" : score >= 75 ? "#2ED573" : score >= 50 ? "#FF9F43" : "#FF5964";
                string statusText = isNa ? "Не применимо" : score >= 75 ? "Устойчиво" : score >= 50 ? "В зоне внимания" : "Критический риск";
                string scoreLabel = isNa ? "—" : $"{score}%";

                sb.AppendLine($@"
  rect(width: 100%, fill: rgb(""#141B26""), stroke: 0.5pt + rgb(""#243042""), radius: 6pt, inset: 10pt)[
    #grid(columns: (1fr, auto), [ #text(weight: ""bold"")[{sectionIdx++}. {Sanitize(s.Title)}] ], [#text(fill: rgb(""{scoreColor}""), weight: ""bold"")[{scoreLabel}]])
    #v(4pt)
    #rect(width: 100%, height: 4pt, fill: rgb(""#243042""), radius: 2pt)[#rect(width: {Math.Max(4, isNa ? 0 : score)}%, height: 4pt, fill: rgb(""{scoreColor}""), radius: 2pt)]
    #v(2pt)
    #text(size: 8pt, fill: rgb(""#8E9BAE""))[Статус: {statusText}]
  ],");
            }
            sb.AppendLine(")\n");
        }

        // Strengths (if any)
        if (result.Strengths != null && result.Strengths.Count > 0)
        {
            sb.AppendLine(@"
#v(14pt)
#text(size: 14pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[Сильные стороны юридической конструкции]
#v(6pt)
#rect(
  width: 100%,
  fill: rgb(""#14241C""),
  stroke: 0.5pt + rgb(""#2ED573""),
  radius: 6pt,
  inset: 12pt,
)[
");
            foreach (var st in result.Strengths)
            {
                sb.AppendLine($"  #text(size: 9.5pt, fill: rgb(\"#2ED573\"))[• {Sanitize(st)}] \\");
            }
            sb.AppendLine("]\n");
        }

        // Complete Registry of Risks
        sb.AppendLine(@"
#v(16pt)
#text(size: 14pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[Полный реестр выявленных рисков и рекомендаций]
#v(6pt)
");

        if (result.Risks == null || result.Risks.Count == 0)
        {
            sb.AppendLine(@"
#rect(width: 100%, fill: rgb(""#141B26""), stroke: 0.5pt + rgb(""#243042""), radius: 6pt, inset: 14pt)[
  #text(size: 10pt, fill: rgb(""#2ED573""))[Существенных рисков по результатам ответа не выявлено.]
]
");
        }
        else
        {
            int idx = 1;
            foreach (var r in result.Risks)
            {
                var borderColor = r.Severity is "CRITICAL" or "critical" or "BLOCKER" ? "#FF5964" : r.Severity is "HIGH" or "high" ? "#FF9F43" : "#F5A623";
                var tagBg = r.Severity is "CRITICAL" or "critical" or "BLOCKER" ? "#3D1A24" : r.Severity is "HIGH" or "high" ? "#3D2B1A" : "#38321A";
                var tagText = r.Resolution switch
                {
                    "lawyer_required" => "ТРЕБУЕТСЯ ЮРИСТ",
                    "check_with_lawyer" => "ЖЕЛАТЕЛЬНО С ЮРИСТОМ",
                    _ => "САМОСТОЯТЕЛЬНО"
                };

                sb.AppendLine($@"
#rect(
  width: 100%,
  fill: rgb(""#141B26""),
  stroke: (left: 4pt + rgb(""{borderColor}""), rest: 0.5pt + rgb(""#243042"")),
  radius: (right: 6pt),
  inset: 14pt,
)[
  #grid(
    columns: (1fr, auto),
    [#text(weight: ""bold"", size: 11pt, fill: rgb(""#FFFFFF""))[{idx++}. {Sanitize(r.Title)}]],
    [#rect(fill: rgb(""{tagBg}""), inset: (x: 6pt, y: 2pt), radius: 3pt)[#text(size: 8pt, weight: ""bold"", fill: rgb(""{borderColor}""))[{tagText}]]]
  )
  #v(6pt)
  #text(size: 9pt, fill: rgb(""#A0AEC0""))[*Что обнаружено:* {Sanitize(r.Finding)}] \
  #text(size: 9pt, fill: rgb(""#A0AEC0""))[*Почему это важно:* {Sanitize(r.WhyItMatters)}] \
  #v(4pt)
  #text(size: 9.5pt, weight: ""medium"", fill: rgb(""#59C2FF""))[*Рекомендация по исправлению:* {Sanitize(r.Recommendation)}]
]
#v(10pt)
");
            }
        }

        // Dynamic Roadmap Section based on actual risks priority
        sb.AppendLine(@"
#v(16pt)
#text(size: 14pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[Пошаговая дорожная карта устранения рисков (Roadmap)]
#v(6pt)
#rect(
  width: 100%,
  fill: rgb(""#141B26""),
  stroke: 0.5pt + rgb(""#243042""),
  radius: 8pt,
  inset: 14pt,
)[
");

        var nowRisks = result.Risks?.Where(x => x.Priority == "NOW" || x.Severity is "CRITICAL" or "BLOCKER").ToList() ?? new();
        var soonRisks = result.Risks?.Where(x => x.Priority == "30_DAYS" || (x.Severity == "HIGH" && x.Priority != "NOW")).ToList() ?? new();
        var roundRisks = result.Risks?.Where(x => x.Priority == "BEFORE_ROUND" || (x.Severity == "MEDIUM" && x.Priority != "NOW" && x.Priority != "30_DAYS")).ToList() ?? new();

        sb.AppendLine("  #text(weight: \"bold\", fill: rgb(\"#FF5964\"))[1. Сделать прямо сейчас (Блокеры)] \\");
        sb.AppendLine("  #v(4pt)");
        if (nowRisks.Count > 0)
        {
            foreach (var cr in nowRisks)
            {
                sb.AppendLine($"  - {Sanitize(cr.Recommendation)} \\");
            }
        }
        else
        {
            sb.AppendLine("  - Оформить базовые уставные документы и распределение долей. \\");
        }

        sb.AppendLine("  #v(10pt)");
        sb.AppendLine("  #text(weight: \"bold\", fill: rgb(\"#FF9F43\"))[2. В течение 30 дней (Основа)] \\");
        sb.AppendLine("  #v(4pt)");
        if (soonRisks.Count > 0)
        {
            foreach (var hr in soonRisks)
            {
                sb.AppendLine($"  - {Sanitize(hr.Recommendation)} \\");
            }
        }
        else
        {
            sb.AppendLine("  - Разработать и разместить персонализированные Terms of Use и Privacy Policy. \\");
        }

        sb.AppendLine("  #v(10pt)");
        sb.AppendLine("  #text(weight: \"bold\", fill: rgb(\"#59C2FF\"))[3. Перед инвестиционным раундом (Data Room)] \\");
        sb.AppendLine("  #v(4pt)");
        if (roundRisks.Count > 0)
        {
            foreach (var mr in roundRisks)
            {
                sb.AppendLine($"  - {Sanitize(mr.Recommendation)} \\");
            }
        }
        sb.AppendLine("  - Сформировать готовый Data Room для инвесторов. \\");

        sb.AppendLine("]\n");

        // Consulting CTA Box
        var primaryCtaText = result.Consulting?.PrimaryCta ?? "Разбрать результаты с Fenix Law";
        sb.AppendLine($@"
#v(16pt)
#rect(
  width: 100%,
  fill: rgb(""#1C2433""),
  stroke: 1pt + rgb(""#59C2FF""),
  radius: 10pt,
  inset: 16pt,
)[
  #grid(
    columns: (1fr, auto),
    gutter: 14pt,
    [
      #text(size: 13pt, weight: ""bold"", fill: rgb(""#FFFFFF""))[Персональное юридическое сопровождение] \
      #v(4pt)
      #text(size: 9.5pt, fill: rgb(""#A0AEC0""))[
        Венчурный юрист *Нариман Исанов* и команда Fenix Law проведут сессию по вашей диагностике, помогут устранить критические уязвимости и подготовить бизнес к сделкам.
      ]
    ],
    [
      #align(right + horizon)[
        #rect(fill: rgb(""#59C2FF""), radius: 6pt, inset: (x: 14pt, y: 8pt))[
          #text(weight: ""bold"", fill: rgb(""#0B0F16""), size: 9.5pt)[{Sanitize(primaryCtaText)}]
        ]
      ]
    ]
  )
]
");

        return sb.ToString();
    }

    private string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("[", "\\[").Replace("]", "\\]").Replace("*", "\\*");
    }
}
