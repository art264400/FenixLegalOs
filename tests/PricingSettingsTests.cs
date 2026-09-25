using Dapper;
using FenixLegalOs.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public sealed class PricingSettingsTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fenix_pricing_{Guid.NewGuid():N}.db");
    private readonly DbInitializer _db;

    public PricingSettingsTests()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _dbPath
        }).Build();
        _db = new DbInitializer(config);
    }

    [Fact]
    public void FreshDatabase_UsesPublishedPricesWithoutDiscount()
    {
        _db.Initialize();

        var pricing = new SettingsRepository(_db).GetPricing();

        Assert.Equal(49990, pricing.PriceKzt);
        Assert.Equal(49990, pricing.OldPriceKzt);
        Assert.Equal(90990, pricing.ConsultationPriceKzt);
        Assert.Equal(0, pricing.DiscountPercent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(79900)]
    public void Startup_PreservesConfiguredPrices_AndSeedsOnlyMissingConsultation(int? consultationPrice)
    {
        _db.Initialize();
        var settings = new SettingsRepository(_db);
        settings.UpdatePricing(35000, 42000, consultationPrice ?? 90990);

        if (consultationPrice == null)
        {
            using var conn = new SqliteConnection(_db.ConnectionString);
            conn.Open();
            conn.Execute("DELETE FROM system_settings WHERE key = 'consultation_price_kzt'");
        }

        _db.Initialize();

        var pricing = settings.GetPricing();
        Assert.Equal(35000, pricing.PriceKzt);
        Assert.Equal(42000, pricing.OldPriceKzt);
        Assert.Equal(consultationPrice ?? 90990, pricing.ConsultationPriceKzt);
    }

    public void Dispose()
    {
        using var conn = new SqliteConnection(_db.ConnectionString);
        SqliteConnection.ClearPool(conn);
        File.Delete(_dbPath);
        File.Delete(_dbPath + "-wal");
        File.Delete(_dbPath + "-shm");
    }
}
