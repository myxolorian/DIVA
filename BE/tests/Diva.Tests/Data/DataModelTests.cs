using Diva.Api.Data;
using Diva.Api.Data.Migrations;
using Diva.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Diva.Tests.Data;

/// <summary>Checks the EF model and migrations without needing a database connection.</summary>
public class DataModelTests
{
    private static DivaDbContext CreateContext() => new(
        new DbContextOptionsBuilder<DivaDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseSnakeCaseNamingConvention()
            .Options);

    private static IEntityType Entity<T>(DivaDbContext db) => db.Model.FindEntityType(typeof(T))!;

    [Fact]
    public void Tables_use_snake_case_names()
    {
        using var db = CreateContext();

        var tables = db.Model.GetEntityTypes().Select(e => e.GetTableName()!).Order().ToArray();

        Assert.Equal(["customers", "order_items", "orders", "outlet_profile", "services"], tables);
    }

    [Fact]
    public void Order_number_and_public_token_are_unique()
    {
        using var db = CreateContext();
        var order = Entity<Order>(db);

        bool IsUnique(string property) => order.GetIndexes()
            .Any(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual([property]));

        Assert.True(IsUnique(nameof(Order.OrderNumber)));
        Assert.True(IsUnique(nameof(Order.PublicToken)));
    }

    [Fact]
    public void Enums_are_stored_as_text()
    {
        using var db = CreateContext();

        Assert.Equal("character varying(20)", Entity<Order>(db).FindProperty(nameof(Order.Status))!.GetColumnType());
        Assert.Equal("character varying(20)", Entity<Order>(db).FindProperty(nameof(Order.PaymentStatus))!.GetColumnType());
        Assert.Equal("character varying(10)", Entity<LaundryService>(db).FindProperty(nameof(LaundryService.Unit))!.GetColumnType());
    }

    [Fact]
    public void Money_and_quantity_use_fixed_precision()
    {
        using var db = CreateContext();

        Assert.Equal("numeric(12,2)", Entity<LaundryService>(db).FindProperty(nameof(LaundryService.Price))!.GetColumnType());
        Assert.Equal("numeric(12,2)", Entity<Order>(db).FindProperty(nameof(Order.Total))!.GetColumnType());
        Assert.Equal("numeric(8,2)", Entity<OrderItem>(db).FindProperty(nameof(OrderItem.Qty))!.GetColumnType());
    }

    [Fact]
    public void Customer_with_orders_cannot_be_hard_deleted()
    {
        using var db = CreateContext();

        var fk = Entity<Order>(db).GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(Customer));

        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Fact]
    public void Seed_data_has_default_services_and_one_outlet_profile()
    {
        using var db = CreateContext();

        // Seed data is only kept in the design-time model.
        var model = db.GetService<IDesignTimeModel>().Model;
        var services = model.FindEntityType(typeof(LaundryService))!.GetSeedData().ToList();
        var outlet = model.FindEntityType(typeof(OutletProfile))!.GetSeedData().Single();

        Assert.Equal(6, services.Count);
        Assert.All(services, s => Assert.True((decimal)s[nameof(LaundryService.Price)]! > 0));
        Assert.Equal(OutletProfile.SingletonId, outlet[nameof(OutletProfile.Id)]);
    }

    [Fact]
    public void Model_has_a_sequence_for_order_numbers()
    {
        using var db = CreateContext();

        Assert.NotNull(db.Model.FindSequence(DivaDbContext.OrderNumberSequence));
    }

    [Fact]
    public void Migrations_are_in_sync_with_the_model()
    {
        using var db = CreateContext();

        Assert.False(
            db.Database.HasPendingModelChanges(),
            "The model changed without a migration. Run `dotnet ef migrations add <Name>` in BE/src/Diva.Api.");
    }

    [Fact]
    public void Row_level_security_covers_every_table()
    {
        using var db = CreateContext();

        var modelTables = db.Model.GetEntityTypes().Select(e => e.GetTableName()!);

        Assert.All(modelTables, t => Assert.Contains(t, EnableRowLevelSecurity.Tables));
        Assert.Contains("__EFMigrationsHistory", EnableRowLevelSecurity.Tables);
    }
}
