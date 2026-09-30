using System.Net.Http.Headers;
using Diva.Api.Data;
using Diva.Tests.Auth;

namespace Diva.Tests.Database;

/// <summary>
/// The real API in memory, connected to its own fresh PostgreSQL database. One instance is shared by
/// all tests in a class (IClassFixture), so tests use unique names instead of assuming an empty table.
/// </summary>
public sealed class ApiWithDatabaseFixture : IAsyncLifetime
{
    private TestDatabase? _database;
    private DivaApiFactory? _factory;

    private DivaApiFactory Factory => _factory
        ?? throw new InvalidOperationException($"{TestDatabase.EnvVar} is not set; use [PostgresFact].");

    public async Task InitializeAsync()
    {
        // xUnit builds fixtures even when every test is skipped, so do nothing without a server.
        if (TestDatabase.AdminConnectionString is null)
        {
            return;
        }

        _database = await TestDatabase.CreateAsync();
        _factory = new DivaApiFactory(
            "Development",
            new Dictionary<string, string?> { ["ConnectionStrings:Default"] = _database.ConnectionString });
    }

    /// <summary>An HttpClient that is logged in as the owner.</summary>
    public HttpClient CreateClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.Jwt.CreateToken());
        return client;
    }

    public HttpClient CreateAnonymousClient() => Factory.CreateClient();

    /// <summary>Direct database access, for checking what the API actually stored.</summary>
    public DivaDbContext CreateDbContext() =>
        (_database ?? throw new InvalidOperationException($"{TestDatabase.EnvVar} is not set.")).CreateDbContext();

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }
}
