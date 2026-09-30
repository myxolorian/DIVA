namespace Diva.Tests.Database;

/// <summary>
/// A [Fact] that needs a real PostgreSQL server. Without the DIVA_TEST_DB environment variable it
/// is reported as skipped (with the reason) instead of failing, so `dotnet test` stays green on a
/// laptop without PostgreSQL. CI sets the variable, so there these tests always run.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (TestDatabase.AdminConnectionString is null)
        {
            Skip = $"Butuh PostgreSQL: set env var {TestDatabase.EnvVar} (lihat README > Tes).";
        }
    }
}
