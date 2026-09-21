using Sgf.Application.Analytics;

namespace Sgf.Application.Tests;

public sealed class AnalyticsRulesTests
{
    [Theory]
    [InlineData("last30", "2026-03-02", "2026-03-31", "2026-01-31", "2026-03-01", "day")]
    [InlineData("currentMonth", "2026-03-01", "2026-03-31", "2026-01-29", "2026-02-28", "day")]
    [InlineData("previousMonth", "2026-02-01", "2026-02-28", "2026-01-04", "2026-01-31", "day")]
    [InlineData("last90", "2026-01-01", "2026-03-31", "2025-10-03", "2025-12-31", "month")]
    public void PeriodsUseEqualLengthContiguousComparisons(string key, string from, string to, string previousFrom, string previousTo, string granularity)
    {
        var p = AnalysisPeriod.Resolve(key, DateTimeOffset.Parse("2026-03-31T18:00:00Z"))!;
        Assert.Equal(DateOnly.Parse(from), p.From);
        Assert.Equal(DateOnly.Parse(to), p.To);
        Assert.Equal(DateOnly.Parse(previousFrom), p.PreviousFrom);
        Assert.Equal(DateOnly.Parse(previousTo), p.PreviousTo);
        Assert.Equal(granularity, p.Granularity);
        Assert.Equal(p.To.DayNumber - p.From.DayNumber, p.PreviousTo.DayNumber - p.PreviousFrom.DayNumber);
    }

    [Fact]
    public void PeriodUsesReportingDayRatherThanServerTimezone_AndHandlesLeapYear()
    {
        var p = AnalysisPeriod.Resolve("currentMonth", DateTimeOffset.Parse("2024-03-01T02:59:59Z"))!;
        Assert.Equal(new DateOnly(2024, 2, 29), p.To);
        Assert.Equal(DateTimeOffset.Parse("2024-02-01T03:00:00Z"), AnalysisPeriod.StartUtc(p.From));
        Assert.Null(AnalysisPeriod.Resolve("unknown", DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(100, 0, null)]
    [InlineData(0, 100, -100)]
    [InlineData(1000, 800, 25)]
    public void ComparisonNeverInventsPercentageWithoutBase(int current, int previous, int? percent)
    {
        Assert.Equal(percent.HasValue ? (decimal?)percent.Value : null, MetricComparison.Create(current, previous).ChangePercent);
    }

    [Fact]
    public void InsightsUseThresholdsAndSuppressIrrelevantMessages()
    {
        var finance = new FinancialIndicators(900, 109, 791, 0, 0, 0, 0, 0);
        var inventory = new InventoryIndicators(1, 0, 0, 100, 0);
        var comparisons = new FinancialComparisons(MetricComparison.Create(900, 1000), MetricComparison.Create(109, 100));
        var insights = InsightRules.Evaluate(finance, comparisons, inventory, []);
        Assert.Equal("income_decrease", Assert.Single(insights).Code);
        Assert.Empty(InsightRules.Evaluate(finance, new(MetricComparison.Create(0, 0), MetricComparison.Create(100, 0)), inventory, []));
    }
}
