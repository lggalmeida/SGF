using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sgf.Application.Identity;
using Sgf.Infrastructure.Database;
using Sgf.Infrastructure.Identity;
using Sgf.Infrastructure.Identity.Authentication;
using Sgf.Infrastructure.Identity.Registration;

namespace Sgf.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<SgfDbContext>(options =>
            options.UseNpgsql(connectionString));
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddEntityFrameworkStores<SgfDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IRegisterCompanyOwnerUseCase, RegisterCompanyOwnerService>();
        services.AddScoped<ILoginUseCase, LoginService>();
        services.AddScoped<IGetCurrentUserUseCase, GetCurrentUserService>();

        return services;
    }
}





