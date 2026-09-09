using Microsoft.EntityFrameworkCore;
using Sgf.Domain.Companies;
using Sgf.Infrastructure.Database.MultiTenancy;

namespace Sgf.Infrastructure.Tests.MultiTenancy;

public sealed class CompanyScopedEntityIsolationTests
{
    [Fact]
    public async Task Queries_ReturnOnlyRecordsFromCurrentCompany()
    {
        var databaseName = CreateDatabaseName();
        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();
        await CreateDatabaseAsync(databaseName);
        await SeedAsync(databaseName, companyAId, companyBId);

        await using var companyAContext = CreateDbContext(databaseName, companyAId);
        await using var companyBContext = CreateDbContext(databaseName, companyBId);

        var companyARecords = await companyAContext.Records.Select(record => record.Name).ToListAsync();
        var companyBRecords = await companyBContext.Records.Select(record => record.Name).ToListAsync();

        Assert.Equal(["Record A"], companyARecords);
        Assert.Equal(["Record B"], companyBRecords);
    }

    [Fact]
    public async Task FindById_RespectsCurrentCompanyFilter()
    {
        var databaseName = CreateDatabaseName();
        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();
        await CreateDatabaseAsync(databaseName);
        var seeded = await SeedAsync(databaseName, companyAId, companyBId);

        await using var companyAContext = CreateDbContext(databaseName, companyAId);
        await using var companyBContext = CreateDbContext(databaseName, companyBId);

        var visibleForCompanyA = await companyAContext.Records.SingleOrDefaultAsync(record => record.Id == seeded.CompanyARecordId);
        var visibleForCompanyB = await companyBContext.Records.SingleOrDefaultAsync(record => record.Id == seeded.CompanyARecordId);

        Assert.NotNull(visibleForCompanyA);
        Assert.Null(visibleForCompanyB);
    }

    [Fact]
    public async Task Add_UsesCurrentCompanyIdEvenWhenEntityContainsDifferentCompanyId()
    {
        var databaseName = CreateDatabaseName();
        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();
        await CreateDatabaseAsync(databaseName);

        await using var dbContext = CreateDbContext(databaseName, companyAId);
        var record = new TestCompanyScopedRecord(companyBId, "Created by A");

        dbContext.Records.Add(record);
        await dbContext.SaveChangesAsync();

        var persisted = await dbContext.Records.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(companyAId, persisted.CompanyId);
    }

