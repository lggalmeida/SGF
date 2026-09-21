namespace Sgf.Application.Finance;

public sealed record CreateFinancialEntryRequest(string? Type, string? Description, string? Category,
    decimal? Amount, DateOnly? DueDate, string? Notes);
public sealed record EditFinancialEntryRequest(string? Description, string? Category,
    decimal? Amount, DateOnly? DueDate, string? Notes);
public sealed record FinancialEntryResponse(Guid Id, string Type, string Description, string? Category,
    decimal Amount, DateOnly DueDate, DateTimeOffset? PaidAt, string Status, string? Notes,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record FinancePage(FinancialEntryResponse[] Items, int Page, int PageSize, int TotalCount);
public sealed record FinanceSummary(decimal Received, decimal Paid, decimal Receivable, decimal Payable, decimal Balance);
public sealed record FinanceFilter(int Page = 1, int PageSize = 20, string? Search = null,
    string? Type = null, string? Status = null, DateOnly? From = null, DateOnly? To = null, string? Category = null);
public enum FinanceError { Validation, NotFound, PaidEntry, Conflict }
public sealed record FinanceResult<T>(T? Value, FinanceError? Error = null, string[]? Details = null)
{
    public static FinanceResult<T> Fail(FinanceError error, params string[] details) => new(default, error, details);
}
public interface IFinanceService
{
    Task<FinanceResult<FinancePage>> ListAsync(FinanceFilter filter, CancellationToken ct);
    Task<FinanceResult<FinancialEntryResponse>> GetAsync(Guid id, CancellationToken ct);
    Task<FinanceResult<FinancialEntryResponse>> CreateAsync(CreateFinancialEntryRequest request, CancellationToken ct);
    Task<FinanceResult<FinancialEntryResponse>> UpdateAsync(Guid id, EditFinancialEntryRequest request, CancellationToken ct);
    Task<FinanceResult<FinancialEntryResponse>> PayAsync(Guid id, CancellationToken ct);
    Task<FinanceSummary> SummaryAsync(CancellationToken ct);
}
