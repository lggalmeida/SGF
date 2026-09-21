using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Sgf.Application.Inventory;
using Sgf.Infrastructure.Database.MultiTenancy;

namespace Sgf.Api.Inventory;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/inventory").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (TenantContextUnavailableException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is DbUpdateException or DbException)
            {
                return Results.Problem(statusCode: 500, title: "Nao foi possivel registrar a operacao de estoque.");
            }
        });
        group.MapGet("", async (IInventoryService service, CancellationToken ct, int page = 1, int pageSize = 20,
            string? search = null, bool lowStock = false) => Respond(await service.ListAsync(page, pageSize, search, lowStock, ct)));
        group.MapGet("/movements", async (IInventoryService service, CancellationToken ct, int page = 1, int pageSize = 20,
            Guid? productId = null, string? type = null) => Respond(await service.HistoryAsync(page, pageSize, productId, type, ct)));
        group.MapPost("/entries", async (InventoryRequest request, IInventoryService service, CancellationToken ct) =>
            Respond(await service.EntryAsync(request, ct), true));
        group.MapPost("/exits", async (InventoryRequest request, IInventoryService service, CancellationToken ct) =>
            Respond(await service.ExitAsync(request, ct), true));
    }

    private static IResult Respond<T>(InventoryResult<T> result, bool created = false)
    {
        if (result.Error is null) return Results.Json(result.Value, statusCode: created ? 201 : 200);
        var (status, code, title) = result.Error switch
        {
            InventoryError.Validation => (400, "inventory_validation", "Informe quantidade positiva com ate tres casas decimais e observacao de ate 1000 caracteres."),
            InventoryError.NotFound => (404, "product_not_found", "Produto nao encontrado."),
            InventoryError.InactiveProduct => (409, "inactive_product", "Produto inativo nao pode ser movimentado."),
            InventoryError.InsufficientStock => (409, "insufficient_stock", "Estoque insuficiente para esta saida."),
            InventoryError.StockLimit => (409, "stock_limit", "A entrada ultrapassa o limite de saldo permitido."),
            _ => (409, "stock_conflict", "O saldo ou status mudou. Atualize e confira antes de tentar novamente.")
        };
        return Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
