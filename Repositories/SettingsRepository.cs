using Dapper;
using Microsoft.Data.Sqlite;

namespace FenixLegalOs.Repositories;

public class PricingConfig
{
    public int PriceKzt { get; set; } = 19999;
    public int OldPriceKzt { get; set; } = 49990;
    public int ConsultationPriceKzt { get; set; } = 79900;
    public string Currency { get; set; } = "₸";
    public int DiscountPercent => OldPriceKzt > PriceKzt ? (int)Math.Round((1.0 - (double)PriceKzt / OldPriceKzt) * 100) : 0;
}

public class CompanyContactsConfig
{
    public string Telegram { get; set; } = "@fenixlaw";
    public string Website { get; set; } = "www.fenixlaw.org";
    public string Phone { get; set; } = "+7-700-559-1377";
    public string Email { get; set; } = "team@fenixlaw.org";
}

public class SettingsRepository
{
    private readonly DbInitializer _db;

    public SettingsRepository(DbInitializer db)
    {
        _db = db;
    }

    private SqliteConnection GetConn()
    {
        var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        return conn;
    }

    public string? Get(string key, string? defaultValue = null)
    {
        using var conn = GetConn();
        var val = conn.QuerySingleOrDefault<string>("SELECT value FROM system_settings WHERE key = @key", new { key });
        return val ?? defaultValue;
    }

    public void Set(string key, string value)
    {
        using var conn = GetConn();
        var now = DateTime.UtcNow.ToString("o");
        conn.Execute(@"
            INSERT INTO system_settings (key, value, updated_at) 
            VALUES (@key, @value, @now)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at
        ", new { key, value, now });
    }

    public PricingConfig GetPricing()
    {
        var pStr = Get("report_price_kzt", "49990");
        var oStr = Get("report_old_price_kzt", "49990");
        var cStr = Get("consultation_price_kzt", "79900");

        int price = int.TryParse(pStr, out var p) ? p : 49990;
        int oldPrice = int.TryParse(oStr, out var o) ? o : 49990;
        int consultationPrice = int.TryParse(cStr, out var c) ? c : 79900;

        return new PricingConfig
        {
            PriceKzt = price,
            OldPriceKzt = oldPrice,
            ConsultationPriceKzt = consultationPrice,
            Currency = "₸"
        };
    }

    public void UpdatePricing(int priceKzt, int oldPriceKzt, int consultationPriceKzt)
    {
        Set("report_price_kzt", priceKzt.ToString());
        Set("report_old_price_kzt", oldPriceKzt.ToString());
        Set("consultation_price_kzt", consultationPriceKzt.ToString());
    }

    public CompanyContactsConfig GetContacts()
    {
        return new CompanyContactsConfig
        {
            Telegram = Get("contact_telegram", "@fenixlaw") ?? "@fenixlaw",
            Website = Get("contact_website", "www.fenixlaw.org") ?? "www.fenixlaw.org",
            Phone = Get("contact_phone", "+7-700-559-1377") ?? "+7-700-559-1377",
            Email = Get("contact_email", "team@fenixlaw.org") ?? "team@fenixlaw.org"
        };
    }

    public void UpdateContacts(string telegram, string website, string phone, string email)
    {
        Set("contact_telegram", telegram);
        Set("contact_website", website);
        Set("contact_phone", phone);
        Set("contact_email", email);
    }
}
