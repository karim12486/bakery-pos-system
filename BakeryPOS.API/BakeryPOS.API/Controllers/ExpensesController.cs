using AutoMapper;
using BakeryPOS.API.Core.Attributes;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.DTOs.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BakeryPOS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ExpensesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;

        public ExpensesController(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        // --- Expense Category Endpoints ---

        // GET: api/Expenses/categories
        [HasPermission(UserPermissions.ManageExpenses)]
        [HttpGet("categories")]
        public async Task<ActionResult<IEnumerable<ExpenseCategoryDto>>> GetCategories()
        {
            var categories = await _context.ExpenseCategories.OrderBy(c => c.Name).ToListAsync();
            return Ok(_mapper.Map<IEnumerable<ExpenseCategoryDto>>(categories));
        }

        // POST: api/Expenses/categories
        [HasPermission(UserPermissions.ManageExpenses)]
        [HttpPost("categories")]
        public async Task<ActionResult<ExpenseCategoryDto>> CreateCategory(ExpenseCategoryForCreateDto categoryDto)
        {
            if (await _context.ExpenseCategories.AnyAsync(c => c.Name.ToLower() == categoryDto.Name.ToLower()))
                return BadRequest("Ce nom de catégorie existe déjà.");

            var newCategory = _mapper.Map<ExpenseCategory>(categoryDto);
            await _context.ExpenseCategories.AddAsync(newCategory);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetCategories), _mapper.Map<ExpenseCategoryDto>(newCategory));
        }

        // --- Expense Endpoints ---

        // GET: api/Expenses
        [HasPermission(UserPermissions.ManageExpenses)]
        [HttpGet]
        public async Task<ActionResult<PagedResponse<ExpenseDto>>> GetExpenses(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? search,
            [FromQuery] PaginationParams pagination)
        {
            var query = _context.Expenses.AsQueryable();

            if (startDate.HasValue) query = query.Where(e => e.Date >= startDate.Value.Date);
            if (endDate.HasValue) query = query.Where(e => e.Date < endDate.Value.Date.AddDays(1));

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(e => e.Description.ToLower().Contains(search));
            }

            var totalRecords = await query.CountAsync();

            var expenses = await query
                .Include(e => e.Category)
                .Include(e => e.User)
                .OrderByDescending(e => e.Date) // Newest first
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            var dtos = _mapper.Map<IEnumerable<ExpenseDto>>(expenses);
            return Ok(new PagedResponse<ExpenseDto>(dtos, pagination.PageNumber, pagination.PageSize, totalRecords));
        }

        // POST: api/Expenses
        [HasPermission(UserPermissions.ManageExpenses)]
        [HttpPost]
        public async Task<ActionResult<ExpenseDto>> CreateExpense(ExpenseForCreateDto expenseDto)
        {
            var categoryExists = await _context.ExpenseCategories.AnyAsync(c => c.Id == expenseDto.CategoryId);
            if (!categoryExists)
            {
                return BadRequest($"La catégorie de dépenses portant l'identifiant {expenseDto.CategoryId} n'existe pas.");
            }

            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (user == null) return Unauthorized();

            var newExpense = _mapper.Map<Expense>(expenseDto);
            newExpense.UserId = user.Id;

            await _context.Expenses.AddAsync(newExpense);
            await _context.SaveChangesAsync();

            // Reload the expense with includes to map it back to a full DTO
            var expenseToReturn = await _context.Expenses
                .Include(e => e.Category)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == newExpense.Id);

            return Ok(_mapper.Map<ExpenseDto>(expenseToReturn));
        }

        // PUT: api/Expenses/5
        [HasPermission(UserPermissions.ManageExpenses)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExpense(int id, ExpenseForCreateDto expenseDto)
        {
            var expenseFromDb = await _context.Expenses.FindAsync(id);
            if (expenseFromDb == null) return NotFound();

            var categoryExists = await _context.ExpenseCategories.AnyAsync(c => c.Id == expenseDto.CategoryId);
            if (!categoryExists)
            {
                return BadRequest($"La catégorie de dépenses portant l'identifiant {expenseDto.CategoryId} n'existe pas.");
            }

            _mapper.Map(expenseDto, expenseFromDb);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Expenses/5
        [HasPermission(UserPermissions.ManageExpenses)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var expenseFromDb = await _context.Expenses.FindAsync(id);
            if (expenseFromDb == null) return NotFound();

            _context.Expenses.Remove(expenseFromDb);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PUT: api/expenses/categories/5
        [HttpPut("categories/{id}")]
        public async Task<IActionResult> UpdateCategory(int id, ExpenseCategoryForUpdateDto categoryDto)
        {
            var category = await _context.ExpenseCategories.FindAsync(id);
            if (category == null)
            {
                return NotFound("Catégorie introuvable."); // Category not found
            }

            // Optional: Check for duplicate name
            if (await _context.ExpenseCategories.AnyAsync(c => c.Id != id && c.Name.ToLower() == categoryDto.Name.ToLower()))
            {
                return BadRequest("Une catégorie avec ce nom existe déjà."); // Name already exists
            }

            _mapper.Map(categoryDto, category);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/expenses/categories/5
        [HttpDelete("categories/{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.ExpenseCategories.FindAsync(id);
            if (category == null)
            {
                return NotFound("Catégorie introuvable.");
            }

            // Safety Check: Prevent deleting a category if expenses are using it
            bool isUsed = await _context.Expenses.AnyAsync(e => e.CategoryId == id);
            if (isUsed)
            {
                return BadRequest("Impossible de supprimer cette catégorie car elle est liée à des dépenses existantes.");
                // "Cannot delete this category because it is linked to existing expenses."
            }

            _context.ExpenseCategories.Remove(category);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/expenses/summary
        // Returns total expenses and count for the current month
        [HttpGet("summary")]
        public async Task<ActionResult<ExpenseSummaryDto>> GetMonthlySummary()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfNextMonth = startOfMonth.AddMonths(1);

            // Base query for this month's expenses
            var query = _context.Expenses
                .Where(e => e.Date >= startOfMonth && e.Date < startOfNextMonth);

            // Execute aggregates in the database
            var totalAmount = await query.SumAsync(e => e.Amount);
            var count = await query.CountAsync();

            var summary = new ExpenseSummaryDto
            {
                TotalAmount = totalAmount,
                TransactionCount = count,
                Period = startOfMonth.ToString("MMMM yyyy") // e.g. "November 2025"
            };

            return Ok(summary);
        }
    }
}