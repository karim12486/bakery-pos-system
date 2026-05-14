using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;

        public CategoriesController(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        // GET: api/categories
        // Public endpoint to get all categories for filtering in the UI
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories()
        {
            var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            return Ok(_mapper.Map<IEnumerable<CategoryDto>>(categories));
        }

        // POST: api/categories
        // Admin-only endpoint to create a new category
        [HttpPost]
        [HasPermission(UserPermissions.ManageProducts)] // Simple authorization for now, can be restricted to admin later
        public async Task<ActionResult<CategoryDto>> CreateCategory(CategoryForCreateDto categoryDto)
        {
            if (await _context.Categories.AnyAsync(c => c.Name.ToLower() == categoryDto.Name.ToLower()))
            {
                return BadRequest("Ce nom de catégorie existe déjà.");
            }

            var newCategory = _mapper.Map<Category>(categoryDto);
            await _context.Categories.AddAsync(newCategory);
            await _context.SaveChangesAsync();

            return Ok(_mapper.Map<CategoryDto>(newCategory));
        }

        // PUT: api/categories/{id}
        [HttpPut("{id}")]
        [HasPermission(UserPermissions.ManageProducts)]
        public async Task<IActionResult> UpdateCategory(int id, CategoryForCreateDto dto)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();
            category.Name = dto.Name;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/categories/{id}
        [HttpDelete("{id}")]
        [HasPermission(UserPermissions.ManageProducts)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            // Refuse to leave orphaned products. Without this guard the SQL FK constraint throws
            // a raw DbUpdateException at SaveChanges and the caller sees a 500.
            if (await _context.Products.AnyAsync(p => p.CategoryId == id))
            {
                throw new DomainConflictException(
                    "ERR_CATEGORY_IN_USE",
                    "Impossible de supprimer cette catégorie car elle est liée à des produits existants. Veuillez d'abord déplacer ou supprimer ces produits.");
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}