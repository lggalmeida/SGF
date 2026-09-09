using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sgf.Api.Identity;
using Sgf.Application.Identity;
using Sgf.Infrastructure;
using Sgf.Infrastructure.Database;
using Sgf.Infrastructure.Identity.Authentication;
using Microsoft.IdentityModel.Tokens;

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

const string DevelopmentCorsPolicy = "DevelopmentCors";

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenantContext, CurrentTenantContext>();
var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();

var hasValidSigningKey = !string.IsNullOrWhiteSpace(jwtOptions.SigningKey)
    && Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) >= 32;

if (!hasValidSigningKey && !builder.Environment.IsEnvironment("Testing"))
{
    throw new InvalidOperationException("JWT signing key must be configured with at least 32 bytes.");
}

var signingKeyBytes = hasValidSigningKey
    ? Encoding.UTF8.GetBytes(jwtOptions.SigningKey)
    : RandomNumberGenerator.GetBytes(64);

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

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy(DevelopmentCorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevelopmentCorsPolicy);
}

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
        Status = error.Code == RegistrationErrorCode.EmailAlreadyRegistered
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest
    };

    problemDetails.Extensions["errors"] = error.Details;

    return error.Code == RegistrationErrorCode.EmailAlreadyRegistered
        ? Results.Conflict(problemDetails)
        : Results.BadRequest(problemDetails);
});

app.MapPost("/api/auth/login", async (
    LoginUserRequest request,
    ILoginUseCase loginUseCase,
    CancellationToken cancellationToken) =>
{
    var result = await loginUseCase.ExecuteAsync(
        new LoginRequest(request.Email, request.Password),
        cancellationToken);

    if (result.Succeeded && result.Value is not null)
    {
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

app.MapGet("/api/auth/me", async (
    IGetCurrentUserUseCase getCurrentUserUseCase,
    CancellationToken cancellationToken) =>
{
    var currentUser = await getCurrentUserUseCase.ExecuteAsync(cancellationToken);

    return currentUser is null
        ? Results.Forbid()
        : Results.Ok(currentUser);
}).RequireAuthorization();

app.Run();

public sealed record LoginUserRequest(
    string Email,
    string Password);

public sealed record RegisterUserAndCompanyRequest(
    string Name,
    string Email,
    string Password,
    string CompanyName);

public partial class Program;






