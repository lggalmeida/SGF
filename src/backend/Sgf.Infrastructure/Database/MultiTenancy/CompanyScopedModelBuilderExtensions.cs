using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Sgf.Domain.Companies;

namespace Sgf.Infrastructure.Database.MultiTenancy;

public static class CompanyScopedModelBuilderExtensions
{
    public static void ApplyCompanyScopedQueryFilters(
        this ModelBuilder modelBuilder,
        ICompanyScopedDbContext dbContext)
    {
        var companyScopedEntityTypes = modelBuilder.Model
            .GetEntityTypes()
            .Where(entityType =>
                entityType.ClrType is not null
                && typeof(ICompanyScopedEntity).IsAssignableFrom(entityType.ClrType));

        foreach (var entityType in companyScopedEntityTypes)
        {
            // Include the tenant in UPDATE/DELETE predicates, including detached entities.
            modelBuilder.Entity(entityType.ClrType)
                .Property<Guid>(nameof(ICompanyScopedEntity.CompanyId)).IsConcurrencyToken();

            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var companyIdProperty = Expression.Call(
                typeof(EF),
                nameof(EF.Property),
                [typeof(Guid)],
                parameter,
                Expression.Constant(nameof(ICompanyScopedEntity.CompanyId)));
            var nullableCompanyIdProperty = Expression.Convert(companyIdProperty, typeof(Guid?));

            var currentCompanyId = Expression.Property(
                Expression.Constant(dbContext),
                nameof(ICompanyScopedDbContext.CurrentCompanyId));
            var hasCurrentCompanyId = Expression.Property(
                currentCompanyId,
                nameof(Nullable<Guid>.HasValue));

            var belongsToCurrentCompany = Expression.Equal(nullableCompanyIdProperty, currentCompanyId);
            var filterBody = Expression.AndAlso(hasCurrentCompanyId, belongsToCurrentCompany);
            var filter = Expression.Lambda(filterBody, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}
