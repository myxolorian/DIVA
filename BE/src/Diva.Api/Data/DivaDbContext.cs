using Diva.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Diva.Api.Data;

public class DivaDbContext(DbContextOptions<DivaDbContext> options) : DbContext(options)
{
    /// <summary>Postgres sequence behind order numbers (DIV-yyMMdd-####).</summary>
    public const string OrderNumberSequence = "order_number_seq";

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LaundryService> Services => Set<LaundryService>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OutletProfile> OutletProfiles => Set<OutletProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>(OrderNumberSequence).StartsAt(1).IncrementsBy(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DivaDbContext).Assembly);
    }
}
