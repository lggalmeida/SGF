using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgf.Domain.Companies;
using Sgf.Domain.Finance;

namespace Sgf.Infrastructure.Database.Configurations;

public sealed class FinancialEntryConfiguration : IEntityTypeConfiguration<FinancialEntry>
{
    public void Configure(EntityTypeBuilder<FinancialEntry> builder)
    {
        builder.ToTable("FinancialEntries", t =>
        {
            t.HasCheckConstraint("CK_FinancialEntries_Amount", "\"Amount\" > 0");
            t.HasCheckConstraint("CK_FinancialEntries_Description", "length(btrim(\"Description\")) > 0");
            t.HasCheckConstraint("CK_FinancialEntries_Type", "\"Type\" IN ('Income', 'Expense')");
            t.HasCheckConstraint("CK_FinancialEntries_Payment",
                "(\"Status\" = 'Pending' AND \"PaidAt\" IS NULL) OR (\"Status\" = 'Paid' AND \"PaidAt\" IS NOT NULL)");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(100);
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.Amount).HasPrecision(14, 2);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CompanyId, e.DueDate });
    }
}
