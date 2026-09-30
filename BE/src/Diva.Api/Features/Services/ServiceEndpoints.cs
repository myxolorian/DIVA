using Diva.Api.Data;
using Diva.Api.Domain;
using Diva.Api.Features.Common;
using Microsoft.EntityFrameworkCore;

namespace Diva.Api.Features.Services;

public static class ServiceEndpoints
{
    private const string NotFoundName = "Jasa";

    public static IEndpointRouteBuilder MapServiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/services").WithTags("Services");

        group.MapGet("/", List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Deactivate);

        return app;
    }

    private static async Task<IResult> List(DivaDbContext db, bool? includeInactive, CancellationToken ct)
    {
        var query = db.Services.AsNoTracking();
        if (includeInactive != true)
        {
            query = query.Where(s => s.IsActive);
        }

        // Same order as the printed price list; the frontend groups by Category.
        var items = await query
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new ServiceResponse(
                s.Id, s.Name, s.Category, s.SortOrder, s.Unit, s.Price, s.MaxPrice, s.MinQty, s.IsActive))
            .ToListAsync(ct);

        return Results.Ok(items);
    }

    private static async Task<IResult> Get(Guid id, DivaDbContext db, CancellationToken ct)
    {
        var service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return service is null ? ApiResults.NotFound(NotFoundName) : Results.Ok(ServiceResponse.From(service));
    }

    private static async Task<IResult> Create(ServiceRequest request, DivaDbContext db, CancellationToken ct)
    {
        var errors = ValidService.Validate(request, out var valid);
        if (!errors.IsValid)
        {
            return errors.ToResult();
        }

        var service = new LaundryService
        {
            Name = valid!.Name,
            Category = valid.Category,
            Unit = valid.Unit,
            Price = valid.Price,
            MaxPrice = valid.MaxPrice,
            MinQty = valid.MinQty,
            // Without an explicit position a new service goes to the end of the list.
            SortOrder = valid.SortOrder ?? await NextSortOrder(db, ct),
            IsActive = valid.IsActive ?? true,
        };
        db.Services.Add(service);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/services/{service.Id}", ServiceResponse.From(service));
    }

    private static async Task<IResult> Update(Guid id, ServiceRequest request, DivaDbContext db, CancellationToken ct)
    {
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (service is null)
        {
            return ApiResults.NotFound(NotFoundName);
        }

        var errors = ValidService.Validate(request, out var valid);
        if (!errors.IsValid)
        {
            return errors.ToResult();
        }

        // Changing the price here does not touch old orders: they keep their own copy of the price.
        // PUT replaces the whole service, so leaving maxPrice/minQty out clears them.
        service.Name = valid!.Name;
        service.Category = valid.Category;
        service.Unit = valid.Unit;
        service.Price = valid.Price;
        service.MaxPrice = valid.MaxPrice;
        service.MinQty = valid.MinQty;
        service.SortOrder = valid.SortOrder ?? service.SortOrder;
        service.IsActive = valid.IsActive ?? service.IsActive;
        await db.SaveChangesAsync(ct);

        return Results.Ok(ServiceResponse.From(service));
    }

    private static async Task<int> NextSortOrder(DivaDbContext db, CancellationToken ct) =>
        (await db.Services.MaxAsync(s => (int?)s.SortOrder, ct) ?? 0) + 10;

    private static async Task<IResult> Deactivate(Guid id, DivaDbContext db, CancellationToken ct)
    {
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (service is null)
        {
            return ApiResults.NotFound(NotFoundName);
        }

        // Not a real delete: order items reference services, and old receipts must stay intact.
        service.IsActive = false;
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}
