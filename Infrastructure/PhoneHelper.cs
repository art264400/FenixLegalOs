using System.Text.RegularExpressions;

namespace FenixLegalOs.Infrastructure;

/// <summary>
/// Утилиты для валидации, нормализации и форматирования номеров телефонов Казахстана (+7 7xx).
/// </summary>
public static class PhoneHelper
{
    /// <summary>
    /// Валидирует и нормализует казахстанский мобильный номер телефона в единый формат +77XXXXXXXXX.
    /// Разрешает только цифры, пробелы, скобки, дефисы и ведущий знак +.
    /// Принимает только казахстанские мобильные номера (+7 7XX).
    /// Отклоняет буквы, короткие, длинные и некорректные номера (включая российские +7 9XX).
    /// </summary>
    public static bool TryNormalizePhone(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string trimmed = input.Trim();

        // Проверяем, что строка содержит только разрешённые символы: цифры, пробелы, скобки, дефисы и ведущий +
        if (!Regex.IsMatch(trimmed, @"^\+?[\d\s\(\)\-]+$"))
        {
            return false;
        }

        string digits = Regex.Replace(trimmed, @"[^\d]", "");

        // Казахстанский мобильный номер состоит строго из 11 цифр и начинается с 77... или 87...
        if (digits.Length == 11 && (digits.StartsWith("77") || digits.StartsWith("87")))
        {
            normalized = "+7" + digits.Substring(1);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Извлекает компоненты казахстанского телефона для M_INFO 3-D Secure:
    /// cc = "7" (код страны) и subscriber = оставшиеся 10 цифр (начиная с 7).
    /// </summary>
    public static bool TryExtractMInfoPhone(string? phone, out string cc, out string subscriber)
    {
        cc = string.Empty;
        subscriber = string.Empty;
        if (string.IsNullOrWhiteSpace(phone)) return false;

        if (!TryNormalizePhone(phone, out string normalized))
        {
            return false;
        }

        string digits = Regex.Replace(normalized, @"[^\d]", "");
        if (digits.Length == 11 && digits.StartsWith("77"))
        {
            cc = "7";
            subscriber = digits.Substring(1); // 10 цифр: 7001234567
            return true;
        }

        return false;
    }
}
