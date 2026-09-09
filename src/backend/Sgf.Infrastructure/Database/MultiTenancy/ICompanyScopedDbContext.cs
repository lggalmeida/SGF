namespace Sgf.Infrastructure.Database.MultiTenancy;

public interface ICompanyScopedDbContext
{
    Guid? CurrentCompanyId { get; }
}
