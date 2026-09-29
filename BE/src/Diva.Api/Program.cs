using Diva.Api.Data;
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

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;
