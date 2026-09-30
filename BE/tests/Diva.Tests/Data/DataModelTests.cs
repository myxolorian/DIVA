using Diva.Api.Data;
using Diva.Api.Data.Migrations;
using Diva.Api.Features.Services;
using static Diva.Api.Data.Configurations.LaundryServiceConfiguration;
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

        Assert.Equal(PriceList.Services.Length, services.Count);
        Assert.Equal(OutletProfile.SingletonId, outlet[nameof(OutletProfile.Id)]);
    }

    [Fact]
    public void Price_list_has_the_21_services_of_the_printed_list()
    {
        Assert.Equal(21, PriceList.Services.Length);
        Assert.Equal(21, PriceList.Services.Select(s => s.Id).Distinct().Count());
        Assert.Equal(21, PriceList.Services.Select(s => s.SortOrder).Distinct().Count());

        Assert.Equal(
            [PriceList.Kiloan, PriceList.BedCover, PriceList.Selimut, PriceList.Karpet, PriceList.Satuan, PriceList.Formal, PriceList.Dress],
            PriceList.Services.OrderBy(s => s.SortOrder).Select(s => s.Category).Distinct());

        // The rules printed on the list: kiloan minimum 3 kg, karpet minimum 4 m2, kebaya payet 60-150 ribu.
        Assert.All(PriceList.Services.Where(s => s.Category == PriceList.Kiloan), s => Assert.Equal((ServiceUnit.Kg, 3m), (s.Unit, s.MinQty)));
        Assert.All(PriceList.Services.Where(s => s.Category == PriceList.Karpet), s => Assert.Equal((ServiceUnit.M2, 4m), (s.Unit, s.MinQty)));
        var ranged = Assert.Single(PriceList.Services, s => s.MaxPrice is not null);
        Assert.Equal(("Dress Pesta/Kebaya Payet", 60_000m, 150_000m), (ranged.Name, ranged.Price, ranged.MaxPrice!.Value));
    }

    [Fact]
    public void Every_seeded_service_would_pass_the_api_validation()
    {
        // Keeps the seed and the rules for POST/PUT /api/services from drifting apart.
        Assert.All(PriceList.Services, s =>
        {
            var errors = ValidService.Validate(
                new ServiceRequest(s.Name, s.Category, s.Unit, s.Price, s.MaxPrice, s.MinQty, s.SortOrder, s.IsActive),
                out _);
            Assert.True(errors.IsValid, s.Name);
        });
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
