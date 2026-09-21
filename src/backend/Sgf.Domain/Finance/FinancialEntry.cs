using Sgf.Domain.Companies;

namespace Sgf.Domain.Finance;

public enum FinancialEntryType { Income, Expense }
public enum FinancialEntryStatus { Pending, Paid }

public sealed class FinancialEntry : ICompanyScopedEntity
{
    public const decimal MaxAmount = 999999999999.99m;
    private FinancialEntry() { }

    public FinancialEntry(FinancialEntryType type, string description, string? category,
        decimal amount, DateOnly dueDate, string? notes)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentException("Invalid financial type.");
        Id = Guid.NewGuid();
        Type = type;
        CreatedAt = Now();
        Update(description, category, amount, dueDate, notes);
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public FinancialEntryType Type { get; private set; }
    public string Description { get; private set; } = "";
    public string? Category { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly DueDate { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public FinancialEntryStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid Version { get; private set; }

    public static string[] Validate(string? description, string? category, decimal? amount, DateOnly? dueDate, string? notes)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 200)
            errors.Add("Informe uma descricao com ate 200 caracteres.");
        if (category?.Trim().Length > 100) errors.Add("Categoria deve ter ate 100 caracteres.");
        if (!amount.HasValue || amount <= 0 || amount > MaxAmount || decimal.Round(amount.Value, 2) != amount)
            errors.Add("Valor deve ser positivo, ate 999999999999.99, com no maximo duas casas decimais.");
        if (!dueDate.HasValue || dueDate == DateOnly.MinValue) errors.Add("Informe o vencimento.");
        if (notes?.Trim().Length > 2000) errors.Add("Observacao deve ter ate 2000 caracteres.");
        return errors.ToArray();
    }

    public void Update(string description, string? category, decimal amount, DateOnly dueDate, string? notes)
    {
        if (Status != FinancialEntryStatus.Pending) throw new InvalidOperationException("Paid entries cannot be edited.");
        var errors = Validate(description, category, amount, dueDate, notes);
        if (errors.Length > 0) throw new ArgumentException(string.Join(" ", errors));
        Description = description.Trim();
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        Amount = amount;
        DueDate = dueDate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = Now();
        Version = Guid.NewGuid();
    }

    public void Pay()
    {
        if (Status == FinancialEntryStatus.Paid) return;
        Status = FinancialEntryStatus.Paid;
        PaidAt = Now();
        UpdatedAt = PaidAt.Value;
        Version = Guid.NewGuid();
    }

    private static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return now.AddTicks(-(now.Ticks % 10));
    }
}
