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
        b.Property(x => x.Category).HasMaxLength(60).IsRequired();
        b.Property(x => x.Unit).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Price).HasPrecision(12, 2);
        b.Property(x => x.MaxPrice).HasPrecision(12, 2);
        b.Property(x => x.MinQty).HasPrecision(8, 2);
        b.HasIndex(x => x.SortOrder);

        b.HasData(PriceList.Services);
    }

    /// <summary>
    /// The DIVA Laundry price list (docs: "PRICE LIST", 2 pages). Used as seed data, so a new
    /// database starts with the real prices. Change prices afterwards through the API, not here.
    /// </summary>
    public static class PriceList
    {
        public const string Kiloan = "Laundry Kiloan";
        public const string BedCover = "Bed Cover";
        public const string Selimut = "Selimut";
        public const string Karpet = "Karpet / Permadani";
        public const string Satuan = "Laundry Satuan";
        public const string Formal = "Pakaian Formal";
        public const string Dress = "Dress / Gaun";

        public static readonly LaundryService[] Services =
        [
            // Laundry kiloan: minimum 3 kg
            Seed(1, Kiloan, "Cuci Kering Setrika", ServiceUnit.Kg, 7_000m, minQty: 3),
            Seed(2, Kiloan, "Cuci Kering Lipat", ServiceUnit.Kg, 5_000m, minQty: 3),

            // Bed cover: satuan and set
            Seed(3, BedCover, "Bed Cover Single (S)", ServiceUnit.Pcs, 30_000m),
            Seed(4, BedCover, "Bed Cover Single (S) - Set", ServiceUnit.Pcs, 45_000m),
            Seed(5, BedCover, "Bed Cover Double (L)", ServiceUnit.Pcs, 35_000m),
            Seed(6, BedCover, "Bed Cover Double (L) - Set", ServiceUnit.Pcs, 55_000m),
            Seed(7, BedCover, "Bed Cover Big Size", ServiceUnit.Pcs, 50_000m),
            Seed(8, BedCover, "Bed Cover Big Size - Set", ServiceUnit.Pcs, 70_000m),

            Seed(9, Selimut, "Selimut Tipis/Bulu Ukuran Kecil", ServiceUnit.Pcs, 25_000m),
            Seed(10, Selimut, "Selimut Tipis/Bulu Ukuran Besar", ServiceUnit.Pcs, 35_000m),

            // Karpet: minimum 4 m2 (so the minimum charge is 4 x price, e.g. 60.000 for tipis)
            Seed(11, Karpet, "Karpet Tipis", ServiceUnit.M2, 15_000m, minQty: 4),
            Seed(12, Karpet, "Karpet Sedang", ServiceUnit.M2, 20_000m, minQty: 4),
            Seed(13, Karpet, "Karpet Tebal", ServiceUnit.M2, 25_000m, minQty: 4),

            Seed(14, Satuan, "Atasan (Kemeja/Blus/Batik/T-Shirt)", ServiceUnit.Pcs, 25_000m),
            Seed(15, Satuan, "Bawahan (Celana/Rok/Jeans)", ServiceUnit.Pcs, 25_000m),

            Seed(16, Formal, "Jas/Blazer", ServiceUnit.Pcs, 35_000m),
            Seed(17, Formal, "Setelan Jas (Jas + Celana)", ServiceUnit.Pcs, 60_000m),

            Seed(18, Dress, "Dress Pendek/Gaun Standard (Polos)", ServiceUnit.Pcs, 40_000m),
            // Price depends on how complex the accessories/payet are: typed in per order.
            Seed(19, Dress, "Dress Pesta/Kebaya Payet", ServiceUnit.Pcs, 60_000m, maxPrice: 150_000m),
            Seed(20, Dress, "Dress Baju Muslim Polos", ServiceUnit.Pcs, 50_000m),
            Seed(21, Dress, "Dress Baju Muslim Variasi", ServiceUnit.Pcs, 75_000m),
        ];

        // Fixed ids (…0201 to …0221) keep the seed stable across migrations.
        private static LaundryService Seed(
            int number, string category, string name, ServiceUnit unit, decimal price,
            decimal? minQty = null, decimal? maxPrice = null) => new()
        {
            Id = Guid.Parse($"00000000-0000-0000-0000-{200 + number:D12}"),
            Category = category,
            SortOrder = number * 10,
            Name = name,
            Unit = unit,
            Price = price,
            MinQty = minQty,
            MaxPrice = maxPrice,
            IsActive = true,
        };
    }
}
