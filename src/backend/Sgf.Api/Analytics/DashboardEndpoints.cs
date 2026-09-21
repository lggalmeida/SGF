using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Sgf.Application.Analytics;
using Sgf.Infrastructure.Database.MultiTenancy;

namespace Sgf.Api.Analytics;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/dashboard", async (IDashboardService service, CancellationToken ct, string period = "last30") =>
        {
            var result = await service.GetAsync(period, ct);
            return result is null
                ? Results.Problem(statusCode: 400, title: "Periodo invalido.", extensions: new Dictionary<string, object?> { ["code"] = "invalid_period" })
                : Results.Ok(result);
        }).RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (TenantContextUnavailableException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is DbException or DbUpdateException)
            { return Results.Problem(statusCode: 500, title: "Nao foi possivel carregar os indicadores."); }
        });
    }
}
