using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.FileProviders;

// 1. Automatically load .env file if present
LoadDotEnv();

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
builder.Services.AddSingleton<AiReportService>();

var app = builder.Build();

// Init SQLite Database
var dbInit = app.Services.GetRequiredService<DbInitializer>();
dbInit.Initialize();

void LoadDotEnv()
{
    var searchPaths = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"),
        "/var/www/fenixlegalos/.env"
    };

    foreach (var path in searchPaths)
    {
        if (File.Exists(path))
        {
            Console.WriteLine($"[Env] Loading environment variables from {path}");
            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                    var idx = trimmed.IndexOf('=');
                    if (idx > 0)
                    {
                        var key = trimmed.Substring(0, idx).Trim();
                        var val = trimmed.Substring(idx + 1).Trim().Trim('"', '\'');
                        if (!string.IsNullOrEmpty(key))
                        {
                            Environment.SetEnvironmentVariable(key, val);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Env] Error reading {path}: {ex.Message}");
            }
            break;
        }
    }
}

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
