namespace Diva.Api.Domain;

/// <summary>A service the laundry offers (jasa), e.g. "Cuci Kering Setrika".</summary>
public class LaundryService
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }

    /// <summary>Group shown in the price list and the order screen, e.g. "Laundry Kiloan".</summary>
    public required string Category { get; set; }

    /// <summary>Display order (lowest first), so lists follow the printed price list.</summary>
    public int SortOrder { get; set; }

    public ServiceUnit Unit { get; set; }

    /// <summary>Price per unit. For a price range this is the lowest price.</summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Set only for services priced per case within a range (e.g. kebaya payet 60.000-150.000):
    /// the cashier types the actual price when creating the order, between Price and MaxPrice.
    /// </summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>
    /// Minimum billed quantity (e.g. 3 kg). A smaller quantity is billed as this minimum.
    /// </summary>
    public decimal? MinQty { get; set; }

    /// <summary>Inactive services are hidden from new orders but stay valid for old ones.</summary>
    public bool IsActive { get; set; } = true;
}
