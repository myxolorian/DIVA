using Diva.Api.Domain;
using Diva.Api.Features.Common;

namespace Diva.Api.Features.Orders;

/// <summary>
/// The price rules of the DIVA price list, kept free of database code so they are easy to test.
/// The server always calculates prices; the frontend only sends which service and how much.
/// </summary>
public static class OrderPricing
{
    public const int MaxItems = 50;
    public const decimal MaxQty = 9_999m;

    /// <summary>Below the minimum (e.g. 2 kg when the minimum is 3 kg) the minimum is charged.</summary>
    public static decimal BilledQty(decimal qty, decimal? minQty) =>
        minQty is { } minimum && qty < minimum ? minimum : qty;

    public static decimal Subtotal(decimal billedQty, decimal unitPrice) =>
        decimal.Round(billedQty * unitPrice, 2, MidpointRounding.AwayFromZero);

    /// <summary>Returns an error message, or null when the quantity is acceptable for this unit.</summary>
    public static string? CheckQty(decimal? qty, ServiceUnit unit)
    {
        if (qty is not { } value)
        {
            return "Qty wajib diisi.";
        }

        if (value <= 0 || value > MaxQty)
        {
            return $"Qty harus lebih dari 0 dan maksimal {Rupiah.Number(MaxQty)}.";
        }

        if (decimal.Round(value, 2) != value)
        {
            return "Qty maksimal 2 angka di belakang koma.";
        }

        if (unit == ServiceUnit.Pcs && decimal.Truncate(value) != value)
        {
            return "Qty untuk satuan Pcs harus bilangan bulat.";
        }

        return null;
    }

    /// <summary>
    /// Fixed-price services use the price from the database and refuse a price from the request.
    /// Services with a price range (MaxPrice set) require a price within that range.
    /// Returns an error message, or null with the price to charge in <paramref name="unitPrice"/>.
    /// </summary>
    public static string? ResolveUnitPrice(LaundryService service, decimal? requested, out decimal unitPrice)
    {
        unitPrice = service.Price;

        if (service.MaxPrice is not { } maxPrice)
        {
            return requested is null
                ? null
                : "Harga jasa ini tetap, jangan kirim unitPrice.";
        }

        if (requested is not { } price)
        {
            return $"Harga wajib diisi: antara {Rupiah.Number(service.Price)} dan {Rupiah.Number(maxPrice)}.";
        }

        if (price < service.Price || price > maxPrice || decimal.Round(price, 2) != price)
        {
            return $"Harga harus antara {Rupiah.Number(service.Price)} dan {Rupiah.Number(maxPrice)}.";
        }

        unitPrice = price;
        return null;
    }
}
