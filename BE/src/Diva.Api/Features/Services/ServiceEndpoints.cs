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

        var items = await query
            .OrderBy(s => s.Name)
            .Select(s => new ServiceResponse(s.Id, s.Name, s.Unit, s.Price, s.IsActive))
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
            Unit = valid.Unit,
            Price = valid.Price,
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
        service.Name = valid!.Name;
        service.Unit = valid.Unit;
        service.Price = valid.Price;
        service.IsActive = valid.IsActive ?? service.IsActive;
        await db.SaveChangesAsync(ct);

        return Results.Ok(ServiceResponse.From(service));
    }

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
