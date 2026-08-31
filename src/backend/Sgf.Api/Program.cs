using Microsoft.EntityFrameworkCore;
using Sgf.Infrastructure;
using Sgf.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

const string DevelopmentCorsPolicy = "DevelopmentCors";

builder.Services.AddInfrastructure(builder.Configuration);

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

app.Run();

public partial class Program;
