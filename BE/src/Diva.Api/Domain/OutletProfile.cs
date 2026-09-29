namespace Diva.Api.Domain;

/// <summary>Single-row table with the laundry's details, printed on every receipt.</summary>
public class OutletProfile
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public required string Name { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? ReceiptFooter { get; set; }
}
