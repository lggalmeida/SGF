using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgf.Domain.Companies;
using Sgf.Infrastructure.Identity;
using Sgf.Infrastructure.Identity.Authentication;

namespace Sgf.Infrastructure.Database.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(refreshToken => refreshToken.Id);

        builder.Property(refreshToken => refreshToken.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(refreshToken => refreshToken.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(refreshToken => refreshToken.CreatedAt)
            .IsRequired();

        builder.Property(refreshToken => refreshToken.ExpiresAt)
            .IsRequired();

        builder.HasIndex(refreshToken => refreshToken.TokenHash)
            .IsUnique();

        builder.Property(refreshToken => refreshToken.RevokedAt).IsConcurrencyToken();

        builder.HasIndex(refreshToken => new { refreshToken.UserId, refreshToken.CompanyId });

        builder.HasOne<ApplicationUser>()
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(refreshToken => refreshToken.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(refreshToken => refreshToken.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

