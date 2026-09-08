using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgf.Domain.Companies;

namespace Sgf.Infrastructure.Database.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(company => company.Id);

        builder.Property(company => company.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(company => company.TradeName)
            .HasMaxLength(200);

        builder.Property(company => company.IsActive)
            .IsRequired();

        builder.Property(company => company.CreatedAt)
            .IsRequired();

        builder.Property(company => company.UpdatedAt)
            .IsRequired();

        builder.Navigation(company => company.Memberships)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
