using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Sgf.Application.Finance;
using Sgf.Application.Identity;
using Sgf.Domain.Finance;
using Sgf.Infrastructure.Database;
using Sgf.Infrastructure.Database.MultiTenancy;

namespace Sgf.Infrastructure.Finance;

public sealed class FinanceService(SgfDbContext db, ICurrentTenantContext tenant) : IFinanceService
{
    private static readonly Expression<Func<FinancialEntry, FinancialEntryResponse>> Projection = e =>
        new(e.Id, e.Type.ToString(), e.Description, e.Category, e.Amount, e.DueDate,
            e.PaidAt, e.Status.ToString(), e.Notes, e.CreatedAt, e.UpdatedAt);

    private async Task RequireTenant(CancellationToken ct)
    {
        if (await tenant.GetCurrentAsync(ct) is null) throw new TenantContextUnavailableException();
    }

    public async Task<FinanceResult<FinancePage>> ListAsync(FinanceFilter f, CancellationToken ct)
    {
        await RequireTenant(ct);
        if (f.Page is < 1 or > 1000000 || f.PageSize is < 1 or > 100 || f.Search?.Length > 200 ||
            f.Category?.Length > 100 || (f.From.HasValue && f.To.HasValue && f.From > f.To) ||
            (f.Type is not null && f.Type is not ("Income" or "Expense")) ||
            (f.Status is not null && f.Status is not ("Pending" or "Paid")))
            return FinanceResult<FinancePage>.Fail(FinanceError.Validation);
        var query = db.FinancialEntries.AsNoTracking();
        if (f.Type is not null) { var type = Enum.Parse<FinancialEntryType>(f.Type); query = query.Where(e => e.Type == type); }
        if (f.Status is not null) { var status = Enum.Parse<FinancialEntryStatus>(f.Status); query = query.Where(e => e.Status == status); }
        if (f.From.HasValue) query = query.Where(e => e.DueDate >= f.From.Value);
        if (f.To.HasValue) query = query.Where(e => e.DueDate <= f.To.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToUpperInvariant();
            query = query.Where(e => e.Description.ToUpper().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(f.Category))
        {
            var category = f.Category.Trim().ToUpperInvariant();
            query = query.Where(e => e.Category != null && e.Category.ToUpper() == category);
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(e => e.DueDate).ThenByDescending(e => e.CreatedAt).ThenBy(e => e.Id)
            .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).Select(Projection).ToArrayAsync(ct);
        return new(new FinancePage(items, f.Page, f.PageSize, total));
    }

    public async Task<FinanceResult<FinancialEntryResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        await RequireTenant(ct);
        var entry = await db.FinancialEntries.AsNoTracking().Where(e => e.Id == id).Select(Projection).SingleOrDefaultAsync(ct);
        return entry is null ? FinanceResult<FinancialEntryResponse>.Fail(FinanceError.NotFound) : new(entry);
    }

    public async Task<FinanceResult<FinancialEntryResponse>> CreateAsync(CreateFinancialEntryRequest r, CancellationToken ct)
    {
        await RequireTenant(ct);
        var errors = FinancialEntry.Validate(r.Description, r.Category, r.Amount, r.DueDate, r.Notes);
        if (r.Type is not ("Income" or "Expense") || errors.Length > 0)
            return FinanceResult<FinancialEntryResponse>.Fail(FinanceError.Validation, errors);
        var entry = new FinancialEntry(Enum.Parse<FinancialEntryType>(r.Type), r.Description!, r.Category, r.Amount!.Value, r.DueDate!.Value, r.Notes);
        db.FinancialEntries.Add(entry);
        return await Save(entry, ct);
    }

    public async Task<FinanceResult<FinancialEntryResponse>> UpdateAsync(Guid id, EditFinancialEntryRequest r, CancellationToken ct)
    {
        await RequireTenant(ct);
        var entry = await db.FinancialEntries.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null) return FinanceResult<FinancialEntryResponse>.Fail(FinanceError.NotFound);
        if (entry.Status == FinancialEntryStatus.Paid) return FinanceResult<FinancialEntryResponse>.Fail(FinanceError.PaidEntry);
        var errors = FinancialEntry.Validate(r.Description, r.Category, r.Amount, r.DueDate, r.Notes);
        if (errors.Length > 0) return FinanceResult<FinancialEntryResponse>.Fail(FinanceError.Validation, errors);
        entry.Update(r.Description!, r.Category, r.Amount!.Value, r.DueDate!.Value, r.Notes);
        return await Save(entry, ct);
    }

    public async Task<FinanceResult<FinancialEntryResponse>> PayAsync(Guid id, CancellationToken ct)
    {
        await RequireTenant(ct);
        var entry = await db.FinancialEntries.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null) return FinanceResult<FinancialEntryResponse>.Fail(FinanceError.NotFound);
        entry.Pay();
        var result = await Save(entry, ct);
        if (result.Error != FinanceError.Conflict) return result;
        // Another payment may have won. Return that committed state, never pay twice.
        var current = await GetAsync(id, ct);
        return current.Value?.Status == "Paid" ? current : result;
    }

    public async Task<FinanceSummary> SummaryAsync(CancellationToken ct)
    {
        await RequireTenant(ct);
        // One SQL statement keeps all five totals on the same database snapshot.
        return await db.FinancialEntries.AsNoTracking().GroupBy(e => 1).Select(g => new FinanceSummary(
            g.Sum(e => e.Type == FinancialEntryType.Income && e.Status == FinancialEntryStatus.Paid ? e.Amount : 0),
            g.Sum(e => e.Type == FinancialEntryType.Expense && e.Status == FinancialEntryStatus.Paid ? e.Amount : 0),
            g.Sum(e => e.Type == FinancialEntryType.Income && e.Status == FinancialEntryStatus.Pending ? e.Amount : 0),
            g.Sum(e => e.Type == FinancialEntryType.Expense && e.Status == FinancialEntryStatus.Pending ? e.Amount : 0),
            g.Sum(e => e.Status == FinancialEntryStatus.Paid ? (e.Type == FinancialEntryType.Income ? e.Amount : -e.Amount) : 0)
        )).SingleOrDefaultAsync(ct) ?? new FinanceSummary(0, 0, 0, 0, 0);
    }

    private async Task<FinanceResult<FinancialEntryResponse>> Save(FinancialEntry e, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return new(new FinancialEntryResponse(e.Id, e.Type.ToString(), e.Description, e.Category, e.Amount,
                e.DueDate, e.PaidAt, e.Status.ToString(), e.Notes, e.CreatedAt, e.UpdatedAt));
        }
        catch (DbUpdateConcurrencyException) { return FinanceResult<FinancialEntryResponse>.Fail(FinanceError.Conflict); }
    }
}
