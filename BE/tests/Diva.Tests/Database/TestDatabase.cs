using Diva.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Diva.Tests.Database;

/// <summary>
/// A throwaway PostgreSQL database with every migration applied. The server comes from the
/// DIVA_TEST_DB environment variable (a connection string to an admin database, e.g. "postgres").
/// Each instance creates its own database named diva_test_xxx and drops it again when disposed,
/// so it must NEVER point at Supabase or any database holding real data.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    public const string EnvVar = "DIVA_TEST_DB";

    private readonly string _adminConnectionString;
    private readonly string _name;

    private TestDatabase(string adminConnectionString, string name, string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        _name = name;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static string? AdminConnectionString =>
        Environment.GetEnvironmentVariable(EnvVar) is { Length: > 0 } value ? value : null;

    public static async Task<TestDatabase> CreateAsync()
    {
        var admin = AdminConnectionString
            ?? throw new InvalidOperationException($"Set {EnvVar} to run database tests.");

        var name = $"diva_test_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString;
        var database = new TestDatabase(admin, name, connectionString);

        // Same migrations that production uses, so these tests also prove the migrations apply cleanly.
        await using var db = database.CreateDbContext();
        await db.Database.MigrateAsync();

        return database;
    }

    public DivaDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<DivaDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    public async ValueTask DisposeAsync()
    {
        // Pooled connections would keep the database "in use" and block the DROP.
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }
}
