using AutoMapper; // 1. ADD THIS USING STATEMENT
using BakeryPOS.API.Core.Attributes;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.DTOs.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper; // 2. ADD A PRIVATE FIELD FOR THE MAPPER

        // 3. INJECT IMAPPER INTO THE CONSTRUCTOR
        public ProductsController(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        // GET: api/products
        [HttpGet]
        public async Task<ActionResult<PagedResponse<ProductDto>>> GetProducts(
            [FromQuery] int? categoryId,
            [FromQuery] string? search,
            [FromQuery] PaginationParams pagination) // <-- Add this
        {
            var query = _context.Products.Include(p => p.Category).Where(p => p.IsActive);

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(search) || (p.Barcode != null && p.Barcode.Contains(search)));
            }

            // 1. Get Total Count BEFORE paging
            var totalRecords = await query.CountAsync();

            // 2. Apply Paging
            var products = await query
                .OrderBy(p => p.Name)
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            var productDtos = _mapper.Map<IEnumerable<ProductDto>>(products);

            // 3. Return Paged Response
            return Ok(new PagedResponse<ProductDto>(productDtos, pagination.PageNumber, pagination.PageSize, totalRecords));
        }

        // GET: api/products/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductDto>> GetProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            // 5. REPLACE MANUAL MAPPING WITH AUTOMAPPER
            var productDto = _mapper.Map<ProductDto>(product);

            return Ok(productDto);
        }

        // POST: api/products
        [HttpPost]
        [HasPermission(UserPermissions.ManageProducts)]
        public async Task<ActionResult<ProductDto>> CreateProduct(ProductForCreateDto productForCreateDto)
        {
            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == productForCreateDto.CategoryId);
            if (!categoryExists)
            {
                return BadRequest($"Category with ID {productForCreateDto.CategoryId} does not exist.");
            }
            // 6. USE AUTOMAPPER TO MAP FROM DTO TO ENTITY
            var newProduct = _mapper.Map<Product>(productForCreateDto);

            // Set properties not included in the DTO
            newProduct.IsActive = true;
            newProduct.CreatedAt = DateTime.UtcNow;

            await _context.Products.AddAsync(newProduct);
            await _context.SaveChangesAsync();

            var productToReturn = _mapper.Map<ProductDto>(newProduct);

            return CreatedAtAction(nameof(GetProduct), new { id = newProduct.Id }, productToReturn);
        }

        // PUT: api/products/5
        [HttpPut("{id}")]
        [HasPermission(UserPermissions.ManageProducts)]
        public async Task<IActionResult> UpdateProduct(int id, ProductForUpdateDto productForUpdateDto)
        {
            var productFromDb = await _context.Products.FindAsync(id);

            if (productFromDb == null)
            {
                return NotFound();
            }

            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == productForUpdateDto.CategoryId);
            if (!categoryExists)
            {
                return BadRequest($"Category with ID {productForUpdateDto.CategoryId} does not exist.");
            }

            // 7. USE AUTOMAPPER TO MAP UPDATES FROM DTO TO THE EXISTING ENTITY
            _mapper.Map(productForUpdateDto, productFromDb);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/products/5
        // "Deletes" a product. This endpoint is protected.
        [HttpDelete("{id}")]
        [HasPermission(UserPermissions.ManageProducts)] // Only authenticated users can delete products
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var productFromDb = await _context.Products.FindAsync(id);

            if (productFromDb == null)
            {
                return NotFound();
            }

            // This is a "soft delete". We don't remove the product from the database,
            // we just mark it as inactive. This is crucial for preserving historical
            // sales data that might be linked to this product.
            productFromDb.IsActive = false;

            await _context.SaveChangesAsync();

            return NoContent(); // Returns a 204 No Content response
        }

        // GET: api/products/barcode/123456
        [HttpGet("barcode/{barcode}")]
        public async Task<ActionResult<ProductDto>> GetProductByBarcode(string barcode)
        {
            // Find the product by barcode (Case insensitive is usually safer)
            var product = await _context.Products
                .Include(p => p.Category) // Important: Include Category so the DTO has CategoryName
                .FirstOrDefaultAsync(p => p.IsActive && p.Barcode == barcode);

            if (product == null)
            {
                return NotFound(new { message = "Produit introuvable" });
            }

            var productDto = _mapper.Map<ProductDto>(product);
            return Ok(productDto);
        }
    }
}
