using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sgf.Domain.Companies;
using Sgf.Infrastructure.Database;
using Sgf.Infrastructure.Identity;

namespace Sgf.Api.Tests.Identity;

public sealed class RegisterUserAndCompanyEndpointTests
    : IClassFixture<RegisterUserAndCompanyApiFactory>, IAsyncLifetime
{
    private readonly RegisterUserAndCompanyApiFactory _factory;

    public RegisterUserAndCompanyEndpointTests(RegisterUserAndCompanyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_WithValidData_CreatesUserCompanyAndOwnerMembership()
    {
        var request = ValidRequest("valid@example.com");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(request.Email);
        Assert.NotNull(user);
        Assert.NotNull(user.PasswordHash);

        var company = await dbContext.Companies.SingleAsync();
        Assert.Equal(request.CompanyName, company.Name);
        Assert.True(company.IsActive);
        Assert.NotEqual(default, company.CreatedAt);
        Assert.Equal(company.CreatedAt, company.UpdatedAt);

        var membership = await dbContext.Memberships.SingleAsync();
        Assert.Equal(user.Id, membership.UserId);
        Assert.Equal(company.Id, membership.CompanyId);
        Assert.Equal(MembershipRole.Owner, membership.Role);
        Assert.True(membership.IsActive);
    }

    [Fact]
    public async Task Register_WithDuplicatedEmail_ReturnsConflict()
    {
        var request = ValidRequest("duplicated@example.com");
        var client = _factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync("/api/auth/register", request);
        var secondResponse = await client.PostAsJsonAsync("/api/auth/register", request with
        {
            CompanyName = "Another Company"
        });

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        Assert.Equal(1, await dbContext.Companies.CountAsync());
        Assert.Equal(1, await dbContext.Memberships.CountAsync());
    }

    [Fact]
    public async Task Register_WithEmptyCompanyName_ReturnsBadRequest()
    {
        var request = ValidRequest("empty-company@example.com") with
        {
            CompanyName = " "
        };
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoRegistrationWasCreated("empty-company@example.com");
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        var request = ValidRequest("invalid-email") with
        {
            Email = "invalid-email"
        };
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoRegistrationWasCreated("invalid-email");
    }

    [Fact]
    public async Task Register_WithInvalidPassword_ReturnsBadRequest()
    {
        var request = ValidRequest("weak-password@example.com") with
        {
            Password = "123"
        };
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoRegistrationWasCreated("weak-password@example.com");
    }

    [Fact]
    public async Task Register_DoesNotReturnPasswordOrPasswordHash()
    {
        var request = ValidRequest("no-secret-response@example.com");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain(request.Password, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_WithCompanyNameOverLimit_ReturnsValidationBeforePersistence()
    {
        var email = "rollback@example.com";
        var request = ValidRequest(email) with
        {
            CompanyName = new string('A', 201)
        };
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Company name must be at most 200 characters.", await response.Content.ReadAsStringAsync());
        await AssertNoRegistrationWasCreated(email);
    }

    [Fact]
    public async Task Register_WithCompanyNameAtLimit_Succeeds()
    {
        var request = ValidRequest("company-limit@example.com") with { CompanyName = new string('A', 200) };
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("Companies")]
    [InlineData("Memberships")]
    public async Task Register_WhenDatabaseRejectsInsert_RollsBackEntireRegistration(string table)
    {
        // This constraint exists only in this fixture's disposable database.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
            var sql = table == "Companies"
                ? "ALTER TABLE \"Companies\" ADD CONSTRAINT \"TestRejectInsert\" CHECK (false)"
                : "ALTER TABLE \"Memberships\" ADD CONSTRAINT \"TestRejectInsert\" CHECK (false)";
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", ValidRequest("real-rollback@example.com"));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Registration could not be completed.", body);
        Assert.DoesNotContain("TestRejectInsert", body);
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
        await AssertNoRegistrationWasCreated("real-rollback@example.com");
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task AssertNoRegistrationWasCreated(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Null(await userManager.FindByEmailAsync(email));
        Assert.Equal(0, await dbContext.Companies.CountAsync());
        Assert.Equal(0, await dbContext.Memberships.CountAsync());
    }

    private static RegisterUserAndCompanyHttpRequest ValidRequest(string email)
    {
        return new RegisterUserAndCompanyHttpRequest(
            "Joao Silva",
            email,
            "SenhaSegura123!",
            "Empresa do Joao");
    }

    private sealed record RegisterUserAndCompanyHttpRequest(
        string Name,
        string Email,
        string Password,
        string CompanyName);
}

public sealed class RegisterUserAndCompanyApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string JwtSigningKey = "register-tests-signing-key-with-at-least-32-characters";

    private readonly string _connectionString =
        $"Host=127.0.0.1;Port=15432;Database=sgf_api_tests_{Guid.NewGuid():N};Username=sgf_user;Password=sgf_dev_password";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", JwtSigningKey);
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = JwtSigningKey
            });
        });

        builder.ConfigureServices(services =>
        {
            var dbContextOptions = services
                .Where(service => service.ServiceType == typeof(DbContextOptions<SgfDbContext>))
                .ToList();

            foreach (var service in dbContextOptions)
            {
                services.Remove(service);
            }

            services.AddDbContext<SgfDbContext>(options =>
                options.UseNpgsql(_connectionString));
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SgfDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    public async Task InitializeAsync()
    {
        await ResetDatabaseAsync();
    }

    public new async Task DisposeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        await dbContext.Database.EnsureDeletedAsync();

        await base.DisposeAsync();
    }
}






