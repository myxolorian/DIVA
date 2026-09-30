using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Diva.Api.Auth;
using Diva.Api.Data;
using Diva.Api.Features.Account;
using Diva.Api.Features.Customers;
using Diva.Api.Features.Orders;
using Diva.Api.Features.Outlet;
using Diva.Api.Features.Receipts;
using Diva.Api.Features.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using QuestPDF.Infrastructure;

// QuestPDF is free for businesses under USD 1M yearly revenue (Community license).
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Supabase Postgres: use the Session Pooler connection string, supplied through
// user-secrets (dev) or the ConnectionStrings__Default env var (prod). Never commit it.
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:Default'. See README.md > Konfigurasi.");
}

builder.Services.AddDbContext<DivaDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

builder.Services.AddDivaAuthentication(builder.Configuration, builder.Environment);

// The clock as a service: endpoints ask it for "now", and tests can replace it.
builder.Services.AddSingleton(TimeProvider.System);

// Errors use the standard ProblemDetails JSON shape: { "title": ..., "status": ..., "errors": ... }.
builder.Services.AddProblemDetails();

// Enums travel as text ("Kg", not 0). Numbers are refused so a typo cannot pick a random unit.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

// Public receipt links need no login, so they are rate limited per IP address instead:
// someone trying random tokens gets 429 Too Many Requests after a few attempts.
var receiptLimit = builder.Configuration.GetValue(ReceiptEndpoints.RateLimitSetting, ReceiptEndpoints.DefaultRateLimitPerMinute);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ReceiptEndpoints.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = receiptLimit, Window = TimeSpan.FromMinutes(1) }));
});

// The frontend (FE/) is served by this API: in development straight from the repo's FE folder,
// in Docker from wwwroot (see Dockerfile). Setting Frontend:Path overrides it.
var frontendPath = Path.GetFullPath(Path.Combine(
    builder.Environment.ContentRootPath, builder.Configuration["Frontend:Path"] ?? "wwwroot"));

var app = builder.Build();

if (app.Configuration.IsDevBypassEnabled())
{
    app.Logger.LogWarning(
        "{Setting} is ON: every request is treated as logged in as a dev user. Local development only!",
        AuthServiceCollectionExtensions.DevBypassSetting);
}

if (!app.Environment.IsDevelopment())
{
    // Unexpected errors become a plain 500 ProblemDetails instead of leaking a stack trace.
    // (In Development the detailed error page is shown instead.)
    app.UseExceptionHandler();
}

// Empty error responses such as 401 or 404 get a ProblemDetails body too.
app.UseStatusCodePages();

// HTML, CSS and JS files of the frontend. They are public on purpose: every piece of data
// they show comes from the API, which checks the login itself.
if (Directory.Exists(frontendPath))
{
    var files = new PhysicalFileProvider(frontendPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
}

app.UseRateLimiter();

// Order matters: authentication (who are you?) must run before authorization (are you allowed in?).
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAccountEndpoints();
app.MapCustomerEndpoints();
app.MapServiceEndpoints();
app.MapOrderEndpoints();
app.MapOutletEndpoints();
app.MapReceiptEndpoints(frontendPath);

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;
