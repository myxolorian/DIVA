namespace Diva.Api.Domain;

/// <summary>A service the laundry offers (jasa), e.g. "Cuci Kiloan".</summary>
public class LaundryService
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public ServiceUnit Unit { get; set; }
    public decimal Price { get; set; }

    /// <summary>Inactive services are hidden from new orders but stay valid for old ones.</summary>
    public bool IsActive { get; set; } = true;
}
