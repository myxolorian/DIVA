using System.Text.RegularExpressions;
using Diva.Api.Data;
using Diva.Api.Domain;
using Diva.Api.Features.Common;
using Microsoft.EntityFrameworkCore;

namespace Diva.Api.Features.Receipts;

/// <summary>
/// Receipts for customers: no login, the random token in the link is the only key.
/// Everything here is rate limited per IP address and never cached.
/// </summary>
public static partial class ReceiptEndpoints
{
    public const string RateLimitPolicy = "public-receipts";
    public const string RateLimitSetting = "Receipts:RateLimitPerMinute";
    public const int DefaultRateLimitPerMinute = 60;

    private const string NotFoundName = "Receipt";

    // Same shape as PublicToken.Create(): 22 base64url characters.
    [GeneratedRegex("^[A-Za-z0-9_-]{22}$")]
    private static partial Regex TokenShape();

    public static IEndpointRouteBuilder MapReceiptEndpoints(this IEndpointRouteBuilder app, string frontendPath)
    {
        var group = app.MapGroup("/api/public/receipts")
            .WithTags("Receipts")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy)
            .AddEndpointFilter(async (context, next) =>
            {
                var headers = context.HttpContext.Response.Headers;
                headers.CacheControl = "no-store";     // a receipt may change (status, payment)
                headers["X-Robots-Tag"] = "noindex";   // keep receipts out of search engines
                return await next(context);
            });

        group.MapGet("/{token}", GetJson);
        group.MapGet("/{token}/pdf", GetPdf);

        // The link that is shared with customers. The page itself loads the JSON above.
        app.MapGet("/r/{token}", (HttpContext http) =>
            {
                http.Response.Headers["X-Robots-Tag"] = "noindex";
                http.Response.Headers["Referrer-Policy"] = "no-referrer";
                var page = Path.Combine(frontendPath, "receipt.html");
                return File.Exists(page)
                    ? Results.File(page, "text/html; charset=utf-8")
                    : ApiResults.NotFound("Halaman receipt");
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy)
            .ExcludeFromDescription();

        return app;
    }

    private static async Task<IResult> GetJson(string token, DivaDbContext db, CancellationToken ct)
    {
        var receipt = await Load(token, db, ct);
        return receipt is null ? ApiResults.NotFound(NotFoundName) : Results.Ok(receipt);
    }

    private static async Task<IResult> GetPdf(string token, HttpRequest request, DivaDbContext db, CancellationToken ct)
    {
        var receipt = await Load(token, db, ct);
        if (receipt is null)
        {
            return ApiResults.NotFound(NotFoundName);
        }

        var receiptUrl = $"{request.Scheme}://{request.Host}/r/{token}";
        var pdf = ReceiptPdf.Create(receipt, receiptUrl);

        // fileDownloadName makes the browser save it as e.g. DIV-260930-0001.pdf.
        return Results.File(pdf, "application/pdf", fileDownloadName: $"{receipt.OrderNumber}.pdf");
    }

    private static async Task<ReceiptResponse?> Load(string token, DivaDbContext db, CancellationToken ct)
    {
        // Malformed tokens are rejected without touching the database.
        if (!TokenShape().IsMatch(token))
        {
            return null;
        }

        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.PublicToken == token, ct);
        if (order is null)
        {
            return null;
        }

        var outlet = await db.OutletProfiles.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? new OutletProfile { Name = "DIVA Laundry" };

        return ReceiptResponse.From(order, outlet);
    }
}
