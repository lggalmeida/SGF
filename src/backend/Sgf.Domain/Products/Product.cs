using Sgf.Domain.Companies;
using Sgf.Domain.Inventory;

namespace Sgf.Domain.Products;

public sealed class Product : ICompanyScopedEntity
{
    public const int MaxNameLength = 200;
    public const int MaxSkuLength = 64;
    public const int MaxDescriptionLength = 2000;
    public const decimal MaxPrice = 9999999999.99m;
    public const decimal MaxStock = 99999999999.999m;

    private Product() { }

    public Product(string name, string sku, string? description, decimal costPrice, decimal salePrice, decimal minimumStock = 0)
    {
        Id = Guid.NewGuid();
        IsActive = true;
        CreatedAt = UtcNow();
        Update(name, sku, description, costPrice, salePrice, minimumStock);
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string SKU { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal CostPrice { get; private set; }
    public decimal SalePrice { get; private set; }
    public decimal CurrentStock { get; private set; }
    public decimal MinimumStock { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static string NormalizeSku(string? sku) =>
        new string((sku ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    public static string[] Validate(string? name, string? sku, string? description, decimal? costPrice, decimal? salePrice, decimal minimumStock = 0)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
            errors.Add("Informe um nome com ate 200 caracteres.");
        var normalizedSku = NormalizeSku(sku);
        if (normalizedSku.Length is 0 or > MaxSkuLength)
            errors.Add("Informe um SKU com ate 64 caracteres, sem espacos.");
        if (description?.Trim().Length > MaxDescriptionLength)
            errors.Add("A descricao deve ter ate 2000 caracteres.");
        if (!ValidPrice(costPrice)) errors.Add("Custo deve estar entre 0 e 9999999999.99, com ate duas casas decimais.");
        if (!ValidPrice(salePrice)) errors.Add("Venda deve estar entre 0 e 9999999999.99, com ate duas casas decimais.");
        if (!ValidStockQuantity(minimumStock)) errors.Add("Estoque minimo deve ser nao negativo, com ate tres casas decimais.");
        return errors.ToArray();
    }

    private static bool ValidPrice(decimal? value) =>
        value.HasValue && value.Value >= 0 && value.Value <= MaxPrice && decimal.Round(value.Value, 2) == value.Value;

    private static DateTimeOffset UtcNow()
    {
        // PostgreSQL timestamps preserve microseconds, rather than .NET's 100ns ticks.
        var now = DateTimeOffset.UtcNow;
        return now.AddTicks(-(now.Ticks % 10));
    }

    public static bool ValidStockQuantity(decimal value) =>
        value >= 0 && value <= MaxStock && decimal.Round(value, 3) == value;

    public InventoryMovement RecordMovement(InventoryMovementType type, decimal quantity, string? notes, string userId)
    {
        if (!IsActive || !Enum.IsDefined(type) || !ValidStockQuantity(quantity) || quantity <= 0)
            throw new InvalidOperationException("Invalid stock operation.");
        var next = type == InventoryMovementType.Entry ? CurrentStock + quantity : CurrentStock - quantity;
        if (!ValidStockQuantity(next)) throw new InvalidOperationException("Stock outside allowed range.");
        var movement = new InventoryMovement(this, type, quantity, notes, userId);
        CurrentStock = next;
        UpdatedAt = UtcNow();
        return movement;
    }

    public void Update(string name, string sku, string? description, decimal costPrice, decimal salePrice, decimal minimumStock = 0)
    {
        var errors = Validate(name, sku, description, costPrice, salePrice, minimumStock);
        if (errors.Length > 0) throw new ArgumentException(string.Join(" ", errors));
        Name = name.Trim();
        SKU = NormalizeSku(sku);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CostPrice = costPrice;
        SalePrice = salePrice;
        MinimumStock = minimumStock;
        UpdatedAt = UtcNow();
    }

    public void SetActive(bool isActive)
    {
        if (IsActive == isActive) return;
        IsActive = isActive;
        UpdatedAt = UtcNow();
    }
}
