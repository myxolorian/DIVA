using Diva.Api.Domain;
using Diva.Api.Features.Common;

namespace Diva.Api.Features.Services;

public sealed record ServiceRequest(string? Name, ServiceUnit? Unit, decimal? Price, bool? IsActive);

public sealed record ServiceResponse(Guid Id, string Name, ServiceUnit Unit, decimal Price, bool IsActive)
{
    public static ServiceResponse From(LaundryService s) => new(s.Id, s.Name, s.Unit, s.Price, s.IsActive);
}

public sealed record ValidService(string Name, ServiceUnit Unit, decimal Price, bool? IsActive)
{
    public const int NameMax = 120;
    public const decimal MaxPrice = 100_000_000m;

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

        // Enum.IsDefined also catches numbers that are not one of the units, such as 7.
        if (request.Unit is not { } unit || !Enum.IsDefined(unit))
        {
            errors.Add("unit", $"Satuan wajib diisi: {string.Join(", ", Enum.GetNames<ServiceUnit>())}.");
        }

        if (request.Price is not { } price)
        {
            errors.Add("price", "Harga wajib diisi.");
        }
        else if (price <= 0 || price > MaxPrice)
        {
            errors.Add("price", $"Harga harus lebih dari 0 dan maksimal {MaxPrice:N0}.");
        }
        else if (decimal.Round(price, 2) != price)
        {
            errors.Add("price", "Harga maksimal 2 angka di belakang koma.");
        }

        if (errors.IsValid)
        {
            valid = new ValidService(name!, request.Unit!.Value, request.Price!.Value, request.IsActive);
        }

        return errors;
    }
}
