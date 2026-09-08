using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgf.Domain.Companies;

namespace Sgf.Infrastructure.Database.Configurations;

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("Memberships", table =>
        {
            table.HasCheckConstraint(
                "CK_Memberships_Role",
                "\"Role\" IN ('Owner', 'Admin', 'Member')");
        });

        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(membership => membership.Role)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(membership => membership.IsActive)
            .IsRequired();

        builder.Property(membership => membership.CreatedAt)
            .IsRequired();

        builder.HasIndex(membership => new { membership.UserId, membership.CompanyId })
            .IsUnique();

        builder.HasOne(membership => membership.Company)
            .WithMany(company => company.Memberships)
            .HasForeignKey(membership => membership.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
