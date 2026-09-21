using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgf.Application.Identity;
using Sgf.Application.Products;
using Sgf.Domain.Products;
using Sgf.Infrastructure.Database;
using Sgf.Infrastructure.Database.MultiTenancy;

namespace Sgf.Infrastructure.Products;

public sealed class ProductService(SgfDbContext db, ICurrentTenantContext tenant) : IProductService
{
    private static readonly Expression<Func<Product, ProductResponse>> Projection = p =>
        new(p.Id, p.Name, p.SKU, p.Description, p.CostPrice, p.SalePrice, p.IsActive, p.CreatedAt, p.UpdatedAt, p.CurrentStock, p.MinimumStock);

    private async Task RequireTenant(CancellationToken ct)
    {
        if (await tenant.GetCurrentAsync(ct) is null) throw new TenantContextUnavailableException();
    }

    public async Task<ProductResult<ProductPage>> ListAsync(int page, int pageSize, string? search, bool? isActive, CancellationToken ct)
    {
        await RequireTenant(ct);
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 100 || search?.Length > 200)
            return ProductResult<ProductPage>.Failure(ProductError.Validation, "Paginacao ou busca invalida.");
        var query = db.Products.AsNoTracking();
        if (isActive.HasValue) query = query.Where(p => p.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            var sku = Product.NormalizeSku(search);
            query = query.Where(p => p.Name.ToUpper().Contains(term) || p.SKU.Contains(sku));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(Projection).ToArrayAsync(ct);
        return new(new ProductPage(items, page, pageSize, total));
    }

    public async Task<ProductResult<ProductResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        await RequireTenant(ct);
        var value = await db.Products.AsNoTracking().Where(p => p.Id == id).Select(Projection).SingleOrDefaultAsync(ct);
        return value is null ? ProductResult<ProductResponse>.Failure(ProductError.NotFound) : new(value);
    }

    public async Task<ProductResult<ProductResponse>> CreateAsync(SaveProductRequest request, CancellationToken ct)
    {
        await RequireTenant(ct);
        var errors = Validate(request);
        if (errors.Length > 0) return ProductResult<ProductResponse>.Failure(ProductError.Validation, errors);
        var product = new Product(request.Name!, request.SKU!, request.Description, request.CostPrice!.Value, request.SalePrice!.Value, request.MinimumStock);
        db.Products.Add(product);
        return await Save(product, ct);
    }

    public async Task<ProductResult<ProductResponse>> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken ct)
    {
        await RequireTenant(ct);
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return ProductResult<ProductResponse>.Failure(ProductError.NotFound);
        var errors = Validate(request);
        if (errors.Length > 0) return ProductResult<ProductResponse>.Failure(ProductError.Validation, errors);
        product.Update(request.Name!, request.SKU!, request.Description, request.CostPrice!.Value, request.SalePrice!.Value, request.MinimumStock);
        return await Save(product, ct);
    }

    public async Task<ProductResult<ProductResponse>> SetStatusAsync(Guid id, bool? isActive, CancellationToken ct)
    {
        await RequireTenant(ct);
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return ProductResult<ProductResponse>.Failure(ProductError.NotFound);
        if (!isActive.HasValue) return ProductResult<ProductResponse>.Failure(ProductError.Validation, "Informe o status.");
        product.SetActive(isActive.Value);
        return await Save(product, ct);
    }

    private static string[] Validate(SaveProductRequest request) =>
        Product.Validate(request.Name, request.SKU, request.Description, request.CostPrice, request.SalePrice, request.MinimumStock);

    private async Task<ProductResult<ProductResponse>> Save(Product p, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return new(new ProductResponse(p.Id, p.Name, p.SKU, p.Description, p.CostPrice, p.SalePrice, p.IsActive, p.CreatedAt, p.UpdatedAt, p.CurrentStock, p.MinimumStock));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Products_CompanyId_SKU" })
        {
            // The unique index also protects simultaneous requests.
            return ProductResult<ProductResponse>.Failure(ProductError.DuplicateSku);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ProductResult<ProductResponse>.Failure(ProductError.Conflict);
        }
    }
}
