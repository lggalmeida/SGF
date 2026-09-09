using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Sgf.Domain.Companies;
using Sgf.Infrastructure.Database;
using Sgf.Infrastructure.Identity;
using Sgf.Infrastructure.Identity.Authentication;

namespace Sgf.Api.Tests.Identity;

public sealed class LoginEndpointTests : IClassFixture<RegisterUserAndCompanyApiFactory>, IAsyncLifetime
{
    private const string ValidPassword = "SenhaSegura123!";
    private const string JwtSigningKey = "register-tests-signing-key-with-at-least-32-characters";

    private readonly RegisterUserAndCompanyApiFactory _factory;

    public LoginEndpointTests(RegisterUserAndCompanyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsJwtWithExpectedClaims()
    {
        var seeded = await SeedUserWithCompanyAsync("valid-login@example.com");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginHttpRequest(
            seeded.Email,
            ValidPassword));
        var body = await response.Content.ReadFromJsonAsync<LoginHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal("Bearer", body.TokenType);
        Assert.Equal(seeded.UserId, body.UserId);
        Assert.Equal(seeded.CompanyId, body.CompanyId);
        Assert.Equal("Owner", body.Role);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        Assert.Equal(seeded.UserId, token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(seeded.CompanyId.ToString(), token.Claims.Single(claim => claim.Type == SgfClaimTypes.CompanyId).Value);
        Assert.Equal("Owner", token.Claims.Single(claim => claim.Type == ClaimTypes.Role).Value);
        Assert.DoesNotContain(token.Claims, claim => claim.Value.Contains(ValidPassword, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(token.Claims, claim => claim.Type.Contains("hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var seeded = await SeedUserWithCompanyAsync("wrong-password@example.com");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginHttpRequest(
            seeded.Email,
            "SenhaErrada123!"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Invalid credentials.", body);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsSameUnauthorizedMessageAsWrongPassword()
    {
        var seeded = await SeedUserWithCompanyAsync("equivalent-error@example.com");
        var client = _factory.CreateClient();

        var wrongPasswordResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginHttpRequest(
            seeded.Email,
            "SenhaErrada123!"));
        var unknownEmailResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginHttpRequest(
            "unknown@example.com",
            ValidPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmailResponse.StatusCode);
        Assert.Equal(
            await wrongPasswordResponse.Content.ReadAsStringAsync(),
            await unknownEmailResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_WithInactiveMembership_ReturnsForbidden()
    {
        var seeded = await SeedUserWithCompanyAsync("inactive-membership@example.com", membershipIsActive: false);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginHttpRequest(
            seeded.Email,
            ValidPassword));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("no_active_company", body);
    }

    [Fact]
    public async Task Login_WithInactiveCompany_ReturnsForbidden()
    {
        var seeded = await SeedUserWithCompanyAsync("inactive-company@example.com", companyIsActive: false);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginHttpRequest(
            seeded.Email,
            ValidPassword));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("no_active_company", body);
    }

    [Fact]
    public async Task Login_WithMultipleActiveMemberships_ReturnsCompanySelectionRequired()
    {
        var seeded = await SeedUserWithCompanyAsync("multiple-companies@example.com");
        await AddSecondActiveMembershipAsync(seeded.UserId);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginHttpRequest(
            seeded.Email,
            ValidPassword));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("company_selection_required", body);
    }

    [Fact]
    public async Task GetMe_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithValidToken_ReturnsCurrentUserAndCompany()
    {
        var seeded = await SeedUserWithCompanyAsync("me-valid@example.com");
        var client = _factory.CreateClient();
        var login = await LoginAsync(client, seeded.Email);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var response = await client.GetAsync("/api/auth/me");
        var body = await response.Content.ReadFromJsonAsync<CurrentUserHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(seeded.UserId, body.UserId);
        Assert.Equal(seeded.Email, body.Email);
        Assert.Equal("Usuario Teste", body.Name);
        Assert.Equal(seeded.CompanyId, body.Company.Id);
        Assert.Equal("Empresa Teste", body.Company.Name);
        Assert.Equal("Owner", body.Role);
    }

    [Fact]
    public async Task GetMe_WhenMembershipWasDeletedAfterTokenWasIssued_ReturnsForbidden()
    {
        var seeded = await SeedUserWithCompanyAsync("deleted-membership-context@example.com");
        var client = _factory.CreateClient();
        var login = await LoginAsync(client, seeded.Email);
        await DeleteMembershipAsync(seeded.UserId, seeded.CompanyId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WhenMembershipWasDisabledAfterTokenWasIssued_ReturnsForbidden()
    {
        var seeded = await SeedUserWithCompanyAsync("disabled-membership-context@example.com");
        var client = _factory.CreateClient();
        var login = await LoginAsync(client, seeded.Email);
        await SetMembershipActiveAsync(seeded.UserId, seeded.CompanyId, isActive: false);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WhenCompanyWasDisabledAfterTokenWasIssued_ReturnsForbidden()
    {
        var seeded = await SeedUserWithCompanyAsync("disabled-company-context@example.com");
        var client = _factory.CreateClient();
        var login = await LoginAsync(client, seeded.Email);
        await SetCompanyActiveAsync(seeded.CompanyId, isActive: false);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WhenTokenRoleDiffersFromMembershipRole_ReturnsForbidden()
    {
        var seeded = await SeedUserWithCompanyAsync("role-mismatch-context@example.com");
        var client = _factory.CreateClient();
        var login = await LoginAsync(client, seeded.Email);
        await SetMembershipRoleAsync(seeded.UserId, seeded.CompanyId, MembershipRole.Admin);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithTokenWithoutCompanyId_ReturnsForbidden()
    {
        var seeded = await SeedUserWithCompanyAsync("missing-company-claim@example.com");
        var client = _factory.CreateClient();
        var token = CreateToken(
        [
            new Claim(JwtRegisteredClaimNames.Sub, seeded.UserId),
            new Claim(ClaimTypes.Role, "Owner")
        ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithTokenWithoutUserId_ReturnsForbidden()
    {
        var seeded = await SeedUserWithCompanyAsync("missing-user-claim@example.com");
        var client = _factory.CreateClient();
        var token = CreateToken(
        [
            new Claim(SgfClaimTypes.CompanyId, seeded.CompanyId.ToString()),
            new Claim(ClaimTypes.Role, "Owner")
        ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithClientProvidedCompanyId_UsesAuthenticatedCompanyFromToken()
    {
        var seeded = await SeedUserWithCompanyAsync("client-company-ignored@example.com");
        var client = _factory.CreateClient();
        var login = await LoginAsync(client, seeded.Email);
        var fakeCompanyId = Guid.NewGuid();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        client.DefaultRequestHeaders.Add("X-Company-Id", fakeCompanyId.ToString());

        var response = await client.GetAsync($"/api/auth/me?companyId={fakeCompanyId}");
        var body = await response.Content.ReadFromJsonAsync<CurrentUserHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(seeded.CompanyId, body.Company.Id);
        Assert.NotEqual(fakeCompanyId, body.Company.Id);
    }

    [Fact]
    public async Task GetMe_WithTamperedToken_ReturnsUnauthorized()
    {
        var seeded = await SeedUserWithCompanyAsync("tampered-token@example.com");
        var client = _factory.CreateClient();
        var login = await LoginAsync(client, seeded.Email);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            login.AccessToken + "tampered");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithExpiredToken_ReturnsUnauthorized()
    {
        var seeded = await SeedUserWithCompanyAsync("expired-token@example.com");
        var client = _factory.CreateClient();
        var expiredToken = CreateExpiredToken(seeded.UserId, seeded.CompanyId, "Owner");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_DoesNotReturnPasswordOrPasswordHash()
    {
        var seeded = await SeedUserWithCompanyAsync("login-no-secrets@example.com");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginHttpRequest(
            seeded.Email,
            ValidPassword));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(ValidPassword, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordHash", body, StringComparison.OrdinalIgnoreCase);
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<SeededUser> SeedUserWithCompanyAsync(
        string email,
        bool membershipIsActive = true,
        bool companyIsActive = true)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<SgfDbContext>();

        var user = new ApplicationUser
        {
            Name = "Usuario Teste",
            Email = email,
            UserName = email
        };

        var result = await userManager.CreateAsync(user, ValidPassword);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));

        var company = new Company("Empresa Teste");
        dbContext.Companies.Add(company);

        if (!companyIsActive)
        {
            dbContext.Entry(company).Property(item => item.IsActive).CurrentValue = false;
        }

        var membership = new Membership(user.Id, company.Id, MembershipRole.Owner);
        dbContext.Memberships.Add(membership);

        if (!membershipIsActive)
        {
            dbContext.Entry(membership).Property(item => item.IsActive).CurrentValue = false;
        }

        await dbContext.SaveChangesAsync();

        return new SeededUser(user.Id, email, company.Id);
    }

    private async Task AddSecondActiveMembershipAsync(string userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SgfDbContext>();

        var company = new Company("Segunda Empresa Teste");
        var membership = new Membership(userId, company.Id, MembershipRole.Member);

        dbContext.Companies.Add(company);
        dbContext.Memberships.Add(membership);

        await dbContext.SaveChangesAsync();
    }

    private async Task DeleteMembershipAsync(string userId, Guid companyId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        var membership = await dbContext.Memberships.SingleAsync(item =>
            item.UserId == userId && item.CompanyId == companyId);

        dbContext.Memberships.Remove(membership);
        await dbContext.SaveChangesAsync();
    }

    private async Task SetMembershipActiveAsync(string userId, Guid companyId, bool isActive)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        var membership = await dbContext.Memberships.SingleAsync(item =>
            item.UserId == userId && item.CompanyId == companyId);

        dbContext.Entry(membership).Property(item => item.IsActive).CurrentValue = isActive;
        await dbContext.SaveChangesAsync();
    }

    private async Task SetMembershipRoleAsync(string userId, Guid companyId, MembershipRole role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        var membership = await dbContext.Memberships.SingleAsync(item =>
            item.UserId == userId && item.CompanyId == companyId);

        dbContext.Entry(membership).Property(item => item.Role).CurrentValue = role;
        await dbContext.SaveChangesAsync();
    }

    private async Task SetCompanyActiveAsync(Guid companyId, bool isActive)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        var company = await dbContext.Companies.SingleAsync(item => item.Id == companyId);

        dbContext.Entry(company).Property(item => item.IsActive).CurrentValue = isActive;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<LoginHttpResponse> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginHttpRequest(email, ValidPassword));
        var body = await response.Content.ReadFromJsonAsync<LoginHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        return body;
    }

    private static string CreateExpiredToken(string userId, Guid companyId, string role)
    {
        return CreateToken(
        [
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(SgfClaimTypes.CompanyId, companyId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ], expires: DateTime.UtcNow.AddMinutes(-1));
    }

    private static string CreateToken(IEnumerable<Claim> claims, DateTime? expires = null)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "SGF.Api",
            audience: "SGF.Frontend",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires: expires ?? DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record SeededUser(string UserId, string Email, Guid CompanyId);

    private sealed record LoginHttpRequest(string Email, string Password);

    private sealed record LoginHttpResponse(
        string AccessToken,
        string TokenType,
        DateTimeOffset ExpiresAt,
        string UserId,
        Guid CompanyId,
        string Role);

    private sealed record CurrentUserHttpResponse(
        string UserId,
        string Name,
        string Email,
        CurrentCompanyHttpResponse Company,
        string Role);

    private sealed record CurrentCompanyHttpResponse(Guid Id, string Name);
}
