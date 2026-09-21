using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sgf.Api.Tests.Identity;
using Sgf.Application.Identity;
using Sgf.Application.Inventory;
using Sgf.Application.Products;
using Sgf.Domain.Inventory;
using Sgf.Infrastructure.Database;

namespace Sgf.Api.Tests.Inventory;

public sealed class InventoryEndpointTests(RegisterUserAndCompanyApiFactory factory)
    : IClassFixture<RegisterUserAndCompanyApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(HttpClient Client, LoginResponse User, ProductResponse Product)> Setup(decimal minimum = 0)
    {
        var client = factory.CreateClient();
        var email = $"inventory-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/api/auth/register", new { name = "Operador", email, password = "SenhaSegura123!", companyName = "Empresa" })).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "SenhaSegura123!" });
        var user = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        var created = await client.PostAsJsonAsync("/api/products", new SaveProductRequest("Mouse", "MOU-001", null, 1, 2, minimum));
        created.EnsureSuccessStatusCode();
        return (client, user, (await created.Content.ReadFromJsonAsync<ProductResponse>())!);
    }

    private static Task<HttpResponseMessage> Move(HttpClient client, Guid id, decimal quantity, string kind = "entries") =>
        client.PostAsJsonAsync("/api/inventory/" + kind, new { productId = id, quantity, notes = "Manual" });
    private static async Task<ProductResponse> Product(HttpClient c, Guid id) =>
        (await c.GetFromJsonAsync<ProductResponse>($"/api/products/{id}"))!;

    [Fact]
    public async Task EntryExitAndHistory_PreserveBalancesAndTrustedIdentity()
    {
        var (client, user, p) = await Setup();
        Assert.Equal(0, p.CurrentStock);
        Assert.Equal(0, p.MinimumStock);
        var entry = await client.PostAsJsonAsync("/api/inventory/entries", new
        { productId = p.Id, quantity = 10.5m, notes = " Inicial ", companyId = Guid.NewGuid(), userId = "forged" });
        Assert.Equal(HttpStatusCode.Created, entry.StatusCode);
        Assert.Equal(10.5m, (await Product(client, p.Id)).CurrentStock);
        Assert.Equal(HttpStatusCode.Created, (await Move(client, p.Id, .25m, "exits")).StatusCode);
        Assert.Equal(10.25m, (await Product(client, p.Id)).CurrentStock);
        var history = (await client.GetFromJsonAsync<MovementPage>("/api/inventory/movements"))!;
        Assert.Equal(2, history.TotalCount);
        Assert.Equal(new[] { "Exit", "Entry" }, history.Items.Select(m => m.Type));
        Assert.Equal(.25m, history.Items[0].Quantity);
        Assert.Equal("Inicial", history.Items[1].Notes);
        Assert.All(history.Items, m => { Assert.Equal(user.UserId, m.UserId); Assert.Equal("Operador", m.UserName); });
        var page = (await client.GetFromJsonAsync<MovementPage>($"/api/inventory/movements?productId={p.Id}&type=Entry&pageSize=1"))!;
        Assert.Equal("Entry", Assert.Single(page.Items).Type);
        Assert.True(history.Items[0].CreatedAt >= history.Items[1].CreatedAt);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.0001")]
    [InlineData("100000000000")]
    public async Task InvalidQuantity_DoesNotWrite(string quantity)
    {
        var (client, _, p) = await Setup();
        var value = decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture);
        foreach (var kind in new[] { "entries", "exits" })
            Assert.Equal(HttpStatusCode.BadRequest, (await Move(client, p.Id, value, kind)).StatusCode);
        Assert.Equal(0, (await Product(client, p.Id)).CurrentStock);
        Assert.Equal(0, (await client.GetFromJsonAsync<MovementPage>("/api/inventory/movements"))!.TotalCount);
    }

    [Fact]
    public async Task InsufficientStock_AndInactiveProduct_AreRejected()
    {
        var (client, _, p) = await Setup();
        await Move(client, p.Id, 5);
        var failed = await Move(client, p.Id, 6, "exits");
        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
        Assert.Contains("insufficient_stock", await failed.Content.ReadAsStringAsync());
        await client.PatchAsJsonAsync($"/api/products/{p.Id}/status", new { isActive = false });
        foreach (var kind in new[] { "entries", "exits" })
            Assert.Equal(HttpStatusCode.Conflict, (await Move(client, p.Id, 1, kind)).StatusCode);
        Assert.Equal(5, (await Product(client, p.Id)).CurrentStock);
        Assert.Equal(1, (await client.GetFromJsonAsync<MovementPage>("/api/inventory/movements"))!.TotalCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DatabaseFailure_RollsBackBalanceAndHistory(bool failMovement)
    {
        var (client, _, p) = await Setup();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
            await db.Database.ExecuteSqlRawAsync(failMovement
                ? "ALTER TABLE \"InventoryMovements\" ADD CONSTRAINT \"TestReject\" CHECK (false)"
                : "ALTER TABLE \"Products\" ADD CONSTRAINT \"TestReject\" CHECK (\"CurrentStock\" = 0)");
        }
        var response = await Move(client, p.Id, 10);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("TestReject", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, (await Product(client, p.Id)).CurrentStock);
        Assert.Equal(0, (await client.GetFromJsonAsync<MovementPage>("/api/inventory/movements"))!.TotalCount);
    }

    [Fact]
    public async Task ConcurrentHttpExits_OnlyOneSucceeds()
    {
        var (client, _, p) = await Setup();
        await Move(client, p.Id, 5);
        var responses = await Task.WhenAll(Move(client, p.Id, 4, "exits"), Move(client, p.Id, 4, "exits"));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, (await Product(client, p.Id)).CurrentStock);
        var movements = (await client.GetFromJsonAsync<MovementPage>("/api/inventory/movements"))!;
        Assert.Equal(2, movements.TotalCount);
        Assert.Single(movements.Items, m => m.Type == "Exit");
    }

    [Fact]
    public async Task TwoReadersOfSameBalance_ConcurrencyTokenRollsBackLoser()
    {
        var (client, user, p) = await Setup();
        await Move(client, p.Id, 5);
        await using var scope1 = factory.Services.CreateAsyncScope();
        await using var scope2 = factory.Services.CreateAsyncScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<SgfDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<SgfDbContext>();
        db1.UseCurrentCompany(user.CompanyId);
        db2.UseCurrentCompany(user.CompanyId);
        // Both transactions prepare their exit from the same persisted balance.
        var p1 = await db1.Products.SingleAsync();
        var p2 = await db2.Products.SingleAsync();
        db1.InventoryMovements.Add(p1.RecordMovement(InventoryMovementType.Exit, 4, null, user.UserId));
        db2.InventoryMovements.Add(p2.RecordMovement(InventoryMovementType.Exit, 4, null, user.UserId));
        await db1.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
        Assert.Equal(1, (await Product(client, p.Id)).CurrentStock);
        Assert.Equal(2, (await client.GetFromJsonAsync<MovementPage>("/api/inventory/movements"))!.TotalCount);
    }

    [Fact]
    public async Task DeactivationAfterRead_PreventsStaleMovement()
    {
        var (client, user, p) = await Setup();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        db.UseCurrentCompany(user.CompanyId);
        var stale = await db.Products.SingleAsync();
        await client.PatchAsJsonAsync($"/api/products/{p.Id}/status", new { isActive = false });
        db.InventoryMovements.Add(stale.RecordMovement(InventoryMovementType.Entry, 1, null, user.UserId));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        Assert.Equal(0, (await Product(client, p.Id)).CurrentStock);
        Assert.Equal(0, (await client.GetFromJsonAsync<MovementPage>("/api/inventory/movements"))!.TotalCount);
    }

    [Fact]
    public async Task Tenants_CannotReadOrMoveEachOthersProducts()
    {
        var (a, _, pa) = await Setup();
        var (b, _, pb) = await Setup();
        await Move(a, pa.Id, 10);
        await Move(b, pb.Id, 20);
        foreach (var (client, foreign, own) in new[] { (a, pb, pa), (b, pa, pb) })
        {
            foreach (var kind in new[] { "entries", "exits" })
                Assert.Equal(HttpStatusCode.NotFound, (await Move(client, foreign.Id, 1, kind)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/inventory/movements?productId={foreign.Id}")).StatusCode);
            Assert.Equal(own.Id, Assert.Single((await client.GetFromJsonAsync<InventoryPage>("/api/inventory"))!.Items).ProductId);
            Assert.Equal(own.Id, Assert.Single((await client.GetFromJsonAsync<MovementPage>("/api/inventory/movements"))!.Items).ProductId);
        }
        Assert.Equal(10, (await Product(a, pa.Id)).CurrentStock);
        Assert.Equal(20, (await Product(b, pb.Id)).CurrentStock);
    }

    [Fact]
    public async Task ProductEdit_OnlyChangesMinimum_AndLowStockIsComputed()
    {
        var (client, _, p) = await Setup();
        await Move(client, p.Id, 7);
        var edit = await client.PutAsJsonAsync($"/api/products/{p.Id}", new
        { name = "Mouse", sku = "MOU-001", costPrice = 1, salePrice = 2, minimumStock = 7, currentStock = 999 });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(7, (await Product(client, p.Id)).CurrentStock);
        Assert.Equal(7, (await Product(client, p.Id)).MinimumStock);
        Assert.True(Assert.Single((await client.GetFromJsonAsync<InventoryPage>("/api/inventory?lowStock=true&search=mou&pageSize=1"))!.Items).IsLowStock);
        var invalid = await client.PutAsJsonAsync($"/api/products/{p.Id}", new SaveProductRequest("Mouse", "MOU-001", null, 1, 2, -1));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await client.PatchAsJsonAsync($"/api/products/{p.Id}/status", new { isActive = false });
        Assert.Empty((await client.GetFromJsonAsync<InventoryPage>("/api/inventory?lowStock=true"))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/inventory?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/inventory/movements?type=Adjustment")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistedHistory_RejectsModificationOrDeletion(bool delete)
    {
        var (client, user, p) = await Setup();
        await Move(client, p.Id, 1);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        db.UseCurrentCompany(user.CompanyId);
        var movement = await db.InventoryMovements.SingleAsync();
        if (delete) db.InventoryMovements.Remove(movement);
        else db.Entry(movement).Property(m => m.Quantity).CurrentValue = 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal(1, Assert.Single((await client.GetFromJsonAsync<MovementPage>("/api/inventory/movements"))!.Items).Quantity);
    }

    [Fact]
    public async Task AnonymousAndInvalidTenant_AreDenied()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/inventory")).StatusCode);
        var (client, user, p) = await Setup();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        var membership = await db.Memberships.SingleAsync(m => m.CompanyId == user.CompanyId);
        db.Entry(membership).Property(m => m.IsActive).CurrentValue = false;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await Move(client, p.Id, 1)).StatusCode);
        Assert.Empty(await db.InventoryMovements.ToListAsync());
    }
}
