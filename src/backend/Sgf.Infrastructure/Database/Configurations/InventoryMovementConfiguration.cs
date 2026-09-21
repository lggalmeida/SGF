using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgf.Domain.Inventory;
using Sgf.Infrastructure.Identity;

namespace Sgf.Infrastructure.Database.Configurations;

public sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> b)
    {
        b.ToTable("InventoryMovements", t =>
        {
            t.HasCheckConstraint("CK_InventoryMovements_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_InventoryMovements_Type", "\"Type\" IN ('Entry', 'Exit')");
        });
        b.HasKey(m => m.Id);
        b.Property(m => m.Type).HasConversion<string>().HasMaxLength(10);
        b.Property(m => m.Quantity).HasPrecision(14, 3);
        b.Property(m => m.Notes).HasMaxLength(InventoryMovement.MaxNotesLength);
        b.Property(m => m.UserId).IsRequired();
        b.HasOne(m => m.Product).WithMany()
            .HasForeignKey(m => new { m.CompanyId, m.ProductId })
            .HasPrincipalKey(p => new { p.CompanyId, p.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(m => new { m.CompanyId, m.CreatedAt });
        b.HasIndex(m => new { m.CompanyId, m.ProductId, m.CreatedAt });
    }
}
