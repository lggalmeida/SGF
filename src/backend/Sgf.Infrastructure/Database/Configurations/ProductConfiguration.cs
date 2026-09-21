using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgf.Domain.Companies;
using Sgf.Domain.Products;

namespace Sgf.Infrastructure.Database.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_Prices", "\"CostPrice\" >= 0 AND \"SalePrice\" >= 0");
            table.HasCheckConstraint("CK_Products_Name", "length(btrim(\"Name\")) > 0");
            table.HasCheckConstraint("CK_Products_SKU", "length(\"SKU\") > 0");
            table.HasCheckConstraint("CK_Products_Stock", "\"CurrentStock\" >= 0 AND \"MinimumStock\" >= 0");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(Product.MaxNameLength).IsRequired();
        builder.Property(p => p.SKU).HasMaxLength(Product.MaxSkuLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(Product.MaxDescriptionLength);
        builder.Property(p => p.CostPrice).HasPrecision(12, 2);
        builder.Property(p => p.SalePrice).HasPrecision(12, 2);
        builder.Property(p => p.CurrentStock).HasPrecision(14, 3).HasDefaultValue(0m).IsConcurrencyToken();
        builder.Property(p => p.MinimumStock).HasPrecision(14, 3).HasDefaultValue(0m);
        builder.Property(p => p.IsActive).IsConcurrencyToken();
        builder.HasOne<Company>().WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.CompanyId, p.SKU }).IsUnique();
    }
}
