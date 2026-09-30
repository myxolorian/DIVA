using Diva.Api.Data;
using Diva.Api.Domain;
using Diva.Api.Features.Common;
using Microsoft.EntityFrameworkCore;

namespace Diva.Api.Features.Customers;

public static class CustomerEndpoints
{
    private const string NotFoundName = "Customer";

    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        // A route group shares the URL prefix; login is already required by the fallback policy.
        var group = app.MapGroup("/api/customers").WithTags("Customers");

        group.MapGet("/", List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);

        return app;
    }

    // The parameters are filled in by ASP.NET: query string values by name, DivaDbContext from
    // dependency injection, CancellationToken is cancelled when the caller disconnects.
    private static async Task<IResult> List(
        DivaDbContext db, string? search, int? page, int? pageSize, CancellationToken ct)
    {
        var (pageNumber, size) = PagedResult<CustomerResponse>.Normalize(page, pageSize);

        // Nothing runs yet: this only describes the SQL query. It is sent when CountAsync/ToListAsync run.
        var query = db.Customers.AsNoTracking().Where(c => !c.IsDeleted);

        var term = ValidationErrors.Clean(search);
        if (term is not null)
        {
            var namePattern = LikePattern.Contains(term);
            var digits = PhoneNumber.StripSeparators(term);
            var phonePattern = LikePattern.Contains(digits);

            // ILIKE = case-insensitive LIKE in PostgreSQL. Phone matching ignores the separators
            // the user typed, because phones are stored without them.
            query = digits.Length == 0
                ? query.Where(c => EF.Functions.ILike(c.Name, namePattern))
                : query.Where(c =>
                    EF.Functions.ILike(c.Name, namePattern) ||
                    EF.Functions.ILike(c.Phone, phonePattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Name).ThenBy(c => c.Id) // ThenBy keeps paging stable when names repeat
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(c => new CustomerResponse(c.Id, c.Name, c.Phone, c.Address, c.Notes, c.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<CustomerResponse>(items, total, pageNumber, size));
    }

    private static async Task<IResult> Get(Guid id, DivaDbContext db, CancellationToken ct)
    {
        var customer = await FindActive(db, id, ct);
        return customer is null ? ApiResults.NotFound(NotFoundName) : Results.Ok(CustomerResponse.From(customer));
    }

    private static async Task<IResult> Create(CustomerRequest request, DivaDbContext db, CancellationToken ct)
    {
        var errors = ValidCustomer.Validate(request, out var valid);
        if (!errors.IsValid)
        {
            return errors.ToResult();
        }

        if (await PhoneTaken(db, valid!.Phone, exceptId: null, ct) is { } conflict)
        {
            return conflict;
        }

        var customer = new Customer
        {
            Name = valid.Name,
            Phone = valid.Phone,
            Address = valid.Address,
            Notes = valid.Notes,
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct); // INSERT happens here

        // 201 Created + Location header pointing at the new customer.
        return Results.Created($"/api/customers/{customer.Id}", CustomerResponse.From(customer));
    }

    private static async Task<IResult> Update(
        Guid id, CustomerRequest request, DivaDbContext db, CancellationToken ct)
    {
        var customer = await FindActive(db, id, ct);
        if (customer is null)
        {
            return ApiResults.NotFound(NotFoundName);
        }

        var errors = ValidCustomer.Validate(request, out var valid);
        if (!errors.IsValid)
        {
            return errors.ToResult();
        }

        if (await PhoneTaken(db, valid!.Phone, exceptId: id, ct) is { } conflict)
        {
            return conflict;
        }

        // EF tracks the loaded entity, so changing its properties is enough: SaveChanges sends an UPDATE.
        customer.Name = valid.Name;
        customer.Phone = valid.Phone;
        customer.Address = valid.Address;
        customer.Notes = valid.Notes;
        await db.SaveChangesAsync(ct);

        return Results.Ok(CustomerResponse.From(customer));
    }

    private static async Task<IResult> Delete(Guid id, DivaDbContext db, CancellationToken ct)
    {
        var customer = await FindActive(db, id, ct);
        if (customer is null)
        {
            return ApiResults.NotFound(NotFoundName);
        }

        // Soft delete: the row stays so old orders and receipts still show the customer's name.
        customer.IsDeleted = true;
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static Task<Customer?> FindActive(DivaDbContext db, Guid id, CancellationToken ct) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

    /// <summary>409 Conflict when another active customer already has this phone number.</summary>
    private static async Task<IResult?> PhoneTaken(DivaDbContext db, string phone, Guid? exceptId, CancellationToken ct)
    {
        var existing = await db.Customers.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Phone == phone && c.Id != exceptId)
            .Select(c => new { c.Id, c.Name })
            .FirstOrDefaultAsync(ct);

        return existing is null
            ? null
            : ApiResults.Conflict(
                $"No. telepon sudah dipakai customer \"{existing.Name}\".",
                // Lets the frontend offer "pilih customer yang sudah ada" instead of a dead end.
                new Dictionary<string, object?> { ["customerId"] = existing.Id });
    }
}
