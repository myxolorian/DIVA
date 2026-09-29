using Diva.Api.Auth;
using Diva.Api.Data;
using Diva.Api.Features.Account;
using Microsoft.EntityFrameworkCore;

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

var app = builder.Build();

if (app.Configuration.IsDevBypassEnabled())
{
    app.Logger.LogWarning(
        "{Setting} is ON: every request is treated as logged in as a dev user. Local development only!",
        AuthServiceCollectionExtensions.DevBypassSetting);
}

// Order matters: authentication (who are you?) must run before authorization (are you allowed in?).
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAccountEndpoints();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;
