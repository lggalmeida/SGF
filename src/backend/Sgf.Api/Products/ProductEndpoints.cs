using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Sgf.Application.Products;
using Sgf.Infrastructure.Database.MultiTenancy;

namespace Sgf.Api.Products;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/products").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (TenantContextUnavailableException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is DbUpdateException or DbException)
            {
                return Results.Problem(statusCode: 500, title: "Nao foi possivel concluir a operacao.");
            }
        });
        group.MapGet("", async (IProductService service, CancellationToken ct, int page = 1,
            int pageSize = 20, string? search = null, bool? isActive = null) =>
            Respond(await service.ListAsync(page, pageSize, search, isActive, ct)));
        group.MapGet("/{id:guid}", async (Guid id, IProductService service, CancellationToken ct) =>
            Respond(await service.GetAsync(id, ct)));
        group.MapPost("", async (SaveProductRequest request, IProductService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.Error is null && result.Value is not null
                ? Results.Created($"/api/products/{result.Value.Id}", result.Value) : Respond(result);
        });
        group.MapPut("/{id:guid}", async (Guid id, SaveProductRequest request, IProductService service, CancellationToken ct) =>
            Respond(await service.UpdateAsync(id, request, ct)));
        group.MapPatch("/{id:guid}/status", async (Guid id, ProductStatusRequest request, IProductService service, CancellationToken ct) =>
            Respond(await service.SetStatusAsync(id, request.IsActive, ct)));
    }

    private static IResult Respond<T>(ProductResult<T> result)
    {
        if (result.Error is null) return Results.Ok(result.Value);
        var (status, code, title) = result.Error switch
        {
            ProductError.Validation => (400, "validation_failed", "Confira os dados do produto."),
            ProductError.NotFound => (404, "product_not_found", "Produto nao encontrado."),
            ProductError.DuplicateSku => (409, "duplicate_sku", "Este SKU ja esta cadastrado na empresa."),
            _ => (409, "product_conflict", "O produto mudou. Atualize a pagina e tente novamente.")
        };
        return Results.Problem(statusCode: status, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["errors"] = result.Details ?? [] });
    }
}
