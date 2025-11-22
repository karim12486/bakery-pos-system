using AutoMapper;
using BakeryPOS.API.Core.Attributes;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
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
        public async Task<ActionResult<IEnumerable<ExpenseDto>>> GetExpenses([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string? search)
        {
            var query = _context.Expenses.AsQueryable();

            if (startDate.HasValue) query = query.Where(e => e.Date >= startDate.Value.Date);
            if (endDate.HasValue) query = query.Where(e => e.Date < endDate.Value.Date.AddDays(1));

            // --- NEW SEARCH LOGIC ---
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(e => e.Description.ToLower().Contains(search));
            }

            var expenses = await query
                .Include(e => e.Category)
                .Include(e => e.User)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            return Ok(_mapper.Map<IEnumerable<ExpenseDto>>(expenses));
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
    }
}