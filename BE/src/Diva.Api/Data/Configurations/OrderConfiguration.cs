using Diva.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diva.Api.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("orders");
        b.HasKey(x => x.Id);
        b.Property(x => x.OrderNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Total).HasPrecision(12, 2);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.PublicToken).HasMaxLength(64).IsRequired();

        b.HasIndex(x => x.OrderNumber).IsUnique();
        b.HasIndex(x => x.PublicToken).IsUnique();
        b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.PaidAt);

        // Customers are soft-deleted, so a customer with orders can never be hard-deleted.
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
