using System.Text.Json;
using Dapper;
using FenixLegalOs.Controllers;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Payments;
using FenixLegalOs.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public sealed class ScreeningLifecycleTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"fenix_screenings_{Guid.NewGuid():N}.db");
    private readonly DbInitializer _db;
    private readonly SessionRepository _sessions;
    private readonly UserRepository _users;
    private readonly LeadRepository _leads;

    public ScreeningLifecycleTests()
    {
        _db = new DbInitializer(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _path
        }).Build());
        _db.Initialize();
        _sessions = new SessionRepository(_db);
        _users = new UserRepository(_db);
        _leads = new LeadRepository(_db);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing-session")]
    [InlineData("foreign-session")]
    public void Registration_DoesNotCreateAnEmptyScreening(string? requestedSession)
    {
        string? foreignId = null;
        if (requestedSession == "foreign-session")
        {
            foreignId = _sessions.CreateSession("Other company", User().Id);
            requestedSession = foreignId;
        }
        int before = Count("sessions");
        var result = Register(requestedSession);
        Assert.Equal(JsonValueKind.Null, Json(result).GetProperty("sessionId").ValueKind);
        Assert.Equal(before, Count("sessions"));
        Assert.Equal(0, Count("leads"));
        Assert.Equal(1, Count("events"));
        Assert.Equal("user_registered", Query<string>("SELECT name FROM events"));
        if (foreignId != null) Assert.Equal("Other company", _sessions.GetSession(foreignId)!.CompanyName);
    }

    [Fact]
    public void RepeatedLoginWithoutSession_DoesNotCreateScreenings()
    {
        var user = User();
        var controller = Auth();
        for (int i = 0; i < 5; i++)
        {
            var result = Assert.IsType<OkObjectResult>(controller.Login(new(user.Email, "password123", null)));
            Assert.Equal(JsonValueKind.Null, Json(result).GetProperty("sessionId").ValueKind);
        }
        Assert.Equal(0, Count("sessions"));
        Assert.Equal(0, Count("leads"));
    }

    [Fact]
    public void Registration_AttachesExistingAnonymousScreeningWithoutLosingAnswers()
    {
        var id = _sessions.CreateSession("Independent project");
        _sessions.SaveAnswers(id, "{\"FND-C01\":\"solo\"}", "FND", "FND-C02");
        var result = Json(Register(id));
        Assert.Equal(id, result.GetProperty("sessionId").GetString());
        var stored = _sessions.GetSession(id)!;
        Assert.Equal(result.GetProperty("user").GetProperty("id").GetString(), stored.UserId);
        Assert.Equal("Independent project", stored.CompanyName);
        Assert.Contains("solo", stored.AnswersJson);
        Assert.Equal("FND-C02", stored.CurrentQuestionId);
        Assert.Equal(1, Count("sessions"));
    }

    [Fact]
    public void Login_PreservesOwnSessionButDoesNotAttachForeignOne()
    {
        var user = User();
        string own = _sessions.CreateSession("Own project", user.Id);
        string foreign = _sessions.CreateSession("Foreign project", User().Id);
        var foreignBefore = _sessions.GetSession(foreign)!;
        var controller = Auth();
        var ownResult = Json(Assert.IsType<OkObjectResult>(controller.Login(new(user.Email, "password123", own))));
        Assert.Equal(own, ownResult.GetProperty("sessionId").GetString());
        var otherResult = Json(Assert.IsType<OkObjectResult>(controller.Login(new(user.Email, "password123", foreign))));
        Assert.Equal(JsonValueKind.Null, otherResult.GetProperty("sessionId").ValueKind);
        Assert.Equal(foreignBefore.UserId, _sessions.GetSession(foreign)!.UserId);
        Assert.Equal(2, Count("sessions"));
    }

    [Fact]
    public void Start_PersistsConfirmedCompanyAndOwnerInsteadOfProfilePlaceholder()
    {
        var user = User("234321");
        var controller = Sessions(user);
        var created = Assert.IsType<OkObjectResult>(controller.CreateSession(new("  Fenix Law  ")));
        string id = Json(created).GetProperty("id").GetString()!;
        var stored = _sessions.GetSession(id)!;
        Assert.Equal("Fenix Law", stored.CompanyName);
        Assert.Equal(user.Id, stored.UserId);
        Assert.True(stored.TermsAccepted);
        Assert.Equal("234321", _users.GetUserById(user.Id)!.Company);
        Assert.Equal(1, Count("sessions"));
        Assert.Equal("Fenix Law", Query<string>("SELECT company FROM leads"));
        Assert.Equal("screening", Query<string>("SELECT type FROM leads"));
        Assert.Equal("diagnostic_started", Query<string>("SELECT name FROM events"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Start_RejectsMissingCompanyWithoutCreatingAnything(string? company)
    {
        var controller = Sessions(User());
        Assert.IsType<BadRequestObjectResult>(controller.CreateSession(new(company)));
        Assert.IsType<BadRequestObjectResult>(controller.CreateSession());
        Assert.Equal(0, Count("sessions"));
        Assert.Equal(0, Count("leads"));
    }

    [Fact]
    public void Start_RejectsOverlongCompanyButAllowsDigitsInRealNames()
    {
        var controller = Sessions(User());
        Assert.IsType<BadRequestObjectResult>(controller.CreateSession(new(new string('a', 151))));
        Assert.IsType<OkObjectResult>(controller.CreateSession(new("3M / 1С")));
        Assert.Equal(1, Count("sessions"));
    }

    [Fact]
    public void DifferentScreenings_KeepTheirCompaniesAfterProfileChanges()
    {
        var user = User("Original profile company");
        var first = _sessions.CreateSession("First company", user.Id);
        var second = _sessions.CreateSession("Second company", user.Id);
        using (var conn = Connection()) conn.Execute("UPDATE users SET company = 'Changed profile' WHERE id = @id", new { id = user.Id });
        var reports = _users.GetUserSessions(user.Id);
        Assert.Equal("First company", reports.Single(r => r.Id == first).CompanyName);
        Assert.Equal("Second company", reports.Single(r => r.Id == second).CompanyName);
        Assert.Equal("First company", _sessions.GetSession(first)!.CompanyName);
    }

    [Fact]
    public void History_HidesLegacyEmptyRegistrationSessionsWithoutDeletingThem()
    {
        var user = User();
        var empty = _sessions.CreateSession();
        _users.AttachUserToSession(empty, user.Id);
        var answered = _sessions.CreateSession();
        _users.AttachUserToSession(answered, user.Id);
        _sessions.SaveAnswers(answered, "{\"FND-C01\":\"solo\"}", "FND");
        var started = _sessions.CreateSession("Confirmed new company", user.Id);
        var completed = _sessions.CreateSession();
        _users.AttachUserToSession(completed, user.Id);
        _sessions.CompleteSession(completed, "{}", new ScoreResult { Overall = 50 });
        var paymentOnly = _sessions.CreateSession();
        _users.AttachUserToSession(paymentOnly, user.Id);
        new PaymentRepository(_db).Create(new Payment { SessionId = paymentOnly, OrderId = "LEGACY-ORDER", Provider = "bcc", AmountKzt = 49990 });
        var reports = _users.GetUserSessions(user.Id);
        Assert.DoesNotContain(reports, r => r.Id == empty);
        Assert.Contains(reports, r => r.Id == answered);
        Assert.Contains(reports, r => r.Id == started);
        Assert.Contains(reports, r => r.Id == completed);
        Assert.Contains(reports, r => r.Id == paymentOnly);
        Assert.Equal(5, Count("sessions"));
        Assert.NotNull(_sessions.GetSession(empty));
    }

    [Fact]
    public void Migration_IsAdditiveAndIdempotentAndPreservesLegacyData()
    {
        var user = User();
        string id = _sessions.CreateSession();
        _users.AttachUserToSession(id, user.Id);
        _sessions.CompleteSession(id, "{\"FND-C01\":\"solo\"}", new ScoreResult { Overall = 50 });
        byte[] pdf = "%PDF-test-old-report"u8.ToArray();
        _sessions.SavePdf(id, pdf);
        var payments = new PaymentRepository(_db);
        payments.Create(new Payment { SessionId = id, OrderId = "PAID-LEGACY", Provider = "bcc", AmountKzt = 49990 });
        payments.UpdateStatus("PAID-LEGACY", PaymentStatuses.Paid);
        var before = _sessions.GetSession(id)!;
        // This fixture's temporary database simulates the schema before the new column.
        using (var conn = Connection()) conn.Execute("ALTER TABLE sessions DROP COLUMN company_name");
        _db.Initialize();
        _db.Initialize();
        var after = _sessions.GetSession(id)!;
        Assert.Null(after.CompanyName);
        Assert.Equal(before.AnswersJson, after.AnswersJson);
        Assert.Equal(before.ResultJson, after.ResultJson);
        Assert.Equal(before.CompletedAt, after.CompletedAt);
        Assert.Equal(before.UserId, after.UserId);
        Assert.True(after.Paid);
        Assert.Equal(before.PaidAt, after.PaidAt);
        Assert.Equal(pdf, after.PdfBytes);
        Assert.Equal(PaymentStatuses.Paid, payments.GetByOrderId("PAID-LEGACY")!.Status);
    }

    private UserAccount User(string company = "Profile company") => _users.CreateUser(
        $"test-{Guid.NewGuid():N}@example.com", "password123", "Test User", company, "CEO", "+77001234567");

    private AuthController Auth() => new(_users, _sessions, _leads)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private OkObjectResult Register(string? sessionId) => Assert.IsType<OkObjectResult>(Auth().Register(new(
        $"register-{Guid.NewGuid():N}@example.com", "password123", "Test User", "Profile company", "CEO", "+77001234567", sessionId, true)));

    private SessionsController Sessions(UserAccount user)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["Cookie"] = $"fenix_user_token={_users.CreateSessionToken(user.Id)}";
        return new SessionsController(_sessions, _leads, null!, null!, null!, new SettingsRepository(_db), null!, _users)
        { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    private static JsonElement Json(OkObjectResult result) => JsonSerializer.SerializeToElement(result.Value);
    private int Count(string table) => Query<int>($"SELECT COUNT(*) FROM {table}");
    private T Query<T>(string sql) { using var conn = Connection(); return conn.QuerySingle<T>(sql); }
    private SqliteConnection Connection() { var conn = new SqliteConnection(_db.ConnectionString); conn.Open(); return conn; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (string suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
    }
}
