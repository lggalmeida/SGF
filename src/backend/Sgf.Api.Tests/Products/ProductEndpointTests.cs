using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sgf.Api.Tests.Identity;
using Sgf.Application.Identity;
using Sgf.Application.Products;
using Sgf.Domain.Products;
using Sgf.Infrastructure.Database;

namespace Sgf.Api.Tests.Products;

public sealed class ProductEndpointTests(RegisterUserAndCompanyApiFactory factory)
    : IClassFixture<RegisterUserAndCompanyApiFactory>, IAsyncLifetime
{
    private static SaveProductRequest Valid(string sku = "MOU-001", string name = "Mouse sem fio") =>
        new(name, sku, "Mouse ergonomico", 45m, 39.90m);

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(HttpClient Client, Guid Company)> Account()
    {
        var client = factory.CreateClient();
        var email = $"product-{Guid.NewGuid():N}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register",
            new { name = "Teste Produtos", email, password = "SenhaSegura123!", companyName = "Empresa Produtos" });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "SenhaSegura123!" });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return (client, token.CompanyId);
    }

    private static async Task<ProductResponse> Create(HttpClient client, SaveProductRequest? data = null)
    {
        var response = await client.PostAsJsonAsync("/api/products", data ?? Valid());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
        Assert.EndsWith($"/api/products/{product.Id}", response.Headers.Location!.ToString());
        return product;
    }

    [Fact]
    public async Task Create_NormalizesAndPersistsWithoutTrustingClientOwnership()
    {
        var (client, company) = await Account();
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = " Mouse ", sku = " mou - 001 ", description = " descricao ", costPrice = 50, salePrice = 20,
            companyId = Guid.NewGuid(), createdAt = DateTimeOffset.UnixEpoch, updatedAt = DateTimeOffset.UnixEpoch, isActive = false
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var p = (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
        Assert.Equal("Mouse", p.Name);
        Assert.Equal("MOU-001", p.SKU);
        Assert.True(p.IsActive);
        Assert.Equal(p.CreatedAt, p.UpdatedAt);
        Assert.True(p.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        db.UseCurrentCompany(company);
        var stored = await db.Products.SingleAsync();
        Assert.Equal(company, stored.CompanyId);
        Assert.Equal(20m, stored.SalePrice);
        Assert.Equal("descricao", stored.Description);
        Assert.True(db.Model.FindEntityType(typeof(Product))!.FindProperty(nameof(Product.CompanyId))!.IsConcurrencyToken);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("sku")]
    [InlineData("cost")]
    [InlineData("sale")]
    [InlineData("precision")]
    [InlineData("range")]
    [InlineData("length")]
    [InlineData("missing")]
    public async Task InvalidInput_IsRejectedBeforePersistence(string field)
    {
        var (client, _) = await Account();
        var data = field switch
        {
            "name" => Valid() with { Name = " " },
            "sku" => Valid() with { SKU = " \t " },
            "cost" => Valid() with { CostPrice = -1 },
            "sale" => Valid() with { SalePrice = -1 },
            "precision" => Valid() with { SalePrice = 1.001m },
            "range" => Valid() with { SalePrice = 10000000000m },
            "length" => Valid() with { Name = new string('x', 201), SKU = new string('x', 65), Description = new string('x', 2001) },
            _ => Valid() with { CostPrice = null }
        };
        var response = await client.PostAsJsonAsync("/api/products", data);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var page = await client.GetFromJsonAsync<ProductPage>("/api/products");
        Assert.Equal(0, page!.TotalCount);
        Assert.DoesNotContain("exception", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sku_IsUniquePerCompany_IncludingInactiveProductsAndConcurrentCreates()
    {
        var (a, _) = await Account();
        var (b, _) = await Account();
        var product = await Create(a);
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync("/api/products", Valid(" mou -001 "))).StatusCode);
        await Create(b);
        await a.PatchAsJsonAsync($"/api/products/{product.Id}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync("/api/products", Valid())).StatusCode);
        var concurrent = await Task.WhenAll(
            a.PostAsJsonAsync("/api/products", Valid("CONCURRENT")),
            a.PostAsJsonAsync("/api/products", Valid("CONCURRENT")));
        Assert.Single(concurrent, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(concurrent, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task QueriesAndWrites_AreIsolatedByTenant()
    {
        var (a, _) = await Account();
        var (b, _) = await Account();
        var pa = await Create(a);
        var pb = await Create(b, Valid("B", "Produto B"));
        var listA = (await a.GetFromJsonAsync<ProductPage>("/api/products"))!;
        var listB = (await b.GetFromJsonAsync<ProductPage>("/api/products"))!;
        Assert.Equal(pa.Id, Assert.Single(listA.Items).Id);
        Assert.Equal(pb.Id, Assert.Single(listB.Items).Id);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/products/{pa.Id}")).StatusCode);
        foreach (var (client, foreign) in new[] { (a, pb), (b, pa) })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/products/{foreign.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/products/{foreign.Id}", Valid())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/products/{foreign.Id}/status", new { isActive = false })).StatusCode);
        }
        Assert.True((await b.GetFromJsonAsync<ProductResponse>($"/api/products/{pb.Id}"))!.IsActive);
        Assert.Equal("Produto B", (await b.GetFromJsonAsync<ProductResponse>($"/api/products/{pb.Id}"))!.Name);
    }

    [Fact]
    public async Task SearchPaginationAndStatusFilter_WorkWithinTenant()
    {
        var (a, _) = await Account();
        await Create(a, Valid("ABC", "Mouse A"));
        var second = await Create(a, Valid("DEF", "Mouse B"));
        await Create(a, Valid("GHI", "Teclado"));
        var firstPage = (await a.GetFromJsonAsync<ProductPage>("/api/products?page=1&pageSize=1&search=mouse"))!;
        var secondPage = (await a.GetFromJsonAsync<ProductPage>("/api/products?page=2&pageSize=1&search=mouse"))!;
        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal("Mouse A", Assert.Single(firstPage.Items).Name);
        Assert.Equal(second.Id, Assert.Single(secondPage.Items).Id);
        Assert.Single((await a.GetFromJsonAsync<ProductPage>("/api/products?search=a%20b%20c"))!.Items);
        await a.PatchAsJsonAsync($"/api/products/{second.Id}/status", new { isActive = false });
        Assert.Single((await a.GetFromJsonAsync<ProductPage>("/api/products?isActive=false"))!.Items);
        Assert.Equal(2, (await a.GetFromJsonAsync<ProductPage>("/api/products?isActive=true"))!.TotalCount);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.GetAsync("/api/products?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.GetAsync("/api/products?page=0")).StatusCode);
    }

    [Fact]
    public async Task EditAndStatus_PreserveOwnershipAndCreationDate()
    {
        var (a, company) = await Account();
        var product = await Create(a);
        var second = await Create(a, Valid("SECOND"));
        Assert.Equal(HttpStatusCode.Conflict, (await a.PutAsJsonAsync($"/api/products/{second.Id}", Valid())).StatusCode);
        var response = await a.PutAsJsonAsync($"/api/products/{product.Id}", new
        {
            name = "Mouse atualizado", sku = "EDIT", description = "", costPrice = 0, salePrice = 0,
            companyId = Guid.NewGuid(), createdAt = DateTimeOffset.UnixEpoch
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
        Assert.Equal(product.CreatedAt, edited.CreatedAt);
        Assert.True(edited.UpdatedAt >= edited.CreatedAt);
        Assert.Null(edited.Description);
        foreach (var status in new[] { false, false, true })
        {
            var r = await a.PatchAsJsonAsync($"/api/products/{product.Id}/status", new { isActive = status });
            Assert.Equal(status, (await r.Content.ReadFromJsonAsync<ProductResponse>())!.IsActive);
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        db.UseCurrentCompany(company);
        Assert.Equal(company, (await db.Products.SingleAsync(p => p.Id == product.Id)).CompanyId);
    }

    [Fact]
    public async Task AnonymousAndRevokedTenant_CannotUseProducts()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/products", Valid())).StatusCode);
        var (a, company) = await Account();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        var membership = await db.Memberships.SingleAsync(m => m.CompanyId == company);
        db.Entry(membership).Property(m => m.IsActive).CurrentValue = false;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await a.GetAsync("/api/products")).StatusCode);
        Assert.Empty(await db.Products.ToListAsync());
    }

    [Theory]
    [InlineData(Sgf.Domain.Companies.MembershipRole.Member)]
    [InlineData(Sgf.Domain.Companies.MembershipRole.Admin)]
    public async Task ActiveMembersAndAdmins_CanManageProducts(Sgf.Domain.Companies.MembershipRole role)
    {
        var (client, company) = await Account();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
            var membership = await db.Memberships.SingleAsync(m => m.CompanyId == company);
            db.Entry(membership).Property(m => m.Role).CurrentValue = role;
            await db.SaveChangesAsync();
        }
        var refresh = await client.PostAsync("/api/auth/refresh", null);
        refresh.EnsureSuccessStatusCode();
        var token = (await refresh.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var product = await Create(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/products/{product.Id}", Valid("EDITED"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/products/{product.Id}/status", new { isActive = false })).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DetachedProduct_CannotWriteAnotherCompany(bool delete)
    {
        var (a, _) = await Account();
        var (_, companyB) = await Account();
        var productA = await Create(a);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        db.UseCurrentCompany(companyB);
        var forged = new Product("Forjado", "FORGED", null, 0, 0);
        db.Entry(forged).Property(p => p.Id).CurrentValue = productA.Id;
        db.Entry(forged).Property(p => p.CompanyId).CurrentValue = companyB;
        if (delete) db.Products.Remove(forged); else db.Products.Update(forged);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        Assert.Equal("Mouse sem fio", (await a.GetFromJsonAsync<ProductResponse>($"/api/products/{productA.Id}"))!.Name);
    }
}
