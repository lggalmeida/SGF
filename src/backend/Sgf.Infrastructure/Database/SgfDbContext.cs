using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sgf.Domain.Companies;
using Sgf.Infrastructure.Database.MultiTenancy;
using Sgf.Infrastructure.Identity;
using Sgf.Infrastructure.Identity.Authentication;

namespace Sgf.Infrastructure.Database;

public sealed class SgfDbContext(DbContextOptions<SgfDbContext> options)
    : IdentityUserContext<ApplicationUser>(options), ICompanyScopedDbContext
{
    public Guid? CurrentCompanyId { get; private set; }

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<Sgf.Domain.Finance.FinancialEntry> FinancialEntries => Set<Sgf.Domain.Finance.FinancialEntry>();

    public DbSet<Sgf.Domain.Products.Product> Products => Set<Sgf.Domain.Products.Product>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Sgf.Domain.Inventory.InventoryMovement> InventoryMovements => Set<Sgf.Domain.Inventory.InventoryMovement>();

    private void ValidateInventoryHistory()
    {
        if (ChangeTracker.Entries<Sgf.Domain.Inventory.InventoryMovement>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Inventory history is immutable.");
    }

    public void UseCurrentCompany(Guid companyId)
    {
        CurrentCompanyId = companyId;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateInventoryHistory();
        ChangeTracker.ApplyCompanyScopeToChanges(CurrentCompanyId);

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidateInventoryHistory();
        ChangeTracker.ApplyCompanyScopeToChanges(CurrentCompanyId);

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(SgfDbContext).Assembly);
        builder.ApplyCompanyScopedQueryFilters(this);

        builder.Entity<Membership>()
            .HasOne<ApplicationUser>()
            .WithMany(user => user.Memberships)
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