    [Fact]
    public async Task Add_WithoutCurrentCompanyContext_FailsClosed()
    {
        var databaseName = CreateDatabaseName();
        await CreateDatabaseAsync(databaseName);

        await using var dbContext = CreateDbContext(databaseName);
        dbContext.Records.Add(new TestCompanyScopedRecord(Guid.NewGuid(), "No tenant"));

        await Assert.ThrowsAsync<TenantContextUnavailableException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Query_WithoutCurrentCompanyContext_ReturnsNoTenantScopedData()
    {
        var databaseName = CreateDatabaseName();
        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();
        await CreateDatabaseAsync(databaseName);
        await SeedAsync(databaseName, companyAId, companyBId);

        await using var dbContext = CreateDbContext(databaseName);

        Assert.Empty(await dbContext.Records.ToListAsync());
    }

    [Fact]
    public async Task Update_CurrentCompanyCanUpdateOwnRecord()
    {
        var databaseName = CreateDatabaseName();
        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();
        await CreateDatabaseAsync(databaseName);
        var seeded = await SeedAsync(databaseName, companyAId, companyBId);

        await using var dbContext = CreateDbContext(databaseName, companyAId);
        var record = await dbContext.Records.SingleAsync(record => record.Id == seeded.CompanyARecordId);
        record.Rename("Updated by A");

        await dbContext.SaveChangesAsync();

        Assert.Equal("Updated by A", record.Name);
    }

    [Fact]
    public async Task Update_OtherCompanyCannotUpdateRecord()
    {
        var databaseName = CreateDatabaseName();
        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();
        await CreateDatabaseAsync(databaseName);
        var seeded = await SeedAsync(databaseName, companyAId, companyBId);

        await using var dbContext = CreateDbContext(databaseName, companyBId);
        var record = await dbContext.Records
            .IgnoreQueryFilters()
            .SingleAsync(record => record.Id == seeded.CompanyARecordId);
        record.Rename("Malicious update by B");

        await Assert.ThrowsAsync<TenantContextUnavailableException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Update_CannotChangeCompanyId()
    {
        var databaseName = CreateDatabaseName();
        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();
        await CreateDatabaseAsync(databaseName);
        var seeded = await SeedAsync(databaseName, companyAId, companyBId);

        await using var dbContext = CreateDbContext(databaseName, companyAId);
        var record = await dbContext.Records.SingleAsync(record => record.Id == seeded.CompanyARecordId);

        dbContext.Entry(record).Property(entity => entity.CompanyId).CurrentValue = companyBId;

        await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Delete_CurrentCompanyCanDeleteOwnRecord()
    {
        var databaseName = CreateDatabaseName();
        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();
        await CreateDatabaseAsync(databaseName);
        var seeded = await SeedAsync(databaseName, companyAId, companyBId);

        await using var dbContext = CreateDbContext(databaseName, companyAId);
        var record = await dbContext.Records.SingleAsync(record => record.Id == seeded.CompanyARecordId);
        dbContext.Records.Remove(record);

        await dbContext.SaveChangesAsync();

        Assert.Null(await dbContext.Records.IgnoreQueryFilters().SingleOrDefaultAsync(record => record.Id == seeded.CompanyARecordId));
    }

    [Fact]
    public async Task Delete_OtherCompanyCannotDeleteRecord()
    {
        var databaseName = CreateDatabaseName();
        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();
        await CreateDatabaseAsync(databaseName);
        var seeded = await SeedAsync(databaseName, companyAId, companyBId);

        await using var dbContext = CreateDbContext(databaseName, companyBId);
        var record = await dbContext.Records
            .IgnoreQueryFilters()
            .SingleAsync(record => record.Id == seeded.CompanyARecordId);
        dbContext.Records.Remove(record);

        await Assert.ThrowsAsync<TenantContextUnavailableException>(() => dbContext.SaveChangesAsync());
    }

    private static async Task CreateDatabaseAsync(string databaseName)
    {
        await using var dbContext = CreateDbContext(databaseName);
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }

    private static async Task<SeededRecords> SeedAsync(
        string databaseName,
        Guid companyAId,
        Guid companyBId)
    {
        await using var companyAContext = CreateDbContext(databaseName, companyAId);
        var companyARecord = new TestCompanyScopedRecord(Guid.Empty, "Record A");
        companyAContext.Records.Add(companyARecord);
        await companyAContext.SaveChangesAsync();

        await using var companyBContext = CreateDbContext(databaseName, companyBId);
        var companyBRecord = new TestCompanyScopedRecord(Guid.Empty, "Record B");
        companyBContext.Records.Add(companyBRecord);
        await companyBContext.SaveChangesAsync();

        return new SeededRecords(companyARecord.Id, companyBRecord.Id);
    }

    private static MultiTenantTestDbContext CreateDbContext(string databaseName, Guid? companyId = null)
    {
        var options = new DbContextOptionsBuilder<MultiTenantTestDbContext>()
            .UseNpgsql($"Host=127.0.0.1;Port=15432;Database={databaseName};Username=sgf_user;Password=sgf_dev_password")
            .Options;

        var dbContext = new MultiTenantTestDbContext(options);

        if (companyId.HasValue)
        {
            dbContext.UseCurrentCompany(companyId.Value);
        }

        return dbContext;
    }

    private static string CreateDatabaseName()
    {
        return $"sgf_multitenancy_tests_{Guid.NewGuid():N}";
    }

    private sealed record SeededRecords(Guid CompanyARecordId, Guid CompanyBRecordId);

    private sealed class MultiTenantTestDbContext(DbContextOptions<MultiTenantTestDbContext> options)
        : DbContext(options), ICompanyScopedDbContext
    {
        public Guid? CurrentCompanyId { get; private set; }

        public DbSet<TestCompanyScopedRecord> Records => Set<TestCompanyScopedRecord>();

        public void UseCurrentCompany(Guid companyId)
        {
            CurrentCompanyId = companyId;
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ChangeTracker.ApplyCompanyScopeToChanges(CurrentCompanyId);

            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            ChangeTracker.ApplyCompanyScopeToChanges(CurrentCompanyId);

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestCompanyScopedRecord>(builder =>
            {
                builder.ToTable("TestCompanyScopedRecords");
                builder.HasKey(record => record.Id);
                builder.Property(record => record.Name).HasMaxLength(100).IsRequired();
            });

            modelBuilder.ApplyCompanyScopedQueryFilters(this);
        }
    }

    private sealed class TestCompanyScopedRecord : ICompanyScopedEntity
    {
        private TestCompanyScopedRecord()
        {
        }

        public TestCompanyScopedRecord(Guid companyId, string name)
        {
            Id = Guid.NewGuid();
            CompanyId = companyId;
            Name = name;
        }

        public Guid Id { get; private set; }

        public Guid CompanyId { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public void Rename(string name)
        {
            Name = name;
        }
    }
}
