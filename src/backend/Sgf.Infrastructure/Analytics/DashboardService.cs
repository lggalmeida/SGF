using System.Data;
using Microsoft.EntityFrameworkCore;
using Sgf.Application.Analytics;
using Sgf.Application.Identity;
using Sgf.Domain.Finance;
using Sgf.Domain.Inventory;
using Sgf.Infrastructure.Database;
using Sgf.Infrastructure.Database.MultiTenancy;

namespace Sgf.Infrastructure.Analytics;

public sealed class DashboardService(SgfDbContext db, ICurrentTenantContext tenant, TimeProvider clock) : IDashboardService
{
    private sealed record FinancialTotals(decimal Received, decimal Paid, decimal PreviousReceived, decimal PreviousPaid,
        decimal Receivable, decimal Payable, decimal OverdueReceivable, decimal OverduePayable, int OverdueCount);

    public async Task<DashboardResponse?> GetAsync(string periodKey, CancellationToken ct)
    {
        if (await tenant.GetCurrentAsync(ct) is null) throw new TenantContextUnavailableException();
        var now = clock.GetUtcNow();
        var period = AnalysisPeriod.Resolve(periodKey, now);
        if (period is null) return null;
        var from = AnalysisPeriod.StartUtc(period.From);
        var until = AnalysisPeriod.StartUtc(period.To.AddDays(1));
        var previousFrom = AnalysisPeriod.StartUtc(period.PreviousFrom);
        var previousUntil = from;
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(AnalysisPeriod.OffsetHours)).DateTime);
        var staleSince = now.AddDays(-30);

        // Keep all blocks on one short, consistent PostgreSQL snapshot.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var totals = await db.FinancialEntries.AsNoTracking().GroupBy(e => 1)
            .Select(g => new FinancialTotals(
                g.Sum(e => e.Status == FinancialEntryStatus.Paid && e.Type == FinancialEntryType.Income && e.PaidAt >= from && e.PaidAt < until && e.PaidAt <= now ? e.Amount : 0),
                g.Sum(e => e.Status == FinancialEntryStatus.Paid && e.Type == FinancialEntryType.Expense && e.PaidAt >= from && e.PaidAt < until && e.PaidAt <= now ? e.Amount : 0),
                g.Sum(e => e.Status == FinancialEntryStatus.Paid && e.Type == FinancialEntryType.Income && e.PaidAt >= previousFrom && e.PaidAt < previousUntil ? e.Amount : 0),
                g.Sum(e => e.Status == FinancialEntryStatus.Paid && e.Type == FinancialEntryType.Expense && e.PaidAt >= previousFrom && e.PaidAt < previousUntil ? e.Amount : 0),
                g.Sum(e => e.Status == FinancialEntryStatus.Pending && e.Type == FinancialEntryType.Income ? e.Amount : 0),
                g.Sum(e => e.Status == FinancialEntryStatus.Pending && e.Type == FinancialEntryType.Expense ? e.Amount : 0),
                g.Sum(e => e.Status == FinancialEntryStatus.Pending && e.Type == FinancialEntryType.Income && e.DueDate < today ? e.Amount : 0),
                g.Sum(e => e.Status == FinancialEntryStatus.Pending && e.Type == FinancialEntryType.Expense && e.DueDate < today ? e.Amount : 0),
                g.Count(e => e.Status == FinancialEntryStatus.Pending && e.DueDate < today)
            )).SingleOrDefaultAsync(ct) ?? new(0, 0, 0, 0, 0, 0, 0, 0, 0);
        var finance = new FinancialIndicators(totals.Received, totals.Paid, totals.Received - totals.Paid,
            totals.Receivable, totals.Payable, totals.OverdueReceivable, totals.OverduePayable, totals.OverdueCount);
        var comparisons = new FinancialComparisons(MetricComparison.Create(totals.Received, totals.PreviousReceived),
            MetricComparison.Create(totals.Paid, totals.PreviousPaid));

        var monthly = period.Granularity == "month";
        var buckets = await db.FinancialEntries.AsNoTracking()
            .Where(e => e.Status == FinancialEntryStatus.Paid && e.PaidAt >= from && e.PaidAt < until && e.PaidAt <= now)
            .GroupBy(e => new {
                Year = e.PaidAt!.Value.UtcDateTime.AddHours(AnalysisPeriod.OffsetHours).Year,
                Month = e.PaidAt!.Value.UtcDateTime.AddHours(AnalysisPeriod.OffsetHours).Month,
                Day = monthly ? 1 : e.PaidAt!.Value.UtcDateTime.AddHours(AnalysisPeriod.OffsetHours).Day
            })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day,
                Received = g.Sum(e => e.Type == FinancialEntryType.Income ? e.Amount : 0),
                Paid = g.Sum(e => e.Type == FinancialEntryType.Expense ? e.Amount : 0) })
            .ToArrayAsync(ct);
        var byDate = buckets.ToDictionary(b => new DateOnly(b.Year, b.Month, b.Day));
        var series = new List<FinancialPoint>();
        for (var date = monthly ? new DateOnly(period.From.Year, period.From.Month, 1) : period.From;
            date <= period.To; date = monthly ? date.AddMonths(1) : date.AddDays(1))
        {
            byDate.TryGetValue(date, out var bucket);
            series.Add(new(date, bucket?.Received ?? 0, bucket?.Paid ?? 0));
        }

        var active = db.Products.AsNoTracking().Where(p => p.IsActive);
        var stock = await active.GroupBy(p => 1).Select(g => new {
            Active = g.Count(), Low = g.Count(p => p.CurrentStock <= p.MinimumStock),
            Zero = g.Count(p => p.CurrentStock == 0), Value = g.Sum(p => p.CurrentStock * p.CostPrice)
        }).SingleOrDefaultAsync(ct);
        var lowStock = await active.Where(p => p.CurrentStock <= p.MinimumStock)
            .OrderBy(p => p.CurrentStock != 0).ThenBy(p => p.CurrentStock - p.MinimumStock).ThenBy(p => p.Name).ThenBy(p => p.Id)
            .Take(10).Select(p => new LowStockProduct(p.Id, p.Name, p.SKU, p.CurrentStock, p.MinimumStock)).ToArrayAsync(ct);
        var stale = active.Where(p => p.CreatedAt <= staleSince && !db.InventoryMovements
            .Any(m => m.ProductId == p.Id && m.CreatedAt >= staleSince && m.CreatedAt <= now));
        var staleCount = await stale.CountAsync(ct);
        var staleProducts = await stale.Select(p => new {
            p.Id, p.Name, p.SKU, p.CurrentStock,
            LastMovement = db.InventoryMovements.Where(m => m.ProductId == p.Id && m.CreatedAt <= now)
                .Select(m => (DateTimeOffset?)m.CreatedAt).Max()
        }).OrderBy(p => p.LastMovement != null).ThenBy(p => p.LastMovement).ThenBy(p => p.Name).ThenBy(p => p.Id)
            .Take(10).Select(p => new StaleProduct(p.Id, p.Name, p.SKU, p.CurrentStock, p.LastMovement)).ToArrayAsync(ct);
        var inventory = new InventoryIndicators(stock?.Active ?? 0, stock?.Low ?? 0, stock?.Zero ?? 0,
            stock?.Value ?? 0, staleCount);

        var movements = db.InventoryMovements.AsNoTracking().Where(m => m.CreatedAt >= from && m.CreatedAt < until && m.CreatedAt <= now);
        var movementSummary = await movements.GroupBy(m => 1).Select(g => new MovementIndicators(
            g.Sum(m => m.Type == InventoryMovementType.Entry ? m.Quantity : 0),
            g.Sum(m => m.Type == InventoryMovementType.Exit ? m.Quantity : 0), g.Count()))
            .SingleOrDefaultAsync(ct) ?? new(0, 0, 0);
        // Include historic movements even if the product is inactive today.
        var top = await movements.Where(m => m.Type == InventoryMovementType.Exit)
            .GroupBy(m => new { m.ProductId, m.Product.Name, m.Product.SKU })
            .Select(g => new { Id = g.Key.ProductId, g.Key.Name, g.Key.SKU, Quantity = g.Sum(m => m.Quantity) })
            .OrderByDescending(p => p.Quantity).ThenBy(p => p.Name).ThenBy(p => p.Id).Take(10)
            .Select(p => new StockOutProduct(p.Id, p.Name, p.SKU, p.Quantity)).ToArrayAsync(ct);
        await transaction.CommitAsync(ct);
        return new(now, period, finance, comparisons, series.ToArray(), inventory, movementSummary,
            lowStock, top, staleProducts, InsightRules.Evaluate(finance, comparisons, inventory, top));
    }
}
