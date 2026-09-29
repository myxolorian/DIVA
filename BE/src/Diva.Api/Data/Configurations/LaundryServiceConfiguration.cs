using Diva.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diva.Api.Data.Configurations;

public class LaundryServiceConfiguration : IEntityTypeConfiguration<LaundryService>
{
    public void Configure(EntityTypeBuilder<LaundryService> b)
    {
        b.ToTable("services");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Unit).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Price).HasPrecision(12, 2);

        b.HasData(
            Seed("Cuci Kiloan", ServiceUnit.Kg, 7_000m, "00000000-0000-0000-0000-000000000101"),
            Seed("Setrika", ServiceUnit.Kg, 5_000m, "00000000-0000-0000-0000-000000000102"),
            Seed("Cuci + Setrika", ServiceUnit.Kg, 10_000m, "00000000-0000-0000-0000-000000000103"),
            Seed("Bed Cover", ServiceUnit.Pcs, 35_000m, "00000000-0000-0000-0000-000000000104"),
            Seed("Selimut", ServiceUnit.Pcs, 25_000m, "00000000-0000-0000-0000-000000000105"),
            Seed("Cuci Sepatu", ServiceUnit.Pcs, 30_000m, "00000000-0000-0000-0000-000000000106"));
    }

    // Starter catalogue with placeholder prices; the owner edits them in the app.
    private static LaundryService Seed(string name, ServiceUnit unit, decimal price, string id) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        Unit = unit,
        Price = price,
        IsActive = true,
    };
}
