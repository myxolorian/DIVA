using Diva.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diva.Api.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("order_items");
        b.HasKey(x => x.Id);
        b.Property(x => x.ServiceName).HasMaxLength(120).IsRequired();
        b.Property(x => x.Unit).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.UnitPrice).HasPrecision(12, 2);
        b.Property(x => x.Qty).HasPrecision(8, 2);
        b.Property(x => x.MinQty).HasPrecision(8, 2);
        b.Property(x => x.BilledQty).HasPrecision(8, 2);
        b.Property(x => x.Subtotal).HasPrecision(12, 2);

        b.HasIndex(x => x.OrderId);
        b.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
    }
}
