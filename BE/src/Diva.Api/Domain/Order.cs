namespace Diva.Api.Domain;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable number such as DIV-260929-0012.</summary>
    public required string OrderNumber { get; set; }

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Baru;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.BelumLunas;
    /// <summary>When the order was marked paid (null while unpaid). Income is counted on this moment.</summary>
    public DateTimeOffset? PaidAt { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    /// <summary>Promised pick-up date (a calendar date in WIB, no time).</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Unguessable token (128-bit, base64url) used in the shareable receipt link.</summary>
    public required string PublicToken { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<OrderItem> Items { get; set; } = [];
}
