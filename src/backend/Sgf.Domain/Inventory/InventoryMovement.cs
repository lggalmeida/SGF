using Sgf.Domain.Companies;
using Sgf.Domain.Products;

namespace Sgf.Domain.Inventory;

public enum InventoryMovementType { Entry, Exit }

public sealed class InventoryMovement : ICompanyScopedEntity
{
    public const int MaxNotesLength = 1000;
    private InventoryMovement() { }

    internal InventoryMovement(Product product, InventoryMovementType type, decimal quantity, string? notes, string userId)
    {
        if (!Enum.IsDefined(type) || !Product.ValidStockQuantity(quantity) || quantity <= 0
            || notes?.Trim().Length > MaxNotesLength || string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Invalid inventory movement.");
        Id = Guid.NewGuid();
        CompanyId = product.CompanyId;
        ProductId = product.Id;
        Product = product;
        Type = type;
        Quantity = quantity;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UserId = userId;
        var now = DateTimeOffset.UtcNow;
        CreatedAt = now.AddTicks(-(now.Ticks % 10));
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public InventoryMovementType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string UserId { get; private set; } = string.Empty;
}
