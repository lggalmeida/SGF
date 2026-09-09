namespace Sgf.Domain.Companies;

public interface ICompanyScopedEntity
{
    Guid CompanyId { get; }
}
