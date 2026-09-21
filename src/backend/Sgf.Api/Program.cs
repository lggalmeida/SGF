using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sgf.Api.Authorization;
using Sgf.Api.Identity;
using Sgf.Api.Products;
using Sgf.Api.Inventory;
using Sgf.Api.Finance;
using Sgf.Api.Analytics;
using Sgf.Application.Identity;
using Sgf.Domain.Companies;
using Sgf.Infrastructure;
using Sgf.Infrastructure.Database;
using Sgf.Infrastructure.Identity.Authentication;

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

const string DevelopmentCorsPolicy = "DevelopmentCors";

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenantContext, CurrentTenantContext>();
builder.Services.AddScoped<IAuthorizationHandler, CurrentTenantRoleAuthorizationHandler>();

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();

var hasValidSigningKey = !string.IsNullOrWhiteSpace(jwtOptions.SigningKey)
    && Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) >= 32;

if (!hasValidSigningKey)
{
    throw new InvalidOperationException("JWT signing key must be configured with at least 32 bytes.");
}

var signingKeyBytes = Encoding.UTF8.GetBytes(jwtOptions.SigningKey);

var refreshOptions = builder.Configuration.GetSection(RefreshTokenOptions.SectionName)
    .Get<RefreshTokenOptions>() ?? new RefreshTokenOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Issuer)
    || string.IsNullOrWhiteSpace(jwtOptions.Audience)
    || jwtOptions.AccessTokenMinutes is < 1 or > 60
    || refreshOptions.Days is < 1 or > 30
    || string.IsNullOrWhiteSpace(refreshOptions.CookieName)
    || refreshOptions.CookieName.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
{
    throw new InvalidOperationException("Invalid JWT or refresh token configuration.");
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
    || (uri.Scheme != "http" && uri.Scheme != "https") || origin.Contains('*')))
{
    throw new InvalidOperationException("CORS requires explicit HTTP/HTTPS origins.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(CurrentUserAuthorizationPolicies.OwnerOnly, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new CurrentTenantRoleRequirement(MembershipRole.Owner));
    });

    options.AddPolicy(CurrentUserAuthorizationPolicies.AdminOrOwner, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new CurrentTenantRoleRequirement(MembershipRole.Owner, MembershipRole.Admin));
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(DevelopmentCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors(DevelopmentCorsPolicy);
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/auth"))
    {
        context.Response.Headers.CacheControl = "no-store";
        // Reject browser requests from origins outside the configured frontend.
        if (HttpMethods.IsPost(context.Request.Method)
            && context.Request.Headers.TryGetValue("Origin", out var origin)
            && !allowedOrigins.Contains(origin.ToString(), StringComparer.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }
    await next(context);
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "SGF API",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/api/health/database", async (SgfDbContext dbContext, IWebHostEnvironment environment) =>
{
    try
    {
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.CloseConnectionAsync();

        return Results.Ok(new { status = "Healthy", database = "PostgreSQL" });
    }
    catch (Exception exception)
    {
        var detail = environment.IsDevelopment()
            ? exception.Message
            : "Database connection failed.";

        return Results.Problem(detail, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/auth/register", async (
    RegisterUserAndCompanyRequest request,
    IRegisterCompanyOwnerUseCase registerCompanyOwnerUseCase,
    CancellationToken cancellationToken) =>
{
    var result = await registerCompanyOwnerUseCase.ExecuteAsync(
        new RegisterCompanyOwnerRequest(
            request.Name,
            request.Email,
            request.Password,
            request.CompanyName),
        cancellationToken);

    if (result.Succeeded && result.Value is not null)
    {
        return Results.Created("/api/auth/register", result.Value);
    }

    var error = result.Error ?? new RegistrationError(
        RegistrationErrorCode.PersistenceFailed,
        "Registration could not be completed.",
        []);

    var problemDetails = new ProblemDetails
    {
        Title = error.Message,
        Status = error.Code switch
        {
            RegistrationErrorCode.EmailAlreadyRegistered => StatusCodes.Status409Conflict,
            RegistrationErrorCode.ValidationFailed => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        }
    };

    problemDetails.Extensions["errors"] = error.Details;

    return Results.Json(problemDetails, statusCode: problemDetails.Status);
});

app.MapPost("/api/auth/login", async (
    LoginUserRequest request,
    HttpResponse response,
    ILoginUseCase loginUseCase,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    IWebHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    var result = await loginUseCase.ExecuteAsync(
        new LoginRequest(request.Email, request.Password),
        cancellationToken);

    if (result.Succeeded && result.Value is not null)
    {
        if (result.RefreshToken is not null && result.RefreshTokenExpiresAt.HasValue)
        {
            response.Cookies.Append(
                refreshTokenOptions.Value.CookieName,
                result.RefreshToken,
                CreateRefreshTokenCookieOptions(result.RefreshTokenExpiresAt.Value, environment));
        }

        return Results.Ok(result.Value);
    }

    var error = result.Error ?? new LoginError(
        LoginErrorCode.InvalidCredentials,
        "Invalid credentials.");

    var statusCode = error.Code switch
    {
        LoginErrorCode.InvalidCredentials => StatusCodes.Status401Unauthorized,
        LoginErrorCode.NoActiveCompany => StatusCodes.Status403Forbidden,
        LoginErrorCode.CompanySelectionRequired => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };

    var problemDetails = new ProblemDetails
    {
        Title = error.Message,
        Status = statusCode
    };

    if (error.PublicCode is not null)
    {
        problemDetails.Extensions["code"] = error.PublicCode;
    }

    return Results.Json(problemDetails, statusCode: statusCode);
});

app.MapPost("/api/auth/refresh", async (
    HttpRequest request,
    HttpResponse response,
    IRefreshTokenUseCase refreshTokenUseCase,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    IWebHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    var cookieName = refreshTokenOptions.Value.CookieName;
    var refreshToken = request.Cookies[cookieName];

    var result = await refreshTokenUseCase.ExecuteAsync(
        new RefreshTokenRequest(refreshToken ?? string.Empty),
        cancellationToken);

    if (!result.Succeeded || result.Value is null)
    {
        response.Cookies.Delete(cookieName, CreateExpiredRefreshTokenCookieOptions(environment));
        return Results.Unauthorized();
    }

    if (result.RefreshToken is not null && result.RefreshTokenExpiresAt.HasValue)
    {
        response.Cookies.Append(
            cookieName,
            result.RefreshToken,
            CreateRefreshTokenCookieOptions(result.RefreshTokenExpiresAt.Value, environment));
    }

    return Results.Ok(result.Value);
});

app.MapPost("/api/auth/logout", async (
    HttpRequest request,
    HttpResponse response,
    ILogoutUseCase logoutUseCase,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    IWebHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    var cookieName = refreshTokenOptions.Value.CookieName;
    await logoutUseCase.ExecuteAsync(
        new LogoutRequest(request.Cookies[cookieName]),
        cancellationToken);

    response.Cookies.Delete(cookieName, CreateExpiredRefreshTokenCookieOptions(environment));

    return Results.NoContent();
});

app.MapGet("/api/auth/me", async (
    IGetCurrentUserUseCase getCurrentUserUseCase,
    CancellationToken cancellationToken) =>
{
    var currentUser = await getCurrentUserUseCase.ExecuteAsync(cancellationToken);

    return currentUser is null
        ? Results.Forbid()
        : Results.Ok(currentUser);
}).RequireAuthorization();

app.MapProductEndpoints();
app.MapInventoryEndpoints();
app.MapFinanceEndpoints();
app.MapDashboardEndpoints();

app.Run();

static CookieOptions CreateRefreshTokenCookieOptions(
    DateTimeOffset expiresAt,
    IWebHostEnvironment environment)
{
    return new CookieOptions
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment() && !environment.IsEnvironment("Testing"),
        SameSite = SameSiteMode.Lax,
        Expires = expiresAt,
        Path = "/api/auth"
    };
}

static CookieOptions CreateExpiredRefreshTokenCookieOptions(IWebHostEnvironment environment)
{
    return new CookieOptions
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment() && !environment.IsEnvironment("Testing"),
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UnixEpoch,
        Path = "/api/auth"
    };
}

public sealed record LoginUserRequest(
    string Email,
    string Password);

public sealed record RegisterUserAndCompanyRequest(
    string Name,
    string Email,
    string Password,
    string CompanyName);

public partial class Program;
