using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Register Controllers
builder.Services.AddControllers();

// Register Services & Repositories
builder.Services.AddSingleton<DbInitializer>();
builder.Services.AddSingleton<QuestionRepository>();
builder.Services.AddSingleton<SessionRepository>();
builder.Services.AddSingleton<LeadRepository>();
builder.Services.AddSingleton<ScoringEngine>();
builder.Services.AddSingleton<TypstPdfService>();

var app = builder.Build();

// Init SQLite Database
var dbInit = app.Services.GetRequiredService<DbInitializer>();
dbInit.Initialize();

// Static Files Configuration (serving wwwroot directory)
var staticPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
if (Directory.Exists(staticPath))
{
    app.UseFileServer(new FileServerOptions
    {
        FileProvider = new PhysicalFileProvider(staticPath),
        RequestPath = "",
        EnableDefaultFiles = true
    });
}
else
{
    app.UseStaticFiles();
}

app.UseRouting();

// Map Controller Endpoints
app.MapControllers();

// Admin HTML Route
app.MapGet("/admin", () =>
{
    var adminHtml = Path.Combine(staticPath, "admin.html");
    return File.Exists(adminHtml) ? Results.File(adminHtml, "text/html") : Results.NotFound();
});

var portStr = Environment.GetEnvironmentVariable("PORT") ?? "5050";
var url = $"http://0.0.0.0:{portStr}";

Console.WriteLine($"Fenix Legal OS (.NET C# + Controllers) -> http://localhost:{portStr}");
Console.WriteLine($"Admin -> http://localhost:{portStr}/admin");

app.Run(url);
