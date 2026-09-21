namespace Sgf.Application.Inventory;

public sealed record InventoryRequest(Guid ProductId, decimal? Quantity, string? Notes);
public sealed record InventoryItem(Guid ProductId, string Name, string SKU, decimal CurrentStock, decimal MinimumStock, bool IsLowStock, bool IsActive);
public sealed record InventoryPage(InventoryItem[] Items, int Page, int PageSize, int TotalCount);
public sealed record MovementResponse(Guid Id, Guid ProductId, string ProductName, string SKU, string Type,
    decimal Quantity, string? Notes, DateTimeOffset CreatedAt, string UserId, string? UserName);
public sealed record MovementPage(MovementResponse[] Items, int Page, int PageSize, int TotalCount);
public sealed record MovementReceipt(Guid Id, Guid ProductId, string Type, decimal Quantity, decimal CurrentStock);
public enum InventoryError { Validation, NotFound, InactiveProduct, InsufficientStock, StockLimit, Conflict }
public sealed record InventoryResult<T>(T? Value, InventoryError? Error = null)
{
    public static InventoryResult<T> Fail(InventoryError error) => new(default, error);
}

public interface IInventoryService
{
    Task<InventoryResult<InventoryPage>> ListAsync(int page, int pageSize, string? search, bool lowStock, CancellationToken ct);
    Task<InventoryResult<MovementPage>> HistoryAsync(int page, int pageSize, Guid? productId, string? type, CancellationToken ct);
    Task<InventoryResult<MovementReceipt>> EntryAsync(InventoryRequest request, CancellationToken ct);
    Task<InventoryResult<MovementReceipt>> ExitAsync(InventoryRequest request, CancellationToken ct);
}
