using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sgf.Api.Tests.Identity;
using Sgf.Application.Analytics;
using Sgf.Application.Identity;
using Sgf.Domain.Finance;
using Sgf.Domain.Inventory;
using Sgf.Domain.Products;
using Sgf.Infrastructure.Database;

namespace Sgf.Api.Tests.Analytics;

public sealed class DashboardEndpointTests : IClassFixture<RegisterUserAndCompanyApiFactory>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 15, 0, 0, TimeSpan.Zero);
    private readonly RegisterUserAndCompanyApiFactory factory;
    private readonly WebApplicationFactory<Program> host;
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    public DashboardEndpointTests(RegisterUserAndCompanyApiFactory factory)
    {
        this.factory = factory;
        host = factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<TimeProvider>(new Clock())));
    }
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public async Task DisposeAsync() => await host.DisposeAsync();

    private async Task<(HttpClient Client, Guid Company, string User)> Account()
    {
        var client = host.CreateClient();
        var email = $"analytics-{Guid.NewGuid():N}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register",
            new { name = "Ana Analytics", email, password = "SenhaSegura123!", companyName = "Empresa Analytics" });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "SenhaSegura123!" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return (client, token.CompanyId, token.UserId);
    }

    private async Task Seed(Guid company, string user)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        db.UseCurrentCompany(company);
        Product Product(string name, decimal cost, decimal minimum, int age)
        {
            var p = new Product(name, name, null, cost, cost, minimum);
            db.Products.Add(p);
            db.Entry(p).Property(e => e.CreatedAt).CurrentValue = Now.AddDays(-age);
            return p;
        }
        var low = Product("Mouse", 10, 5, 60);
        var zero = Product("Teclado", 20, 2, 60);
        var stale = Product("Cabo", 2, 1, 60);
        Product("Parado", 1, 0, 60);
        Product("Novo", 1, 0, 1);
        var inactive = Product("Inativo", 5, 1, 60);
        await db.SaveChangesAsync();
        void Move(Product p, InventoryMovementType type, decimal quantity, int days)
        {
            var m = p.RecordMovement(type, quantity, null, user);
            db.InventoryMovements.Add(m);
            // Historical timestamps exist only in this test fixture, before insertion.
            db.Entry(m).Property(e => e.CreatedAt).CurrentValue = Now.AddDays(-days);
        }
        Move(low, InventoryMovementType.Entry, 10, 5);
        Move(low, InventoryMovementType.Exit, 7, 3);
        Move(zero, InventoryMovementType.Entry, 4, 10);
        Move(zero, InventoryMovementType.Exit, 4, 2);
        Move(stale, InventoryMovementType.Entry, 8, 40);
        Move(inactive, InventoryMovementType.Entry, 5, 4);
        Move(inactive, InventoryMovementType.Exit, 3, 1);
        inactive.SetActive(false);
        var period = AnalysisPeriod.Resolve("last30", Now)!;
        void Entry(FinancialEntryType type, decimal amount, DateOnly due, DateTimeOffset? paid)
        {
            var e = new FinancialEntry(type, "Lancamento", null, amount, due, null);
            if (paid.HasValue) e.Pay();
            db.FinancialEntries.Add(e);
            if (paid.HasValue) db.Entry(e).Property(x => x.PaidAt).CurrentValue = paid;
        }
        var today = new DateOnly(2026, 9, 15);
        Entry(FinancialEntryType.Income, 1000, today, Now.AddDays(-3));
        Entry(FinancialEntryType.Income, 250, today, AnalysisPeriod.StartUtc(period.From));
        Entry(FinancialEntryType.Income, 1000, today, AnalysisPeriod.StartUtc(period.From).AddTicks(-10));
        Entry(FinancialEntryType.Expense, 300, today, Now.AddDays(-2));
        Entry(FinancialEntryType.Expense, 200, today, AnalysisPeriod.StartUtc(period.PreviousFrom));
        Entry(FinancialEntryType.Expense, 9999, today, AnalysisPeriod.StartUtc(period.PreviousFrom).AddTicks(-10));
        Entry(FinancialEntryType.Income, 9999, today, Now.AddSeconds(1));
        Entry(FinancialEntryType.Income, 400, today.AddDays(-1), null);
        Entry(FinancialEntryType.Income, 100, today.AddMonths(2), null);
        Entry(FinancialEntryType.Expense, 200, today.AddDays(-1), null);
        Entry(FinancialEntryType.Expense, 50, today, null);
        await db.SaveChangesAsync();
    }

    private static async Task<DashboardResponse> Read(HttpClient client, string period = "last30")
    {
        var response = await client.GetAsync("/api/dashboard?period=" + period);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DashboardResponse>())!;
    }

    [Fact]
    public async Task FinancialTotalsPeriodsAndSeries_AreCorrectAtBoundaries()
    {
        var (client, company, user) = await Account();
        await Seed(company, user);
        var d = await Read(client);
        Assert.Equal(new FinancialIndicators(1250, 300, 950, 500, 250, 400, 200, 2), d.Financial);
        Assert.Equal(new MetricComparison(1250, 1000, 25), d.Comparisons.Received);
        Assert.Equal(new MetricComparison(300, 200, 50), d.Comparisons.Paid);
        Assert.Equal(30, d.FinancialSeries.Length);
        Assert.Equal(1250, d.FinancialSeries.Sum(x => x.Received));
        Assert.Equal(300, d.FinancialSeries.Sum(x => x.Paid));
        Assert.Equal(250, d.FinancialSeries[0].Received);
        Assert.Equal(d.Period.From, d.FinancialSeries[0].Date);
        Assert.Equal(d.Period.To, d.FinancialSeries[^1].Date);
        Assert.Equal(0, d.FinancialSeries[^1].Received); // Future timestamps are excluded.
        var monthly = await Read(client, "last90");
        Assert.Equal("month", monthly.Period.Granularity);
        Assert.InRange(monthly.FinancialSeries.Length, 3, 4);
        Assert.Equal(monthly.Financial.Received, monthly.FinancialSeries.Sum(x => x.Received));
        Assert.Equal(monthly.Financial.Paid, monthly.FinancialSeries.Sum(x => x.Paid));
        Assert.Equal(500, monthly.Financial.Receivable);
    }

    [Fact]
    public async Task InventoryStateMovementsAndInsights_UseCorrectScopeAndAge()
    {
        var (client, company, user) = await Account();
        await Seed(company, user);
        var d = await Read(client);
        Assert.Equal(new InventoryIndicators(5, 4, 3, 46, 2), d.Inventory);
        Assert.Equal(new MovementIndicators(19, 14, 6), d.Movements);
        Assert.Equal("Mouse", d.TopStockOut[0].Name);
        Assert.Equal(7, d.TopStockOut[0].Quantity);
        Assert.Contains(d.TopStockOut, p => p.Name == "Inativo");
        Assert.DoesNotContain(d.LowStock, p => p.Name == "Inativo");
        Assert.Equal(0, d.LowStock[0].CurrentStock);
        Assert.Equal(new[] { "Parado", "Cabo" }, d.StaleProducts.Select(p => p.Name));
        Assert.Null(d.StaleProducts[0].LastMovementAt);
        Assert.Equal(Now.AddDays(-40), d.StaleProducts[1].LastMovementAt);
        Assert.DoesNotContain(d.StaleProducts, p => p.Name == "Novo");
        Assert.Equal(new[] { "zero_stock", "low_stock", "overdue_payable", "overdue_receivable",
            "expense_increase", "stale_products", "top_stock_out" }, d.Insights.Select(i => i.Code));
        Assert.All(d.Insights, i => Assert.False(string.IsNullOrWhiteSpace(i.Evidence)));
        var previousMonth = await Read(client, "previousMonth");
        Assert.Equal(d.Inventory, previousMonth.Inventory); // Current state is not historic stock.
        Assert.Equal(d.Financial.OverdueCount, previousMonth.Financial.OverdueCount);
    }

    [Fact]
    public async Task EveryAggregateAndList_IsIsolatedFromOtherCompany()
    {
        var (a, companyA, userA) = await Account();
        var (b, companyB, userB) = await Account();
        var emptyB = await Read(b);
        await Seed(companyA, userA);
        Assert.Equal(JsonSerializer.Serialize(emptyB), JsonSerializer.Serialize(await Read(b)));
        var aBefore = await Read(a);
        await Seed(companyB, userB);
        Assert.Equal(JsonSerializer.Serialize(aBefore), JsonSerializer.Serialize(await Read(a)));
        var forged = await a.GetFromJsonAsync<DashboardResponse>("/api/dashboard?companyId=" + companyB);
        Assert.Equal(JsonSerializer.Serialize(aBefore), JsonSerializer.Serialize(forged));
        Assert.NotEqual((await Read(b)).LowStock[0].Id, aBefore.LowStock[0].Id);
    }

    [Fact]
    public async Task EmptyCompany_HasZerosNoInsightsAndNoPercentages()
    {
        var (client, _, _) = await Account();
        foreach (var key in new[] { "last30", "currentMonth", "previousMonth", "last90" })
        {
            var d = await Read(client, key);
            Assert.Equal(new FinancialIndicators(0, 0, 0, 0, 0, 0, 0, 0), d.Financial);
            Assert.Equal(new InventoryIndicators(0, 0, 0, 0, 0), d.Inventory);
            Assert.Equal(new MovementIndicators(0, 0, 0), d.Movements);
            Assert.Null(d.Comparisons.Paid.ChangePercent);
            Assert.Null(d.Comparisons.Received.ChangePercent);
            Assert.Empty(d.Insights);
            Assert.Empty(d.LowStock);
            Assert.Empty(d.TopStockOut);
            Assert.Empty(d.StaleProducts);
            Assert.All(d.FinancialSeries, p => { Assert.Equal(0, p.Received); Assert.Equal(0, p.Paid); });
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/dashboard?period=invalid")).StatusCode);
    }

    [Fact]
    public async Task AnonymousAndInactiveMembership_AreDenied()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().GetAsync("/api/dashboard")).StatusCode);
        var (client, company, _) = await Account();
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        var membership = await db.Memberships.SingleAsync(m => m.CompanyId == company);
        db.Entry(membership).Property(m => m.IsActive).CurrentValue = false;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/dashboard")).StatusCode);
    }

    [Fact]
    public async Task ListsAreBounded_AndRecentMovementPreventsStaleClassification()
    {
        var (client, company, user) = await Account();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
            db.UseCurrentCompany(company);
            for (var i = 0; i < 12; i++)
            {
                var p = new Product($"Produto {i:D2}", $"SKU{i}", null, 1, 2, 5);
                db.Products.Add(p);
                db.Entry(p).Property(e => e.CreatedAt).CurrentValue = Now.AddDays(-31);
                await db.SaveChangesAsync();
                if (i == 0)
                {
                    var m = p.RecordMovement(InventoryMovementType.Entry, 1, null, user);
                    db.InventoryMovements.Add(m);
                    db.Entry(m).Property(e => e.CreatedAt).CurrentValue = Now.AddDays(-30); // Inclusive cutoff.
                    await db.SaveChangesAsync();
                }
            }
        }
        var d = await Read(client);
        Assert.Equal(12, d.Inventory.LowStockProducts);
        Assert.Equal(11, d.Inventory.StaleProducts);
        Assert.Equal(10, d.LowStock.Length);
        Assert.Equal(10, d.StaleProducts.Length);
        Assert.DoesNotContain(d.StaleProducts, p => p.Name == "Produto 00");
    }
}
