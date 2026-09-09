using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Sgf.Domain.Companies;

namespace Sgf.Infrastructure.Database.MultiTenancy;

public static class CompanyScopedChangeTrackerExtensions
{
    public static void ApplyCompanyScopeToChanges(
        this ChangeTracker changeTracker,
        Guid? currentCompanyId)
    {
        var companyScopedEntries = changeTracker.Entries()
            .Where(entry =>
                entry.Entity is ICompanyScopedEntity
                && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        foreach (var entry in companyScopedEntries)
        {
            var companyIdProperty = entry.Property(nameof(ICompanyScopedEntity.CompanyId));

            if (entry.State == EntityState.Added)
            {
                companyIdProperty.CurrentValue = GetRequiredCompanyId(currentCompanyId);
                continue;
            }

            var originalCompanyId = (Guid)companyIdProperty.OriginalValue!;
            var currentEntityCompanyId = (Guid)companyIdProperty.CurrentValue!;

            if (entry.State == EntityState.Modified && originalCompanyId != currentEntityCompanyId)
            {
                throw new InvalidOperationException("CompanyId cannot be changed for tenant-scoped entities.");
            }

            var currentTenantCompanyId = GetRequiredCompanyId(currentCompanyId);

            if (originalCompanyId != currentTenantCompanyId || currentEntityCompanyId != currentTenantCompanyId)
            {
                throw new TenantContextUnavailableException();
            }
        }
    }

    private static Guid GetRequiredCompanyId(Guid? currentCompanyId)
    {
        return currentCompanyId ?? throw new TenantContextUnavailableException();
    }
}
