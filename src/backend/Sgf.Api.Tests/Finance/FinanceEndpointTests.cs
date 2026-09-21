using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sgf.Api.Tests.Identity;
using Sgf.Application.Finance;
using Sgf.Application.Identity;
using Sgf.Domain.Finance;
using Sgf.Infrastructure.Database;

namespace Sgf.Api.Tests.Finance;

public sealed class FinanceEndpointTests(RegisterUserAndCompanyApiFactory factory)
    : IClassFixture<RegisterUserAndCompanyApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private static CreateFinancialEntryRequest Valid(string type = "Income", decimal amount = 1000, string description = "Servico") =>
        new(type, description, " Servicos ", amount, new DateOnly(2026, 9, 20), " Observacao ");

    private async Task<(HttpClient Client, Guid Company)> Account()
    {
        var client = factory.CreateClient();
        var email = $"finance-{Guid.NewGuid():N}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register",
            new { name = "Teste Financeiro", email, password = "SenhaSegura123!", companyName = "Empresa Financeiro" });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "SenhaSegura123!" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return (client, token.CompanyId);
    }
    private static async Task<FinancialEntryResponse> Create(HttpClient client, CreateFinancialEntryRequest? request = null)
    {
        var response = await client.PostAsJsonAsync("/api/finance", request ?? Valid());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var entry = (await response.Content.ReadFromJsonAsync<FinancialEntryResponse>())!;
        Assert.EndsWith($"/api/finance/{entry.Id}", response.Headers.Location!.ToString());
        return entry;
    }

    [Fact]
    public async Task IncomeExpenseAndSummary_ReflectOnlyConfirmedPayments()
    {
        var (client, company) = await Account();
        Assert.Equal(new FinanceSummary(0, 0, 0, 0, 0), await client.GetFromJsonAsync<FinanceSummary>("/api/finance/summary"));
        var income = await Create(client);
        var expense = await Create(client, Valid("Expense", 300, "Aluguel"));
        foreach (var e in new[] { income, expense })
        {
            Assert.Equal("Pending", e.Status);
            Assert.Null(e.PaidAt);
            Assert.Equal(e.CreatedAt, e.UpdatedAt);
            Assert.Equal("Servicos", e.Category);
            Assert.Equal("Observacao", e.Notes);
        }
        Assert.Equal(new FinanceSummary(0, 0, 1000, 300, 0), await client.GetFromJsonAsync<FinanceSummary>("/api/finance/summary"));
        var payIncome = await client.PatchAsync($"/api/finance/{income.Id}/pay", null);
        Assert.Equal(HttpStatusCode.OK, payIncome.StatusCode);
        var first = (await payIncome.Content.ReadFromJsonAsync<FinancialEntryResponse>())!;
        Assert.Equal("Paid", first.Status);
        Assert.NotNull(first.PaidAt);
        var again = await client.PatchAsync($"/api/finance/{income.Id}/pay", null);
        Assert.Equal(first, await again.Content.ReadFromJsonAsync<FinancialEntryResponse>());
        Assert.Equal(new FinanceSummary(1000, 0, 0, 300, 1000), await client.GetFromJsonAsync<FinanceSummary>("/api/finance/summary"));
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsync($"/api/finance/{expense.Id}/pay", null)).StatusCode);
        Assert.Equal(new FinanceSummary(1000, 300, 0, 0, 700), await client.GetFromJsonAsync<FinanceSummary>("/api/finance/summary"));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        db.UseCurrentCompany(company);
        Assert.Equal(2, await db.FinancialEntries.CountAsync());
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("negative")]
    [InlineData("precision")]
    [InlineData("range")]
    [InlineData("description")]
    [InlineData("length")]
    [InlineData("date")]
    [InlineData("type")]
    [InlineData("missing")]
    public async Task InvalidData_IsRejectedBeforePersistence(string field)
    {
        var (client, _) = await Account();
        var request = field switch
        {
            "zero" => Valid() with { Amount = 0 },
            "negative" => Valid() with { Amount = -1 },
            "precision" => Valid() with { Amount = 0.001m },
            "range" => Valid() with { Amount = 1000000000000m },
            "description" => Valid() with { Description = " " },
            "length" => Valid() with { Description = new string('x', 201), Category = new string('x', 101), Notes = new string('x', 2001) },
            "date" => Valid() with { DueDate = null },
            "type" => Valid() with { Type = "0" },
            _ => Valid() with { Amount = null }
        };
        var response = await client.PostAsJsonAsync("/api/finance", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("exception", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, (await client.GetFromJsonAsync<FinancePage>("/api/finance"))!.TotalCount);
    }

    [Fact]
    public async Task OwnershipStatusAndDates_CannotBeForged_OnlyPendingCanBeEdited()
    {
        var (client, company) = await Account();
        var response = await client.PostAsJsonAsync("/api/finance", new
        {
            type = "Income", description = " Receita ", category = "", amount = 10.50m, dueDate = "2026-09-20",
            status = "Paid", paidAt = DateTimeOffset.UtcNow, companyId = Guid.NewGuid(), createdAt = DateTimeOffset.UnixEpoch
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var entry = (await response.Content.ReadFromJsonAsync<FinancialEntryResponse>())!;
        Assert.Equal("Pending", entry.Status);
        Assert.Null(entry.PaidAt);
        Assert.Null(entry.Category);
        Assert.Equal("Receita", entry.Description);
        var edit = new { description = "Editada", category = "Nova", amount = 20, dueDate = "2026-10-01", notes = "Nova",
            type = "Expense", status = "Paid", paidAt = DateTimeOffset.UtcNow, companyId = Guid.NewGuid(), createdAt = DateTimeOffset.UnixEpoch };
        var updated = await client.PutAsJsonAsync($"/api/finance/{entry.Id}", edit);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var changed = (await updated.Content.ReadFromJsonAsync<FinancialEntryResponse>())!;
        Assert.Equal("Income", changed.Type);
        Assert.Equal("Pending", changed.Status);
        Assert.Equal(20, changed.Amount);
        Assert.Equal(entry.CreatedAt, changed.CreatedAt);
        Assert.Equal(new DateOnly(2026, 10, 1), changed.DueDate);
        await client.PatchAsync($"/api/finance/{entry.Id}/pay", null);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/finance/{entry.Id}", edit)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        db.UseCurrentCompany(company);
        Assert.Equal(company, (await db.FinancialEntries.SingleAsync()).CompanyId);
    }

    [Fact]
    public async Task FiltersAndPagination_AreStableAndValidateInput()
    {
        var (client, _) = await Account();
        await Create(client, Valid("Income", 100, "Servico A") with { DueDate = new DateOnly(2026, 9, 1) });
        var b = await Create(client, Valid("Income", 200, "Servico B") with { DueDate = new DateOnly(2026, 9, 2) });
        await Create(client, Valid("Expense", 30, "Conta") with { Category = "Aluguel" });
        await client.PatchAsync($"/api/finance/{b.Id}/pay", null);
        var one = (await client.GetFromJsonAsync<FinancePage>("/api/finance?search=servico&pageSize=1"))!;
        var two = (await client.GetFromJsonAsync<FinancePage>("/api/finance?search=servico&pageSize=1&page=2"))!;
        Assert.Equal(2, one.TotalCount);
        Assert.Equal("Servico A", Assert.Single(one.Items).Description);
        Assert.Equal(b.Id, Assert.Single(two.Items).Id);
        Assert.Equal(b.Id, Assert.Single((await client.GetFromJsonAsync<FinancePage>(
            "/api/finance?type=Income&status=Paid&from=2026-09-02&to=2026-09-02&category=servicos"))!.Items).Id);
        Assert.Single((await client.GetFromJsonAsync<FinancePage>("/api/finance?type=Expense&status=Pending"))!.Items);
        foreach (var query in new[] { "page=0", "pageSize=101", "type=0", "status=Unknown", "from=2026-10-01&to=2026-09-01", "from=invalid" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/finance?" + query)).StatusCode);
    }

    [Fact]
    public async Task TenantIsolation_ProtectsGetListEditPaymentAndSummary()
    {
        var (a, _) = await Account();
        var (b, _) = await Account();
        var ea = await Create(a);
        var eb = await Create(b, Valid("Expense", 300));
        foreach (var (client, own, foreign) in new[] { (a, ea, eb), (b, eb, ea) })
        {
            Assert.Equal(own.Id, Assert.Single((await client.GetFromJsonAsync<FinancePage>("/api/finance"))!.Items).Id);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/finance/{own.Id}")).StatusCode);
            foreach (var id in new[] { foreign.Id, Guid.NewGuid() })
            {
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/finance/{id}")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/finance/{id}", new EditFinancialEntryRequest("X", null, 1, new DateOnly(2026, 9, 1), null))).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsync($"/api/finance/{id}/pay", null)).StatusCode);
            }
        }
        await a.PatchAsync($"/api/finance/{ea.Id}/pay", null);
        Assert.Equal(new FinanceSummary(1000, 0, 0, 0, 1000), await a.GetFromJsonAsync<FinanceSummary>("/api/finance/summary"));
        Assert.Equal(new FinanceSummary(0, 0, 0, 300, 0), await b.GetFromJsonAsync<FinanceSummary>("/api/finance/summary"));
    }

    [Fact]
    public async Task ConcurrentPayments_ConfirmOnceAndKeepTheSameTimestamp()
    {
        var (client, _) = await Account();
        var entry = await Create(client);
        var responses = await Task.WhenAll(client.PatchAsync($"/api/finance/{entry.Id}/pay", null), client.PatchAsync($"/api/finance/{entry.Id}/pay", null));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(await responses[0].Content.ReadFromJsonAsync<FinancialEntryResponse>(),
            await responses[1].Content.ReadFromJsonAsync<FinancialEntryResponse>());
        Assert.Equal(1000, (await client.GetFromJsonAsync<FinanceSummary>("/api/finance/summary"))!.Balance);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentEditAndPayment_CannotOverwriteEachOther(bool paymentFirst)
    {
        var (client, company) = await Account();
        var entry = await Create(client);
        await using var s1 = factory.Services.CreateAsyncScope();
        await using var s2 = factory.Services.CreateAsyncScope();
        var d1 = s1.ServiceProvider.GetRequiredService<SgfDbContext>();
        var d2 = s2.ServiceProvider.GetRequiredService<SgfDbContext>();
        d1.UseCurrentCompany(company);
        d2.UseCurrentCompany(company);
        var payment = await d1.FinancialEntries.SingleAsync();
        var edit = await d2.FinancialEntries.SingleAsync();
        payment.Pay();
        edit.Update("Editado", null, 200, edit.DueDate, null);
        await (paymentFirst ? d1 : d2).SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => (paymentFirst ? d2 : d1).SaveChangesAsync());
        var stored = (await client.GetFromJsonAsync<FinancialEntryResponse>($"/api/finance/{entry.Id}"))!;
        Assert.Equal(paymentFirst ? "Paid" : "Pending", stored.Status);
        Assert.Equal(paymentFirst ? 1000 : 200, stored.Amount);
    }

    [Fact]
    public async Task AnonymousAndInactiveTenant_AreRejected()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/finance")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/finance/summary")).StatusCode);
        var (client, company) = await Account();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        var membership = await db.Memberships.SingleAsync(m => m.CompanyId == company);
        db.Entry(membership).Property(m => m.IsActive).CurrentValue = false;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/finance")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/finance", Valid())).StatusCode);
        Assert.Empty(await db.FinancialEntries.ToArrayAsync());
    }

    [Fact]
    public async Task DetachedFinancialEntry_CannotUpdateAnotherCompany()
    {
        var (client, _) = await Account();
        var (_, otherCompany) = await Account();
        var entry = await Create(client);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        db.UseCurrentCompany(otherCompany);
        var forged = new FinancialEntry(FinancialEntryType.Income, "Forjado", null, 1, new DateOnly(2026, 9, 1), null);
        db.Entry(forged).Property(e => e.Id).CurrentValue = entry.Id;
        db.Entry(forged).Property(e => e.CompanyId).CurrentValue = otherCompany;
        db.FinancialEntries.Update(forged);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        Assert.Equal(1000, (await client.GetFromJsonAsync<FinancialEntryResponse>($"/api/finance/{entry.Id}"))!.Amount);
    }
}
