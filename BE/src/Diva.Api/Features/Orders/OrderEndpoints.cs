using Diva.Api.Data;
using Diva.Api.Domain;
using Diva.Api.Features.Common;
using Microsoft.EntityFrameworkCore;

namespace Diva.Api.Features.Orders;

public static class OrderEndpoints
{
    private const string NotFoundName = "Order";
    private const int NotesMax = 500;

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        group.MapGet("/", List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/", Create);
        group.MapPatch("/{id:guid}/status", UpdateStatus);
        group.MapPatch("/{id:guid}/payment", UpdatePayment);

        return app;
    }

    private static async Task<IResult> Create(
        CreateOrderRequest request, DivaDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        // --- 1. Check the order as a whole -------------------------------------------------
        Customer? customer = null;
        if (request.CustomerId is not { } customerId)
        {
            errors.Add("customerId", "Customer wajib dipilih.");
        }
        else
        {
            customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && !c.IsDeleted, ct);
            if (customer is null)
            {
                errors.Add("customerId", "Customer tidak ditemukan.");
            }
        }

        var items = request.Items ?? [];
        if (items.Count == 0)
        {
            errors.Add("items", "Order harus berisi minimal 1 jasa.");
        }
        else if (items.Count > OrderPricing.MaxItems)
        {
            errors.Add("items", $"Maksimal {OrderPricing.MaxItems} baris jasa per order.");
        }

        var notes = ValidationErrors.Clean(request.Notes);
        errors.MaxLength("notes", notes, NotesMax);

        var now = clock.GetUtcNow();
        if (request.DueDate is { } dueDate && dueDate < Wib.Today(clock))
        {
            errors.Add("dueDate", "Tanggal selesai tidak boleh sebelum hari ini.");
        }

        // --- 2. Check every line and calculate its price ------------------------------------
        // One query for all services in the order instead of one query per line.
        var serviceIds = items.Where(i => i.ServiceId is not null).Select(i => i.ServiceId!.Value).Distinct().ToList();
        var services = await db.Services.AsNoTracking()
            .Where(s => serviceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var lines = new List<OrderItem>();
        for (var i = 0; i < items.Count && i < OrderPricing.MaxItems; i++)
        {
            var item = items[i];
            var field = $"items[{i}]";

            if (item.ServiceId is not { } serviceId)
            {
                errors.Add($"{field}.serviceId", "Jasa wajib dipilih.");
                continue;
            }

            if (!services.TryGetValue(serviceId, out var service) || !service.IsActive)
            {
                errors.Add($"{field}.serviceId", "Jasa tidak ditemukan atau sudah tidak aktif.");
                continue;
            }

            if (OrderPricing.CheckQty(item.Qty, service.Unit) is { } qtyError)
            {
                errors.Add($"{field}.qty", qtyError);
            }

            if (OrderPricing.ResolveUnitPrice(service, item.UnitPrice, out var unitPrice) is { } priceError)
            {
                errors.Add($"{field}.unitPrice", priceError);
            }

            if (!errors.IsValid)
            {
                continue;
            }

            var billedQty = OrderPricing.BilledQty(item.Qty!.Value, service.MinQty);
            lines.Add(new OrderItem
            {
                LineNo = i + 1,
                ServiceId = service.Id,
                // Snapshot: later changes to the service never alter this order.
                ServiceName = service.Name,
                Unit = service.Unit,
                UnitPrice = unitPrice,
                MinQty = service.MinQty,
                Qty = item.Qty.Value,
                BilledQty = billedQty,
                Subtotal = OrderPricing.Subtotal(billedQty, unitPrice),
            });
        }

        if (!errors.IsValid)
        {
            return errors.ToResult();
        }

        // --- 3. Number, token, save ------------------------------------------------------------
        // nextval() hands every caller a different number, even when two orders are saved at once.
        var sequence = await db.Database
            .SqlQuery<long>($"SELECT nextval({DivaDbContext.OrderNumberSequence}::regclass) AS \"Value\"")
            .SingleAsync(ct);

        var order = new Order
        {
            OrderNumber = OrderNumber.Format(now, sequence),
            PublicToken = PublicToken.Create(),
            CustomerId = customer!.Id,
            Customer = customer,
            PaymentStatus = request.PaymentStatus ?? PaymentStatus.BelumLunas,
            Notes = notes,
            DueDate = request.DueDate,
            CreatedAt = now,
            Items = lines,
            Total = lines.Sum(l => l.Subtotal),
        };
        db.Orders.Add(order);

        // The order and all its items are written in one transaction: all or nothing.
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/orders/{order.Id}", OrderResponse.From(order));
    }

    private static async Task<IResult> Get(Guid id, DivaDbContext db, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        return order is null ? ApiResults.NotFound(NotFoundName) : Results.Ok(OrderResponse.From(order));
    }

    private static async Task<IResult> List(
        DivaDbContext db,
        OrderStatus? status,
        PaymentStatus? paymentStatus,
        Guid? customerId,
        string? search,
        DateOnly? from,
        DateOnly? to,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        var (pageNumber, size) = PagedResult<OrderSummaryResponse>.Normalize(page, pageSize);
        var query = db.Orders.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(o => o.Status == status);
        }

        if (paymentStatus is not null)
        {
            query = query.Where(o => o.PaymentStatus == paymentStatus);
        }

        if (customerId is not null)
        {
            query = query.Where(o => o.CustomerId == customerId);
        }

        // from/to are WIB calendar dates, both inclusive.
        if (from is { } fromDate)
        {
            var start = Wib.StartOfDayUtc(fromDate);
            query = query.Where(o => o.CreatedAt >= start);
        }

        if (to is { } toDate)
        {
            var end = Wib.StartOfDayUtc(toDate.AddDays(1));
            query = query.Where(o => o.CreatedAt < end);
        }

        if (ValidationErrors.Clean(search) is { } term)
        {
            var pattern = LikePattern.Contains(term);
            query = query.Where(o =>
                EF.Functions.ILike(o.OrderNumber, pattern) ||
                EF.Functions.ILike(o.Customer!.Name, pattern) ||
                EF.Functions.ILike(o.Customer!.Phone, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.OrderNumber)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(OrderSummaryResponse.Projection)
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<OrderSummaryResponse>(items, total, pageNumber, size));
    }

    private static async Task<IResult> UpdateStatus(
        Guid id, UpdateStatusRequest request, DivaDbContext db, CancellationToken ct)
    {
        if (request.Status is not { } status || !Enum.IsDefined(status))
        {
            var errors = new ValidationErrors();
            errors.Add("status", $"Status wajib diisi: {string.Join(", ", Enum.GetNames<OrderStatus>())}.");
            return errors.ToResult();
        }

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            return ApiResults.NotFound(NotFoundName);
        }

        // Any status may be chosen, so the owner can correct a wrong click.
        order.Status = status;
        await db.SaveChangesAsync(ct);

        return await Get(id, db, ct);
    }

    private static async Task<IResult> UpdatePayment(
        Guid id, UpdatePaymentRequest request, DivaDbContext db, CancellationToken ct)
    {
        if (request.PaymentStatus is not { } paymentStatus || !Enum.IsDefined(paymentStatus))
        {
            var errors = new ValidationErrors();
            errors.Add("paymentStatus", $"Status bayar wajib diisi: {string.Join(", ", Enum.GetNames<PaymentStatus>())}.");
            return errors.ToResult();
        }

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            return ApiResults.NotFound(NotFoundName);
        }

        order.PaymentStatus = paymentStatus;
        await db.SaveChangesAsync(ct);

        return await Get(id, db, ct);
    }
}
