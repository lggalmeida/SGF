using Microsoft.EntityFrameworkCore;
using Sgf.Domain.Companies;
using Sgf.Infrastructure.Database.MultiTenancy;

namespace Sgf.Infrastructure.Tests.MultiTenancy;

public sealed class CompanyScopedEntityIsolationTests : IAsyncLifetime
{
    private readonly List<string> _databases = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        var failures = new List<Exception>();
        foreach (var databaseName in _databases)
        {
            try
            {
                await using var db = CreateDbContext(databaseName);
                await db.Database.EnsureDeletedAsync();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
        // xUnit reports cleanup errors alongside the original test failure.
        if (failures.Count > 0) throw new AggregateException("Test database cleanup failed.", failures);
    }

    [Theory]
    [InlineData("attach-modified", false, false)]
    [InlineData("update", false, false)]
    [InlineData("attach-remove", false, false)]
    [InlineData("remove", false, false)]
    [InlineData("attach-modified", true, false)]
    [InlineData("update", true, false)]
    [InlineData("attach-remove", true, false)]
    [InlineData("remove", true, false)]
    [InlineData("attach-modified", false, true)]
    [InlineData("update", false, true)]
    [InlineData("attach-remove", false, true)]
    [InlineData("remove", false, true)]
    [InlineData("attach-modified", true, true)]
    [InlineData("update", true, true)]
    [InlineData("attach-remove", true, true)]
    [InlineData("remove", true, true)]
    public async Task DetachedWrites_CheckPersistedCompany(string pattern, bool ownsRecord, bool synchronous)
    {
        var databaseName = CreateDatabaseName();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        await CreateDatabaseAsync(databaseName);
        var seeded = await SeedAsync(databaseName, companyA, companyB);
        var targetId = ownsRecord ? seeded.CompanyARecordId : seeded.CompanyBRecordId;
        var sql = new List<string>();
        await using var db = CreateDbContext(databaseName, companyA, sql);

        // Deliberately forge CompanyId=A without loading the target from the database.
        var detached = new TestCompanyScopedRecord(companyA, "Changed", targetId);
        switch (pattern)
        {
            case "attach-modified": db.Attach(detached).State = EntityState.Modified; break;
            case "update": db.Update(detached); break;
            case "attach-remove": db.Attach(detached); db.Remove(detached); break;
            case "remove": db.Remove(detached); break;
        }
        Assert.Empty(sql);
        if (ownsRecord)
        {
            if (synchronous) db.SaveChanges();
            else await db.SaveChangesAsync();
        }
        else if (synchronous)
        {
            Assert.Throws<DbUpdateConcurrencyException>(() => db.SaveChanges());
        }
        else
        {
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        }

        var delete = pattern.Contains("remove");
        var command = Assert.Single(sql, line => line.Contains(delete ? "DELETE FROM" : "UPDATE "));
        var predicate = command[command.IndexOf("WHERE", StringComparison.Ordinal)..];
        Assert.Contains("\"CompanyId\" =", predicate);
        Assert.Contains("\"Id\" =", predicate);

        await using var verification = CreateDbContext(databaseName, ownsRecord ? companyA : companyB);
        var persisted = await verification.Records.SingleOrDefaultAsync(r => r.Id == targetId);
        if (ownsRecord && delete) Assert.Null(persisted);
        else
        {
            Assert.NotNull(persisted);
            Assert.Equal(ownsRecord ? companyA : companyB, persisted.CompanyId);
            Assert.Equal(ownsRecord ? "Changed" : "Record B", persisted.Name);
        }
    }

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

    private static MultiTenantTestDbContext CreateDbContext(string databaseName, Guid? companyId = null, List<string>? sql = null)
    {
        var builder = new DbContextOptionsBuilder<MultiTenantTestDbContext>()
            .UseNpgsql($"Host=127.0.0.1;Port=15432;Database={databaseName};Username=sgf_user;Password=sgf_dev_password");
        if (sql is not null) builder.LogTo(sql.Add, [DbLoggerCategory.Database.Command.Name], Microsoft.Extensions.Logging.LogLevel.Information);

        var dbContext = new MultiTenantTestDbContext(builder.Options);

        if (companyId.HasValue)
        {
            dbContext.UseCurrentCompany(companyId.Value);
        }

        return dbContext;
    }

    private string CreateDatabaseName()
    {
        var name = $"sgf_multitenancy_tests_{Guid.NewGuid():N}";
        _databases.Add(name);
        return name;
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

        public TestCompanyScopedRecord(Guid companyId, string name, Guid? id = null)
        {
            Id = id ?? Guid.NewGuid();
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
