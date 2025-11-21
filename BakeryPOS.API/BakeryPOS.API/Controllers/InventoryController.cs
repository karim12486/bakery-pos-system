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
    [Authorize] // All inventory actions require authentication
    public class InventoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InventoryController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/inventory/add
        [HasPermission(UserPermissions.ManageInventory)]
        [HttpPost("add")]
        public async Task<IActionResult> AddStock(StockAdditionDto stockAdditionDto)
        {
            // --- Find the product ---
            var product = await _context.Products.FindAsync(stockAdditionDto.ProductId);
            if (product == null)
            {
                return NotFound($"Product with ID {stockAdditionDto.ProductId} not found.");
            }

            // --- Get the current user ---
            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                return Unauthorized(); // Should not happen
            }

            // --- Update product stock ---
            product.StockQuantity += stockAdditionDto.QuantityToAdd;

            // --- Create a stock movement log ---
            var stockMovement = new StockMovement
            {
                ProductId = product.Id,
                UserId = user.Id,
                QuantityChange = stockAdditionDto.QuantityToAdd, // Positive for addition
                Type = StockMovementType.Addition
            };
            await _context.StockMovements.AddAsync(stockMovement);

            // --- Save all changes in a single transaction ---
            await _context.SaveChangesAsync();

            return Ok($"Stock for '{product.Name}' updated successfully. New quantity: {product.StockQuantity}");
        }

        // GET: api/inventory/history/{productId}
        [HasPermission(UserPermissions.ManageInventory)]
        [HttpGet("history/{productId}")]
        public async Task<ActionResult<IEnumerable<StockMovementDto>>> GetStockHistory(int productId)
        {
            var movements = await _context.StockMovements
                .Include(sm => sm.Product) // Eagerly load the related Product
                .Include(sm => sm.User)    // Eagerly load the related User
                .Where(sm => sm.ProductId == productId)
                .OrderByDescending(sm => sm.Timestamp) // Show the most recent first
                .ToListAsync();

            if (!movements.Any())
            {
                return NotFound($"No stock history found for product with ID {productId}.");
            }

            // Manually map to DTOs
            var movementDtos = movements.Select(sm => new StockMovementDto
            {
                Id = sm.Id,
                Timestamp = sm.Timestamp,
                QuantityChange = sm.QuantityChange,
                Type = sm.Type.ToString(), // Convert enum to string
                ProductName = sm.Product.Name,
                UserName = sm.User.FullName
            }).ToList();

            return Ok(movementDtos);
        }
    }
}