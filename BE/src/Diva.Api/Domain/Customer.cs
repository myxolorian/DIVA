namespace Diva.Api.Domain;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Soft delete: orders keep referencing the customer, so rows are never removed.</summary>
    public bool IsDeleted { get; set; }
}
