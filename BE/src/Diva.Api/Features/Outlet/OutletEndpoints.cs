using Diva.Api.Data;
using Diva.Api.Domain;
using Diva.Api.Features.Common;
using Diva.Api.Features.Customers;
using Microsoft.EntityFrameworkCore;

namespace Diva.Api.Features.Outlet;

public sealed record OutletRequest(string? Name, string? Address, string? Phone, string? ReceiptFooter);

public sealed record OutletResponse(string Name, string? Address, string? Phone, string? ReceiptFooter)
{
    public static OutletResponse From(OutletProfile o) => new(o.Name, o.Address, o.Phone, o.ReceiptFooter);
}

/// <summary>The laundry's own details, printed at the top and bottom of every receipt.</summary>
public static class OutletEndpoints
{
    public static IEndpointRouteBuilder MapOutletEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/outlet").WithTags("Outlet");

        group.MapGet("/", async (DivaDbContext db, CancellationToken ct) =>
            Results.Ok(OutletResponse.From(await GetOrCreate(db, ct))));

        group.MapPut("/", Update);

        return app;
    }

    private static async Task<IResult> Update(OutletRequest request, DivaDbContext db, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        var name = ValidationErrors.Clean(request.Name);
        if (name is null)
        {
            errors.Add("name", "Nama laundry wajib diisi.");
        }

        errors.MaxLength("name", name, 120);

        var address = ValidationErrors.Clean(request.Address);
        errors.MaxLength("address", address, 300);

        var footer = ValidationErrors.Clean(request.ReceiptFooter);
        errors.MaxLength("receiptFooter", footer, 300);

        // Optional, but when given it must be a real number (stored the same way as customer phones).
        string? phone = null;
        if (ValidationErrors.Clean(request.Phone) is not null && !PhoneNumber.TryNormalize(request.Phone, out phone))
        {
            errors.Add("phone", $"No. telepon harus {PhoneNumber.MinDigits}-{PhoneNumber.MaxDigits} digit angka (boleh diawali +).");
        }

        if (!errors.IsValid)
        {
            return errors.ToResult();
        }

        var outlet = await GetOrCreate(db, ct);
        outlet.Name = name!;
        outlet.Address = address;
        outlet.Phone = phone;
        outlet.ReceiptFooter = footer;
        await db.SaveChangesAsync(ct);

        return Results.Ok(OutletResponse.From(outlet));
    }

    /// <summary>The single outlet row (id 1) comes from the seed data; recreate it if it was removed.</summary>
    private static async Task<OutletProfile> GetOrCreate(DivaDbContext db, CancellationToken ct)
    {
        var outlet = await db.OutletProfiles.FirstOrDefaultAsync(o => o.Id == OutletProfile.SingletonId, ct);
        if (outlet is null)
        {
            outlet = new OutletProfile { Name = "DIVA Laundry" };
            db.OutletProfiles.Add(outlet);
            await db.SaveChangesAsync(ct);
        }

        return outlet;
    }
}
