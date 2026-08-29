using FenixLegalOs.Models;

namespace FenixLegalOs.Scoring.Core;

public class ConsultingEvaluator
{
    public static ConsultingRecommendation Calculate(
        List<RiskFinding> findings,
        SharedFactStore facts,
        int overallScore)
    {
        int opportunityScore = 30;
        if (findings.Any(f => f.Severity == "BLOCKER")) opportunityScore += 25;
        if (findings.Any(f => f.Severity == "CRITICAL")) opportunityScore += 20;

        string primary = "FULL_LEGAL_ARCHITECTURE";
        string primaryCta = "Провести полный юридический аудит компании";
        string? secondary = null;
        string? secondaryCta = null;

        var topFinding = findings.FirstOrDefault(f => !string.IsNullOrEmpty(f.ServiceCode));
        if (topFinding != null && !string.IsNullOrEmpty(topFinding.ServiceCode))
        {
            primary = topFinding.ServiceCode;
            primaryCta = topFinding.Cta ?? GetServiceCta(topFinding.ServiceCode);
            secondary = "FULL_LEGAL_ARCHITECTURE";
            secondaryCta = "Провести полный юридический аудит компании";
        }

        return new ConsultingRecommendation
        {
            PrimaryServiceCode = primary,
            PrimaryCta = primaryCta,
            SecondaryServiceCode = secondary,
            SecondaryCta = secondaryCta,
            ConsultingOpportunityScore = opportunityScore
        };
    }

    public static string GetServiceCta(string serviceCode)
    {
        return serviceCode switch
        {
            "CORP_STRUCT_KZ" => "Заказать разработку устава и документов для ТОО в Казахстане",
            "FOUNDERS_AGREEMENT" => "Разработать соглашение сооснователей (Founders' Agreement / SHA)",
            "IP_ASSIGNMENT_PACK" => "Оформить пакет документов по передаче интеллектуальной собственности (IP Assignment)",
            "IP_AUDIT" => "Провести аудит интеллектуальной собственности и цепочек прав",
            _ => "Получить консультацию юриста по устранению выявленных рисков"
        };
    }
}
