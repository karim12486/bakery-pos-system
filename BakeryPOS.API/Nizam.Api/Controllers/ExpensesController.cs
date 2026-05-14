using System.Security.Claims;
using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.DTOs.Shared;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenses;

    public ExpensesController(IExpenseService expenses)
    {
        _expenses = expenses;
    }

    // --- Categories ---

    [HasPermission(UserPermissions.ManageExpenses)]
    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<ExpenseCategoryDto>>> GetCategories(CancellationToken ct)
        => Ok(await _expenses.ListCategoriesAsync(ct));

    [HasPermission(UserPermissions.ManageExpenses)]
    [HttpPost("categories")]
    public async Task<ActionResult<ExpenseCategoryDto>> CreateCategory(ExpenseCategoryForCreateDto dto, CancellationToken ct)
    {
        var cat = await _expenses.CreateCategoryAsync(dto, ct);
        return CreatedAtAction(nameof(GetCategories), cat);
    }

    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, ExpenseCategoryForUpdateDto dto, CancellationToken ct)
    {
        await _expenses.UpdateCategoryAsync(id, dto, ct);
        return NoContent();
    }

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        await _expenses.DeleteCategoryAsync(id, ct);
        return NoContent();
    }

    // --- Expenses ---

    [HasPermission(UserPermissions.ManageExpenses)]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ExpenseDto>>> GetExpenses(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? search,
        [FromQuery] PaginationParams pagination,
        CancellationToken ct)
        => Ok(await _expenses.ListAsync(startDate, endDate, search, pagination, ct));

    [HasPermission(UserPermissions.ManageExpenses)]
    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> CreateExpense(ExpenseForCreateDto dto, CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        return Ok(await _expenses.CreateAsync(dto, username, ct));
    }

    [HasPermission(UserPermissions.ManageExpenses)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateExpense(int id, ExpenseForCreateDto dto, CancellationToken ct)
    {
        await _expenses.UpdateAsync(id, dto, ct);
        return NoContent();
    }

    [HasPermission(UserPermissions.ManageExpenses)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteExpense(int id, CancellationToken ct)
    {
        await _expenses.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ExpenseSummaryDto>> GetMonthlySummary(CancellationToken ct)
        => Ok(await _expenses.GetMonthlySummaryAsync(ct));
}
