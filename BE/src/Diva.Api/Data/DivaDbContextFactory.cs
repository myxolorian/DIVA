using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Diva.Api.Data;

/// <summary>
/// Used only by `dotnet ef` at design time. Reads DIVA_DB_CONNECTION when set (for
/// `database update`); otherwise falls back to a placeholder, which is enough to add migrations
/// because that never opens a connection.
/// </summary>
public class DivaDbContextFactory : IDesignTimeDbContextFactory<DivaDbContext>
{
    public DivaDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("DIVA_DB_CONNECTION")
            ?? "Host=localhost;Database=diva;Username=postgres;Password=design-time-only";

        var options = new DbContextOptionsBuilder<DivaDbContext>()
            .UseNpgsql(connection)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new DivaDbContext(options);
    }
}
