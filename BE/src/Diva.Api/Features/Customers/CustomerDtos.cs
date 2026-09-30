using Diva.Api.Domain;
using Diva.Api.Features.Common;

namespace Diva.Api.Features.Customers;

/// <summary>What the frontend sends to create or update a customer. Everything is nullable so that
/// missing fields reach our validation (and get a clear message) instead of failing JSON binding.</summary>
public sealed record CustomerRequest(string? Name, string? Phone, string? Address, string? Notes);

public sealed record CustomerResponse(
    Guid Id, string Name, string Phone, string? Address, string? Notes, DateTimeOffset CreatedAt)
{
    public static CustomerResponse From(Customer c) => new(c.Id, c.Name, c.Phone, c.Address, c.Notes, c.CreatedAt);
}

/// <summary>A request that passed validation: trimmed, phone normalized.</summary>
public sealed record ValidCustomer(string Name, string Phone, string? Address, string? Notes)
{
    public const int NameMax = 120;
    public const int AddressMax = 300;
    public const int NotesMax = 500;

    public static ValidationErrors Validate(CustomerRequest request, out ValidCustomer? valid)
    {
        var errors = new ValidationErrors();
        valid = null;

        var name = ValidationErrors.Clean(request.Name);
        if (name is null)
        {
            errors.Add("name", "Nama wajib diisi.");
        }

        errors.MaxLength("name", name, NameMax);

        var phone = "";
        if (ValidationErrors.Clean(request.Phone) is null)
        {
            errors.Add("phone", "No. telepon wajib diisi.");
        }
        else if (!PhoneNumber.TryNormalize(request.Phone, out phone))
        {
            errors.Add(
                "phone",
                $"No. telepon harus {PhoneNumber.MinDigits}-{PhoneNumber.MaxDigits} digit angka (boleh diawali +).");
        }

        var address = ValidationErrors.Clean(request.Address);
        errors.MaxLength("address", address, AddressMax);

        var notes = ValidationErrors.Clean(request.Notes);
        errors.MaxLength("notes", notes, NotesMax);

        if (errors.IsValid)
        {
            valid = new ValidCustomer(name!, phone, address, notes);
        }

        return errors;
    }
}
