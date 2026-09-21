namespace Sgf.Application.Analytics;

public sealed record AnalysisPeriod(string Key, DateOnly From, DateOnly To,
    DateOnly PreviousFrom, DateOnly PreviousTo, string Granularity, string TimeZone)
{
    public const int OffsetHours = -3;

    public static AnalysisPeriod? Resolve(string key, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(OffsetHours)).DateTime);
        var month = new DateOnly(today.Year, today.Month, 1);
        var from = key switch
        {
            "last30" => today.AddDays(-29),
            "currentMonth" => month,
            "previousMonth" => month.AddMonths(-1),
            "last90" => today.AddDays(-89),
            _ => (DateOnly?)null
        };
        if (from is null) return null;
        var to = key == "previousMonth" ? month.AddDays(-1) : today;
        var days = to.DayNumber - from.Value.DayNumber + 1;
        return new(key, from.Value, to, from.Value.AddDays(-days), from.Value.AddDays(-1),
            days <= 31 ? "day" : "month", "UTC-03:00");
    }

    public static DateTimeOffset StartUtc(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(OffsetHours)).ToUniversalTime();
}

public sealed record FinancialIndicators(decimal Received, decimal Paid, decimal Balance,
    decimal Receivable, decimal Payable, decimal OverdueReceivable, decimal OverduePayable, int OverdueCount);
public sealed record MetricComparison(decimal Current, decimal Previous, decimal? ChangePercent)
{
    public static MetricComparison Create(decimal current, decimal previous) =>
        new(current, previous, previous == 0 ? null : decimal.Round((current - previous) / previous * 100m, 1));
}
public sealed record FinancialComparisons(MetricComparison Received, MetricComparison Paid);
public sealed record FinancialPoint(DateOnly Date, decimal Received, decimal Paid);
public sealed record InventoryIndicators(int ActiveProducts, int LowStockProducts, int ZeroStockProducts,
    decimal EstimatedCostValue, int StaleProducts);
public sealed record MovementIndicators(decimal Entries, decimal Exits, int Count);
public sealed record LowStockProduct(Guid Id, string Name, string SKU, decimal CurrentStock, decimal MinimumStock);
public sealed record StockOutProduct(Guid Id, string Name, string SKU, decimal Quantity);
public sealed record StaleProduct(Guid Id, string Name, string SKU, decimal CurrentStock, DateTimeOffset? LastMovementAt);
public sealed record AnalysisInsight(string Code, string Level, string Message, string Evidence);
public sealed record DashboardResponse(DateTimeOffset AsOf, AnalysisPeriod Period,
    FinancialIndicators Financial, FinancialComparisons Comparisons, FinancialPoint[] FinancialSeries,
    InventoryIndicators Inventory, MovementIndicators Movements, LowStockProduct[] LowStock,
    StockOutProduct[] TopStockOut, StaleProduct[] StaleProducts, AnalysisInsight[] Insights);

public interface IDashboardService
{
    Task<DashboardResponse?> GetAsync(string period, CancellationToken ct);
}
