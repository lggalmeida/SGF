using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Sgf.Application.Finance;
using Sgf.Infrastructure.Database.MultiTenancy;

namespace Sgf.Api.Finance;

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/finance").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (TenantContextUnavailableException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is DbUpdateException or DbException)
            { return Results.Problem(statusCode: 500, title: "Nao foi possivel concluir a operacao."); }
        });
        group.MapGet("", async (IFinanceService service, CancellationToken ct, int page = 1, int pageSize = 20,
            string? search = null, string? type = null, string? status = null,
            DateOnly? from = null, DateOnly? to = null, string? category = null) =>
            Respond(await service.ListAsync(new(page, pageSize, search, type, status, from, to, category), ct)));
        group.MapGet("/summary", async (IFinanceService service, CancellationToken ct) => Results.Ok(await service.SummaryAsync(ct)));
        group.MapGet("/{id:guid}", async (Guid id, IFinanceService service, CancellationToken ct) => Respond(await service.GetAsync(id, ct)));
        group.MapPost("", async (CreateFinancialEntryRequest request, IFinanceService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.Error is null && result.Value is not null
                ? Results.Created($"/api/finance/{result.Value.Id}", result.Value) : Respond(result);
        });
        group.MapPut("/{id:guid}", async (Guid id, EditFinancialEntryRequest request, IFinanceService service, CancellationToken ct) =>
            Respond(await service.UpdateAsync(id, request, ct)));
        group.MapPatch("/{id:guid}/pay", async (Guid id, IFinanceService service, CancellationToken ct) =>
            Respond(await service.PayAsync(id, ct)));
    }

    private static IResult Respond<T>(FinanceResult<T> result)
    {
        if (result.Error is null) return Results.Ok(result.Value);
        var (status, code, title) = result.Error switch
        {
            FinanceError.Validation => (400, "finance_validation", "Confira os dados do lancamento."),
            FinanceError.NotFound => (404, "finance_not_found", "Lancamento nao encontrado."),
            FinanceError.PaidEntry => (409, "finance_paid", "Lancamento pago nao pode ser editado."),
            _ => (409, "finance_conflict", "O lancamento mudou. Atualize a lista antes de continuar.")
        };
        return Results.Problem(statusCode: status, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["errors"] = result.Details ?? [] });
    }
}
