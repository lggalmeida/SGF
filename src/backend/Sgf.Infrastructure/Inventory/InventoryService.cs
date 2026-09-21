using Microsoft.EntityFrameworkCore;
using Sgf.Application.Identity;
using Sgf.Application.Inventory;
using Sgf.Domain.Inventory;
using Sgf.Domain.Products;
using Sgf.Infrastructure.Database;
using Sgf.Infrastructure.Database.MultiTenancy;

namespace Sgf.Infrastructure.Inventory;

public sealed class InventoryService(SgfDbContext db, ICurrentTenantContext context) : IInventoryService
{
    private async Task<CurrentTenant> Tenant(CancellationToken ct) =>
        await context.GetCurrentAsync(ct) ?? throw new TenantContextUnavailableException();

    private static bool ValidPage(int page, int size) => page is >= 1 and <= 1000000 && size is >= 1 and <= 100;

    public async Task<InventoryResult<InventoryPage>> ListAsync(int page, int pageSize, string? search, bool lowStock, CancellationToken ct)
    {
        await Tenant(ct);
        if (!ValidPage(page, pageSize) || search?.Length > 200) return InventoryResult<InventoryPage>.Fail(InventoryError.Validation);
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            var sku = Product.NormalizeSku(search);
            query = query.Where(p => p.Name.ToUpper().Contains(term) || p.SKU.Contains(sku));
        }
        if (lowStock) query = query.Where(p => p.IsActive && p.CurrentStock <= p.MinimumStock);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Name).ThenBy(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new InventoryItem(p.Id, p.Name, p.SKU, p.CurrentStock, p.MinimumStock,
                p.IsActive && p.CurrentStock <= p.MinimumStock, p.IsActive)).ToArrayAsync(ct);
        return new(new(items, page, pageSize, total));
    }

    public async Task<InventoryResult<MovementPage>> HistoryAsync(int page, int pageSize, Guid? productId, string? type, CancellationToken ct)
    {
        await Tenant(ct);
        if (!ValidPage(page, pageSize) || type is not (null or "Entry" or "Exit"))
            return InventoryResult<MovementPage>.Fail(InventoryError.Validation);
        if (productId.HasValue && !await db.Products.AnyAsync(p => p.Id == productId.Value, ct))
            return InventoryResult<MovementPage>.Fail(InventoryError.NotFound);
        var query = db.InventoryMovements.AsNoTracking();
        if (productId.HasValue) query = query.Where(m => m.ProductId == productId.Value);
        if (type is not null)
        {
            var movementType = Enum.Parse<InventoryMovementType>(type);
            query = query.Where(m => m.Type == movementType);
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new MovementResponse(m.Id, m.ProductId, m.Product.Name, m.Product.SKU, m.Type.ToString(),
                m.Quantity, m.Notes, m.CreatedAt, m.UserId,
                db.Users.Where(u => u.Id == m.UserId).Select(u => u.Name).FirstOrDefault())).ToArrayAsync(ct);
        return new(new(items, page, pageSize, total));
    }

    public Task<InventoryResult<MovementReceipt>> EntryAsync(InventoryRequest request, CancellationToken ct) => Move(request, InventoryMovementType.Entry, ct);
    public Task<InventoryResult<MovementReceipt>> ExitAsync(InventoryRequest request, CancellationToken ct) => Move(request, InventoryMovementType.Exit, ct);

    private async Task<InventoryResult<MovementReceipt>> Move(InventoryRequest request, InventoryMovementType type, CancellationToken ct)
    {
        var tenant = await Tenant(ct);
        if (request.ProductId == Guid.Empty || !request.Quantity.HasValue
            || !Product.ValidStockQuantity(request.Quantity.Value) || request.Quantity <= 0
            || request.Notes?.Trim().Length > InventoryMovement.MaxNotesLength)
            return InventoryResult<MovementReceipt>.Fail(InventoryError.Validation);
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == request.ProductId, ct);
        if (product is null) return InventoryResult<MovementReceipt>.Fail(InventoryError.NotFound);
        if (!product.IsActive) return InventoryResult<MovementReceipt>.Fail(InventoryError.InactiveProduct);
        var quantity = request.Quantity.Value;
        if (type == InventoryMovementType.Exit && quantity > product.CurrentStock)
            return InventoryResult<MovementReceipt>.Fail(InventoryError.InsufficientStock);
        if (type == InventoryMovementType.Entry && quantity > Product.MaxStock - product.CurrentStock)
            return InventoryResult<MovementReceipt>.Fail(InventoryError.StockLimit);
        var movement = product.RecordMovement(type, quantity, request.Notes, tenant.UserId);
        db.InventoryMovements.Add(movement);
        try
        {
            // One SaveChanges transaction commits both the balance and its immutable evidence.
            await db.SaveChangesAsync(ct);
            return new(new(movement.Id, product.Id, type.ToString(), quantity, product.CurrentStock));
        }
        catch (DbUpdateConcurrencyException)
        {
            return InventoryResult<MovementReceipt>.Fail(InventoryError.Conflict);
        }
    }
}
