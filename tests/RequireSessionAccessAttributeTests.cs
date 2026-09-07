using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FenixLegalOs.Data;
using FenixLegalOs.Infrastructure;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FenixLegalOs.Tests;

public class RequireSessionAccessAttributeTests
{
    private readonly SessionRepository _sessionRepo;
    private readonly UserRepository _userRepo;
    private readonly IServiceProvider _serviceProvider;

    public RequireSessionAccessAttributeTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_filter_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();

        var dbInit = new DbInitializer(config);
        dbInit.Initialize();

        _sessionRepo = new SessionRepository(dbInit);
        _userRepo = new UserRepository(dbInit);

        var services = new ServiceCollection();
        services.AddSingleton(_sessionRepo);
        services.AddSingleton(_userRepo);
        _serviceProvider = services.BuildServiceProvider();
    }

    private (ActionExecutingContext context, bool[] nextCalled) CreateFilterContext(
        string? sessionId,
        IServiceProvider? customServices = null,
        string? cookieToken = null,
        string? authHeader = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = customServices ?? _serviceProvider;

        if (!string.IsNullOrEmpty(cookieToken))
        {
            httpContext.Request.Headers["Cookie"] = $"fenix_user_token={cookieToken}";
        }
        if (!string.IsNullOrEmpty(authHeader))
        {
            httpContext.Request.Headers["Authorization"] = authHeader;
        }

        var routeData = new RouteData();
        if (sessionId != null)
        {
            routeData.Values["id"] = sessionId;
        }

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        var executingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object()
        );

        var nextCalled = new bool[] { false };
        return (executingContext, nextCalled);
    }

    [Fact(DisplayName = "1. P1: Fail-closed when SessionRepository or UserRepository is missing from services")]
    public async Task FailClosed_WhenServicesMissing_Returns500()
    {
        var emptyServices = new ServiceCollection().BuildServiceProvider();
        var (context, nextCalled) = CreateFilterContext("any-id", customServices: emptyServices);

        var filter = new RequireSessionAccessAttribute();
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled[0] = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.False(nextCalled[0]);
        var objResult = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objResult.StatusCode);
    }

    [Fact(DisplayName = "2. Missing session ID route param returns 400 BadRequest")]
    public async Task MissingSessionId_Returns400()
    {
        var (context, nextCalled) = CreateFilterContext(sessionId: null);

        var filter = new RequireSessionAccessAttribute();
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled[0] = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.False(nextCalled[0]);
        Assert.IsType<BadRequestObjectResult>(context.Result);
    }

    [Fact(DisplayName = "3. Non-existent session returns 404 NotFound")]
    public async Task NonExistentSession_Returns404()
    {
        var (context, nextCalled) = CreateFilterContext("non-existent-id");

        var filter = new RequireSessionAccessAttribute();
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled[0] = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.False(nextCalled[0]);
        Assert.IsType<NotFoundObjectResult>(context.Result);
    }

    [Fact(DisplayName = "4. Terms not accepted returns 403 terms_required")]
    public async Task TermsNotAccepted_Returns403()
    {
        var sessionId = _sessionRepo.CreateSession();
        // TermsAccepted defaults to false

        var (context, nextCalled) = CreateFilterContext(sessionId);

        var filter = new RequireSessionAccessAttribute();
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled[0] = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.False(nextCalled[0]);
        var res = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, res.StatusCode);
    }

    [Fact(DisplayName = "5. Owner mismatch without token returns 403 forbidden_session_owner")]
    public async Task OwnerMismatch_WithoutToken_Returns403()
    {
        var user = _userRepo.CreateUser("test1@test.com", "pass1234", "User 1", "Corp", "CEO", null);
        var sessionId = _sessionRepo.CreateSession();
        _userRepo.AttachUserToSession(sessionId, user.Id);

        var (context, nextCalled) = CreateFilterContext(sessionId);

        var filter = new RequireSessionAccessAttribute();
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled[0] = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.False(nextCalled[0]);
        var res = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, res.StatusCode);
    }

    [Fact(DisplayName = "6. Owner match with HttpOnly cookie token succeeds")]
    public async Task OwnerMatch_WithCookieToken_Succeeds()
    {
        var user = _userRepo.CreateUser("test2@test.com", "pass1234", "User 2", "Corp", "CEO", null);
        var sessionId = _sessionRepo.CreateSession();
        _userRepo.AttachUserToSession(sessionId, user.Id);
        var token = _userRepo.CreateSessionToken(user.Id);

        var (context, nextCalled) = CreateFilterContext(sessionId, cookieToken: token);

        var filter = new RequireSessionAccessAttribute();
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled[0] = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.True(nextCalled[0]);
        Assert.Null(context.Result);
        Assert.NotNull(context.HttpContext.Items["DiagnosticSession"]);
        Assert.NotNull(context.HttpContext.Items["AuthUser"]);
    }

    [Fact(DisplayName = "7. Payment required when session unpaid returns 403 payment_required")]
    public async Task PaymentRequired_WhenUnpaid_Returns403()
    {
        var user = _userRepo.CreateUser("test3@test.com", "pass1234", "User 3", "Corp", "CEO", null);
        var sessionId = _sessionRepo.CreateSession();
        _userRepo.AttachUserToSession(sessionId, user.Id);
        var token = _userRepo.CreateSessionToken(user.Id);

        var (context, nextCalled) = CreateFilterContext(sessionId, cookieToken: token);

        var filter = new RequireSessionAccessAttribute(requirePayment: true);
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled[0] = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.False(nextCalled[0]);
        var res = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, res.StatusCode);
    }

    [Fact(DisplayName = "8. Payment required when session paid succeeds")]
    public async Task PaymentRequired_WhenPaid_Succeeds()
    {
        var user = _userRepo.CreateUser("test4@test.com", "pass1234", "User 4", "Corp", "CEO", null);
        var sessionId = _sessionRepo.CreateSession();
        _userRepo.AttachUserToSession(sessionId, user.Id);
        _sessionRepo.MarkSessionPaid(sessionId, 14900, "kaspi_pay");
        var token = _userRepo.CreateSessionToken(user.Id);

        var (context, nextCalled) = CreateFilterContext(sessionId, cookieToken: token);

        var filter = new RequireSessionAccessAttribute(requirePayment: true);
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled[0] = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.True(nextCalled[0]);
        Assert.Null(context.Result);
    }

    [Fact(DisplayName = "9. SavePdf stores bytes in DB and GetPdf retrieves them permanently")]
    public void SavePdf_StoresBytesInDatabase_AndCanBeRetrieved()
    {
        var sessionId = _sessionRepo.CreateSession();
        var mockPdfBytes = System.Text.Encoding.UTF8.GetBytes("%PDF-1.7 mock pdf content for storage test");

        bool saved = _sessionRepo.SavePdf(sessionId, mockPdfBytes);
        Assert.True(saved);

        var retrieved = _sessionRepo.GetPdf(sessionId);
        Assert.NotNull(retrieved);
        Assert.Equal(mockPdfBytes, retrieved);

        var session = _sessionRepo.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.NotNull(session.PdfBytes);
        Assert.Equal(mockPdfBytes, session.PdfBytes);
        Assert.NotNull(session.PdfGeneratedAt);
    }

    [Fact(DisplayName = "10. DisallowCompleted returns 409 Conflict when session already completed")]
    public async Task DisallowCompleted_WhenSessionCompleted_Returns409Conflict()
    {
        var user = _userRepo.CreateUser("test5@test.com", "pass1234", "User 5", "Corp", "CEO", null);
        var sessionId = _sessionRepo.CreateSession();
        _userRepo.AttachUserToSession(sessionId, user.Id);
        var token = _userRepo.CreateSessionToken(user.Id);

        var mockResult = new ScoreResult { Overall = 75 };
        _sessionRepo.CompleteSession(sessionId, "{\"q1\":\"a\"}", mockResult);

        var (context, nextCalled) = CreateFilterContext(sessionId, cookieToken: token);

        var filter = new RequireSessionAccessAttribute(disallowCompleted: true);
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled[0] = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.False(nextCalled[0]);
        var res = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status409Conflict, res.StatusCode);
    }

    [Fact(DisplayName = "11. DisallowCompleted succeeds when session is not yet completed")]
    public async Task DisallowCompleted_WhenSessionNotCompleted_Succeeds()
    {
        var user = _userRepo.CreateUser("test6@test.com", "pass1234", "User 6", "Corp", "CEO", null);
        var sessionId = _sessionRepo.CreateSession();
        _userRepo.AttachUserToSession(sessionId, user.Id);
        var token = _userRepo.CreateSessionToken(user.Id);

        var (context, nextCalled) = CreateFilterContext(sessionId, cookieToken: token);

        var filter = new RequireSessionAccessAttribute(disallowCompleted: true);
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled[0] = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.True(nextCalled[0]);
        Assert.Null(context.Result);
    }

    [Fact(DisplayName = "12. P1: SessionRepository.SaveAnswers atomically rejects updates to completed session")]
    public void SessionRepository_SaveAnswers_RejectsCompletedSession()
    {
        var sessionId = _sessionRepo.CreateSession();
        var initialAnswers = "{\"FND-C01\":\"2\"}";
        _sessionRepo.SaveAnswers(sessionId, initialAnswers, "founders");

        var mockResult = new ScoreResult { Overall = 80 };
        bool completed = _sessionRepo.CompleteSession(sessionId, initialAnswers, mockResult);
        Assert.True(completed);

        // Attempt late/concurrent overwrite from old tab
        string modifiedAnswers = "{\"FND-C01\":\"3\"}";
        bool savedAfterCompletion = _sessionRepo.SaveAnswers(sessionId, modifiedAnswers, "founders");
        Assert.False(savedAfterCompletion);

        var curSession = _sessionRepo.GetSession(sessionId);
        Assert.NotNull(curSession);
        Assert.Equal(initialAnswers, curSession.AnswersJson);
    }

    [Fact(DisplayName = "13. P1: SessionRepository.CompleteSession atomically rejects re-completion")]
    public void SessionRepository_CompleteSession_RejectsRecompletion()
    {
        var sessionId = _sessionRepo.CreateSession();
        var initialAnswers = "{\"FND-C01\":\"2\"}";
        var firstResult = new ScoreResult { Overall = 80 };
        bool firstComplete = _sessionRepo.CompleteSession(sessionId, initialAnswers, firstResult);
        Assert.True(firstComplete);

        var secondResult = new ScoreResult { Overall = 50 };
        bool secondComplete = _sessionRepo.CompleteSession(sessionId, "{\"tampered\":true}", secondResult);
        Assert.False(secondComplete);

        var curSession = _sessionRepo.GetSession(sessionId);
        Assert.NotNull(curSession);
        Assert.Equal(initialAnswers, curSession.AnswersJson);
    }

    [Fact(DisplayName = "14. P2: SavePdf with overwrite=false preserves existing version")]
    public void SavePdf_WithoutOverwrite_PreservesExistingBytes()
    {
        var sessionId = _sessionRepo.CreateSession();
        var originalPdf = new byte[] { 1, 2, 3, 4, 5 };
        var duplicatePdf = new byte[] { 9, 9, 9 };

        bool firstSave = _sessionRepo.SavePdf(sessionId, originalPdf);
        Assert.True(firstSave);

        // Concurrent/duplicate save should not overwrite existing PDF
        bool secondSave = _sessionRepo.SavePdf(sessionId, duplicatePdf, overwrite: false);
        Assert.False(secondSave);

        var persisted = _sessionRepo.GetPdf(sessionId);
        Assert.Equal(originalPdf, persisted);
    }
}
