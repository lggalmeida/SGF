using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sgf.Domain.Companies;
using Sgf.Infrastructure.Identity;

namespace Sgf.Infrastructure.Database;

public sealed class SgfDbContext(DbContextOptions<SgfDbContext> options)
    : IdentityUserContext<ApplicationUser>(options)
{
    public DbSet<Company> Companies => Set<Company>();

    public DbSet<Membership> Memberships => Set<Membership>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(SgfDbContext).Assembly);

        builder.Entity<Membership>()
            .HasOne<ApplicationUser>()
            .WithMany(user => user.Memberships)
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
