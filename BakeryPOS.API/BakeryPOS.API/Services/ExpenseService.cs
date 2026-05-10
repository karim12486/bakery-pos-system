using AutoMapper;
using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.DTOs.Shared;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Services;

public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(CancellationToken ct);
    Task<ExpenseCategoryDto> CreateCategoryAsync(ExpenseCategoryForCreateDto dto, CancellationToken ct);
    Task UpdateCategoryAsync(int id, ExpenseCategoryForUpdateDto dto, CancellationToken ct);
    Task DeleteCategoryAsync(int id, CancellationToken ct);

    Task<PagedResponse<ExpenseDto>> ListAsync(DateTime? startDate, DateTime? endDate, string? search, PaginationParams pagination, CancellationToken ct);
    Task<ExpenseDto> CreateAsync(ExpenseForCreateDto dto, string username, CancellationToken ct);
    Task UpdateAsync(int id, ExpenseForCreateDto dto, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
    Task<ExpenseSummaryDto> GetMonthlySummaryAsync(CancellationToken ct);
}

public sealed class ExpenseService : IExpenseService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public ExpenseService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(CancellationToken ct)
    {
        var categories = await _context.ExpenseCategories.OrderBy(c => c.Name).ToListAsync(ct);
        return _mapper.Map<List<ExpenseCategoryDto>>(categories);
    }

    public async Task<ExpenseCategoryDto> CreateCategoryAsync(ExpenseCategoryForCreateDto dto, CancellationToken ct)
    {
        if (await _context.ExpenseCategories.AnyAsync(c => c.Name.ToLower() == dto.Name.ToLower(), ct))
            throw new DomainConflictException("ERR_EXPENSE_CATEGORY_DUPLICATE", "Ce nom de catégorie existe déjà.");

        var category = _mapper.Map<ExpenseCategory>(dto);
        await _context.ExpenseCategories.AddAsync(category, ct);
        await _context.SaveChangesAsync(ct);
        return _mapper.Map<ExpenseCategoryDto>(category);
    }

    public async Task UpdateCategoryAsync(int id, ExpenseCategoryForUpdateDto dto, CancellationToken ct)
    {
        var category = await _context.ExpenseCategories.FindAsync(new object?[] { id }, ct)
            ?? throw new DomainNotFoundException("ERR_EXPENSE_CATEGORY_NOT_FOUND", "Catégorie introuvable.");

        if (await _context.ExpenseCategories.AnyAsync(c => c.Id != id && c.Name.ToLower() == dto.Name.ToLower(), ct))
            throw new DomainConflictException("ERR_EXPENSE_CATEGORY_DUPLICATE", "Une catégorie avec ce nom existe déjà.");

        _mapper.Map(dto, category);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct)
    {
        var category = await _context.ExpenseCategories.FindAsync(new object?[] { id }, ct)
            ?? throw new DomainNotFoundException("ERR_EXPENSE_CATEGORY_NOT_FOUND", "Catégorie introuvable.");

        if (await _context.Expenses.AnyAsync(e => e.CategoryId == id, ct))
            throw new DomainConflictException("ERR_EXPENSE_CATEGORY_IN_USE",
                "Impossible de supprimer cette catégorie car elle est liée à des dépenses existantes.");

        _context.ExpenseCategories.Remove(category);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<PagedResponse<ExpenseDto>> ListAsync(DateTime? startDate, DateTime? endDate, string? search, PaginationParams pagination, CancellationToken ct)
    {
        var query = _context.Expenses.AsQueryable();

        if (startDate.HasValue) query = query.Where(e => e.Date >= startDate.Value.Date);
        if (endDate.HasValue) query = query.Where(e => e.Date < endDate.Value.Date.AddDays(1));

        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            query = query.Where(e => e.Description.ToLower().Contains(search));
        }

        var totalRecords = await query.CountAsync(ct);
        var expenses = await query
            .Include(e => e.Category)
            .Include(e => e.User)
            .OrderByDescending(e => e.Date)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

        return new PagedResponse<ExpenseDto>(_mapper.Map<IEnumerable<ExpenseDto>>(expenses),
            pagination.PageNumber, pagination.PageSize, totalRecords);
    }

    public async Task<ExpenseDto> CreateAsync(ExpenseForCreateDto dto, string username, CancellationToken ct)
    {
        if (!await _context.ExpenseCategories.AnyAsync(c => c.Id == dto.CategoryId, ct))
            throw new DomainException("ERR_EXPENSE_CATEGORY_NOT_FOUND",
                $"La catégorie de dépenses portant l'identifiant {dto.CategoryId} n'existe pas.");

        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username, ct)
            ?? throw new DomainException("ERR_USER_NOT_FOUND", "Utilisateur introuvable.", StatusCodes.Status401Unauthorized);

        var expense = _mapper.Map<Expense>(dto);
        expense.UserId = user.Id;
        await _context.Expenses.AddAsync(expense, ct);
        await _context.SaveChangesAsync(ct);

        var reloaded = await _context.Expenses
            .Include(e => e.Category)
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == expense.Id, ct);

        return _mapper.Map<ExpenseDto>(reloaded);
    }

    public async Task UpdateAsync(int id, ExpenseForCreateDto dto, CancellationToken ct)
    {
        var expense = await _context.Expenses.FindAsync(new object?[] { id }, ct)
            ?? throw new DomainNotFoundException("ERR_EXPENSE_NOT_FOUND", "Dépense introuvable.");

        if (!await _context.ExpenseCategories.AnyAsync(c => c.Id == dto.CategoryId, ct))
            throw new DomainException("ERR_EXPENSE_CATEGORY_NOT_FOUND",
                $"La catégorie de dépenses portant l'identifiant {dto.CategoryId} n'existe pas.");

        _mapper.Map(dto, expense);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var expense = await _context.Expenses.FindAsync(new object?[] { id }, ct)
            ?? throw new DomainNotFoundException("ERR_EXPENSE_NOT_FOUND", "Dépense introuvable.");

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<ExpenseSummaryDto> GetMonthlySummaryAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfNextMonth = startOfMonth.AddMonths(1);

        var query = _context.Expenses.Where(e => e.Date >= startOfMonth && e.Date < startOfNextMonth);
        var totalAmount = await query.SumAsync(e => e.Amount, ct);
        var count = await query.CountAsync(ct);

        return new ExpenseSummaryDto
        {
            TotalAmount = totalAmount,
            TransactionCount = count,
            Period = startOfMonth.ToString("MMMM yyyy")
        };
    }
}
