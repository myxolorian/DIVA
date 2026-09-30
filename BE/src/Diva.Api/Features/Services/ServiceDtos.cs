using Diva.Api.Domain;
using Diva.Api.Features.Common;

namespace Diva.Api.Features.Services;

public sealed record ServiceRequest(
    string? Name,
    string? Category,
    ServiceUnit? Unit,
    decimal? Price,
    decimal? MaxPrice,
    decimal? MinQty,
    int? SortOrder,
    bool? IsActive);

public sealed record ServiceResponse(
    Guid Id,
    string Name,
    string Category,
    int SortOrder,
    ServiceUnit Unit,
    decimal Price,
    decimal? MaxPrice,
    decimal? MinQty,
    bool IsActive)
{
    public static ServiceResponse From(LaundryService s) =>
        new(s.Id, s.Name, s.Category, s.SortOrder, s.Unit, s.Price, s.MaxPrice, s.MinQty, s.IsActive);
}

public sealed record ValidService(
    string Name,
    string Category,
    ServiceUnit Unit,
    decimal Price,
    decimal? MaxPrice,
    decimal? MinQty,
    int? SortOrder,
    bool? IsActive)
{
    public const int NameMax = 120;
    public const int CategoryMax = 60;
    public const decimal MaxAllowedPrice = 100_000_000m;
    public const decimal MaxAllowedQty = 9_999m;

    public static ValidationErrors Validate(ServiceRequest request, out ValidService? valid)
    {
        var errors = new ValidationErrors();
        valid = null;

        var name = ValidationErrors.Clean(request.Name);
        if (name is null)
        {
            errors.Add("name", "Nama jasa wajib diisi.");
        }

        errors.MaxLength("name", name, NameMax);

        var category = ValidationErrors.Clean(request.Category);
        if (category is null)
        {
            errors.Add("category", "Kategori wajib diisi, misalnya \"Laundry Kiloan\".");
        }

        errors.MaxLength("category", category, CategoryMax);

        // Enum.IsDefined also catches numbers that are not one of the units, such as 7.
        if (request.Unit is not { } unit || !Enum.IsDefined(unit))
        {
            errors.Add("unit", $"Satuan wajib diisi: {string.Join(", ", Enum.GetNames<ServiceUnit>())}.");
        }

        if (request.Price is null)
        {
            errors.Add("price", "Harga wajib diisi.");
        }
        else
        {
            CheckMoney(errors, "price", request.Price.Value);
        }

        if (request.MaxPrice is { } maxPrice)
        {
            CheckMoney(errors, "maxPrice", maxPrice);
            if (request.Price is { } price && maxPrice <= price)
            {
                errors.Add("maxPrice", "Harga maksimal harus lebih besar dari harga (minimal).");
            }
        }

        if (request.MinQty is { } minQty)
        {
            if (minQty <= 0 || minQty > MaxAllowedQty)
            {
                errors.Add("minQty", $"Minimal order harus lebih dari 0 dan maksimal {Rupiah.Number(MaxAllowedQty)}.");
            }
            else if (decimal.Round(minQty, 2) != minQty)
            {
                errors.Add("minQty", "Minimal order maksimal 2 angka di belakang koma.");
            }
        }

        if (request.SortOrder is < 0)
        {
            errors.Add("sortOrder", "Urutan tidak boleh negatif.");
        }

        if (errors.IsValid)
        {
            valid = new ValidService(
                name!, category!, request.Unit!.Value, request.Price!.Value,
                request.MaxPrice, request.MinQty, request.SortOrder, request.IsActive);
        }

        return errors;
    }

    private static void CheckMoney(ValidationErrors errors, string field, decimal value)
    {
        if (value <= 0 || value > MaxAllowedPrice)
        {
            errors.Add(field, $"Harga harus lebih dari 0 dan maksimal {Rupiah.Number(MaxAllowedPrice)}.");
        }
        else if (decimal.Round(value, 2) != value)
        {
            errors.Add(field, "Harga maksimal 2 angka di belakang koma.");
        }
    }
}
