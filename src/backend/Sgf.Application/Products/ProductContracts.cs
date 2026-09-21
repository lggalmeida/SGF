namespace Sgf.Application.Products;

public sealed record SaveProductRequest(string? Name, string? SKU, string? Description, decimal? CostPrice, decimal? SalePrice, decimal MinimumStock = 0);
public sealed record ProductStatusRequest(bool? IsActive);
public sealed record ProductResponse(Guid Id, string Name, string SKU, string? Description, decimal CostPrice,
    decimal SalePrice, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, decimal CurrentStock = 0, decimal MinimumStock = 0);
public sealed record ProductPage(ProductResponse[] Items, int Page, int PageSize, int TotalCount);
public enum ProductError { Validation, NotFound, DuplicateSku, Conflict }
public sealed record ProductResult<T>(T? Value, ProductError? Error = null, string[]? Details = null)
{
    public static ProductResult<T> Failure(ProductError error, params string[] details) => new(default, error, details);
}

public interface IProductService
{
    Task<ProductResult<ProductPage>> ListAsync(int page, int pageSize, string? search, bool? isActive, CancellationToken ct);
    Task<ProductResult<ProductResponse>> GetAsync(Guid id, CancellationToken ct);
    Task<ProductResult<ProductResponse>> CreateAsync(SaveProductRequest request, CancellationToken ct);
    Task<ProductResult<ProductResponse>> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken ct);
    Task<ProductResult<ProductResponse>> SetStatusAsync(Guid id, bool? isActive, CancellationToken ct);
}
