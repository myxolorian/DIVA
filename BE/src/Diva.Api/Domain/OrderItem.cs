namespace Diva.Api.Domain;

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>Position in the order (1, 2, 3, ...) so receipts list items in the order they were entered.</summary>
    public int LineNo { get; set; }

    public Guid ServiceId { get; set; }
    public LaundryService? Service { get; set; }

    // Snapshot of the service at order time, so old receipts never change when prices do.
    public required string ServiceName { get; set; }
    public ServiceUnit Unit { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>The service's minimum billed quantity at order time (e.g. 3 kg), if any.</summary>
    public decimal? MinQty { get; set; }

    /// <summary>Quantity actually brought in by the customer.</summary>
    public decimal Qty { get; set; }

    /// <summary>Quantity that is charged: Qty, raised to MinQty when it is below the minimum.</summary>
    public decimal BilledQty { get; set; }

    /// <summary>BilledQty x UnitPrice.</summary>
    public decimal Subtotal { get; set; }
}
