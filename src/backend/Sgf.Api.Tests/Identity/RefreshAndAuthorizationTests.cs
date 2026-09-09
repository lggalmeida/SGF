using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sgf.Application.Identity;
using Sgf.Domain.Companies;
using Sgf.Infrastructure.Database;
using Sgf.Infrastructure.Identity.Authentication;

namespace Sgf.Api.Tests.Identity;

public sealed class RefreshAndAuthorizationTests
    : IClassFixture<RegisterUserAndCompanyApiFactory>, IAsyncLifetime
{
    private readonly RegisterUserAndCompanyApiFactory _factory;
    public RefreshAndAuthorizationTests(RegisterUserAndCompanyApiFactory factory) => _factory = factory;
    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(HttpClient Client, string Cookie, LoginResponse Login)> StartAsync()
    {
        var client = _factory.CreateClient(new() { HandleCookies = false });
        var email = $"{Guid.NewGuid():N}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Refresh Test", email, password = "SenhaSegura123!", companyName = "Refresh Company"
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "SenhaSegura123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);
        var cookieHeader = response.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("httponly", cookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", cookieHeader, StringComparison.OrdinalIgnoreCase);
        return (client, cookieHeader.Split(';')[0], (await response.Content.ReadFromJsonAsync<LoginResponse>())!);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string cookie, string path = "refresh")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/auth/{path}");
        if (!string.IsNullOrEmpty(cookie)) request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request);
    }

    private async Task ChangeAsync(Func<SgfDbContext, Task> change)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgfDbContext>();
        await change(db);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Refresh_RotatesAndStoresOnlyHash_RejectsReplay()
    {
        var session = await StartAsync();
        await ChangeAsync(async db =>
        {
            var token = await db.RefreshTokens.SingleAsync();
            var raw = Uri.UnescapeDataString(session.Cookie[(session.Cookie.IndexOf('=') + 1)..]);
            Assert.NotEqual(raw, token.TokenHash);
            Assert.Equal(new RefreshTokenGenerator().Hash(raw), token.TokenHash);
        });
        var response = await SendAsync(session.Client, session.Cookie);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<RefreshTokenResponse>())!;
        Assert.Equal(session.Login.UserId, body.UserId);
        Assert.Equal(session.Login.CompanyId, body.CompanyId);
        Assert.NotEqual(session.Login.AccessToken, body.AccessToken);
        Assert.Equal("Owner", body.Role);
        Assert.DoesNotContain("password", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(session.Cookie, response.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(session.Client, session.Cookie)).StatusCode);
        await ChangeAsync(async db =>
        {
            Assert.Equal(2, await db.RefreshTokens.CountAsync());
            Assert.Equal(1, await db.RefreshTokens.CountAsync(t => t.RevokedAt == null));
        });
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("membership-inactive")]
    [InlineData("membership-deleted")]
    [InlineData("company-inactive")]
    [InlineData("user-deleted")]
    public async Task Refresh_RejectsInvalidSession(string reason)
    {
        var session = await StartAsync();
        await ChangeAsync(async db =>
        {
            switch (reason)
            {
                case "expired":
                    db.Entry(await db.RefreshTokens.SingleAsync()).Property(t => t.ExpiresAt).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-1);
                    break;
                case "revoked": (await db.RefreshTokens.SingleAsync()).Revoke(DateTimeOffset.UtcNow); break;
                case "membership-inactive":
                    db.Entry(await db.Memberships.SingleAsync()).Property(m => m.IsActive).CurrentValue = false;
                    break;
                case "membership-deleted": db.Memberships.Remove(await db.Memberships.SingleAsync()); break;
                case "company-inactive":
                    db.Entry(await db.Companies.SingleAsync()).Property(c => c.IsActive).CurrentValue = false;
                    break;
                case "user-deleted": db.Users.Remove(await db.Users.SingleAsync()); break;
            }
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(session.Client, session.Cookie)).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sgf_refresh_token=unknown")]
    public async Task Refresh_MissingOrUnknownCookie_IsUnauthorized(string cookie)
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, cookie)).StatusCode);
    }

    [Fact]
    public async Task Refresh_UsesCurrentRoleAndPreservesCompany()
    {
        var session = await StartAsync();
        await ChangeAsync(async db =>
        {
            db.Entry(await db.Memberships.SingleAsync()).Property(m => m.Role).CurrentValue = MembershipRole.Member;
            var other = new Company("Other");
            db.Companies.Add(other);
            db.Memberships.Add(new Membership(session.Login.UserId, other.Id, MembershipRole.Owner));
        });
        var response = await SendAsync(session.Client, session.Cookie);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<RefreshTokenResponse>())!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        Assert.Equal(session.Login.CompanyId.ToString(), jwt.Claims.Single(c => c.Type == "company_id").Value);
        Assert.Equal("Member", body.Role);
        Assert.Contains(jwt.Claims, c => c.Value == "Member");
    }

    [Fact]
    public async Task Logout_RevokesAndDeletesCookie_IsIdempotent()
    {
        var session = await StartAsync();
        var response = await SendAsync(session.Client, session.Cookie, "logout");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("expires=Thu, 01 Jan 1970", response.Headers.GetValues("Set-Cookie").Single());
        await ChangeAsync(async db => Assert.NotNull((await db.RefreshTokens.SingleAsync()).RevokedAt));
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(session.Client, session.Cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(session.Client, session.Cookie, "logout")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentRefresh_OnlyOneSucceeds()
    {
        var session = await StartAsync();
        var responses = await Task.WhenAll(SendAsync(session.Client, session.Cookie), SendAsync(session.Client, session.Cookie));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);
        await ChangeAsync(async db => Assert.Equal(1, await db.RefreshTokens.CountAsync(t => t.RevokedAt == null)));
    }

    [Theory]
    [InlineData(MembershipRole.Owner, "OwnerOnly", true)]
    [InlineData(MembershipRole.Admin, "OwnerOnly", false)]
    [InlineData(MembershipRole.Member, "OwnerOnly", false)]
    [InlineData(MembershipRole.Owner, "AdminOrOwner", true)]
    [InlineData(MembershipRole.Admin, "AdminOrOwner", true)]
    [InlineData(MembershipRole.Member, "AdminOrOwner", false)]
    public async Task Policies_UseValidatedMembership(MembershipRole role, string policy, bool expected)
    {
        var session = await StartAsync();
        await ChangeAsync(async db => db.Entry(await db.Memberships.SingleAsync()).Property(m => m.Role).CurrentValue = role);
        Assert.Equal(expected, await AuthorizeAsync(session.Login, role, policy));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Policies_RejectStaleRoleOrInvalidTenant(bool deactivate)
    {
        var session = await StartAsync();
        Assert.True(await AuthorizeAsync(session.Login, MembershipRole.Owner, "OwnerOnly"));
        await ChangeAsync(async db =>
        {
            var membership = await db.Memberships.SingleAsync();
            if (deactivate) db.Entry(membership).Property(m => m.IsActive).CurrentValue = false;
            else db.Entry(membership).Property(m => m.Role).CurrentValue = MembershipRole.Member;
        });
        Assert.False(await AuthorizeAsync(session.Login, MembershipRole.Owner, "OwnerOnly"));
    }

    private async Task<bool> AuthorizeAsync(LoginResponse login, MembershipRole tokenRole, string policy)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", login.UserId), new Claim("company_id", login.CompanyId.ToString()),
            new Claim(ClaimTypes.Role, tokenRole.ToString())
        }, "Bearer"));
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = principal, RequestServices = scope.ServiceProvider };
        try
        {
            return (await scope.ServiceProvider.GetRequiredService<IAuthorizationService>()
                .AuthorizeAsync(principal, null, policy)).Succeeded;
        }
        finally { accessor.HttpContext = null; }
    }

    [Theory]
    [InlineData("login")]
    [InlineData("refresh")]
    [InlineData("logout")]
    public async Task UntrustedOrigin_CannotChangeCookies(string path)
    {
        var session = await StartAsync();
        session.Client.DefaultRequestHeaders.Add("Origin", "https://untrusted.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(session.Client, session.Cookie, path)).StatusCode);
    }
}
