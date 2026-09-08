using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Sgf.Domain.Companies;
using Sgf.Infrastructure.Database;
using Sgf.Infrastructure.Identity;

namespace Sgf.Infrastructure.Tests;

public sealed class IdentityAndCompanyModelTests
{
    [Fact]
    public void DbContextModel_IncludesIdentityCompanyAndMembershipEntities()
    {
        using var dbContext = CreateDbContext();

        Assert.NotNull(dbContext.Model.FindEntityType(typeof(ApplicationUser)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(Company)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(Membership)));
    }

    [Fact]
    public void Membership_HasRelationshipWithCompany()
    {
        using var dbContext = CreateDbContext();

        var membershipType = GetMembershipEntityType(dbContext);

        Assert.Contains(membershipType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Company)
            && foreignKey.Properties.Single().Name == nameof(Membership.CompanyId));
    }

    [Fact]
    public void Membership_HasRelationshipWithApplicationUser()
    {
        using var dbContext = CreateDbContext();

        var membershipType = GetMembershipEntityType(dbContext);

        Assert.Contains(membershipType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(ApplicationUser)
            && foreignKey.Properties.Single().Name == nameof(Membership.UserId));
    }

    [Fact]
    public void Membership_HasUniqueIndexForUserAndCompany()
    {
        using var dbContext = CreateDbContext();

        var membershipType = GetMembershipEntityType(dbContext);

        Assert.Contains(membershipType.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Membership.UserId), nameof(Membership.CompanyId)]));
    }

    [Fact]
    public void MembershipRole_DefinesOnlyInitialRoles()
    {
        var roleNames = Enum.GetNames<MembershipRole>();

        Assert.Equal(["Owner", "Admin", "Member"], roleNames);
    }

    [Fact]
    public void Membership_RejectsInvalidRole()
    {
        var invalidRole = (MembershipRole)999;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Membership("user-id", Guid.NewGuid(), invalidRole));
    }

    [Fact]
    public void MembershipRole_HasDatabaseCheckConstraint()
    {
        using var dbContext = CreateDbContext();

        var designTimeModel = dbContext.GetService<IDesignTimeModel>().Model;
        var membershipType = designTimeModel.FindEntityType(typeof(Membership))
            ?? throw new InvalidOperationException("Membership entity type was not configured.");

        Assert.Contains(membershipType.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_Memberships_Role"
            && constraint.Sql.Contains("Owner", StringComparison.Ordinal)
            && constraint.Sql.Contains("Admin", StringComparison.Ordinal)
            && constraint.Sql.Contains("Member", StringComparison.Ordinal));
    }

    private static SgfDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SgfDbContext>()
            .UseNpgsql("Host=localhost;Database=sgf_model_tests;Username=sgf_user;Password=sgf_dev_password")
            .Options;

        return new SgfDbContext(options);
    }

    private static IReadOnlyEntityType GetMembershipEntityType(SgfDbContext dbContext)
    {
        return dbContext.Model.FindEntityType(typeof(Membership))
            ?? throw new InvalidOperationException("Membership entity type was not configured.");
    }
}
