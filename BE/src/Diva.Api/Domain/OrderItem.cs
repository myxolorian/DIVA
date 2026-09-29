namespace Diva.Api.Domain;

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid ServiceId { get; set; }
    public LaundryService? Service { get; set; }

    // Snapshot of the service at order time, so old receipts never change when prices do.
    public required string ServiceName { get; set; }
    public ServiceUnit Unit { get; set; }
    public decimal UnitPrice { get; set; }

    public decimal Qty { get; set; }
    public decimal Subtotal { get; set; }
}
