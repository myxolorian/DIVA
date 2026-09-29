using Diva.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diva.Api.Data.Configurations;

public class OutletProfileConfiguration : IEntityTypeConfiguration<OutletProfile>
{
    public void Configure(EntityTypeBuilder<OutletProfile> b)
    {
        b.ToTable("outlet_profile", t => t.HasCheckConstraint("ck_outlet_profile_singleton", "id = 1"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.ReceiptFooter).HasMaxLength(300);

        b.HasData(new OutletProfile
        {
            Id = OutletProfile.SingletonId,
            Name = "DIVA Laundry",
            ReceiptFooter = "Terima kasih telah mempercayakan cucian Anda kepada kami.",
        });
    }
}
