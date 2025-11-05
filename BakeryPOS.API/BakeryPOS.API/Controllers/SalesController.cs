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
    [Authorize] // All actions in this controller require the user to be logged in
    public class SalesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SalesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateSale(SaleForCreateDto saleForCreateDto)
        {
            // --- Step 1: Validate the incoming data ---
            if (saleForCreateDto.SaleDetails == null || !saleForCreateDto.SaleDetails.Any())
            {
                return BadRequest("Sale must contain at least one item.");
            }

            // --- Step 2: Get the logged-in user (cashier) ---
            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cashier = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (cashier == null)
            {
                return Unauthorized("Cashier not found.");
            }

            // --- Step 3: Fetch products and calculate total amount ---
            var productIds = saleForCreateDto.SaleDetails.Select(d => d.ProductId).ToList();
            var productsFromDb = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id); // Use ToDictionary for efficient lookup

            decimal totalAmount = 0;
            foreach (var item in saleForCreateDto.SaleDetails)
            {
                if (!productsFromDb.TryGetValue(item.ProductId, out var product))
                {
                    return BadRequest($"Product with ID {item.ProductId} not found.");
                }
                totalAmount += product.Price * item.Quantity;
            }

            // --- Step 4: Handle Payment and Customer Credit Logic ---
            var amountOwed = totalAmount - saleForCreateDto.AmountPaid;

            if (saleForCreateDto.PaymentMethod == PaymentType.Credit)
            {
                if (saleForCreateDto.CustomerId == null)
                {
                    return BadRequest("A customer must be selected for credit sales.");
                }

                var customer = await _context.Customers.FindAsync(saleForCreateDto.CustomerId);
                if (customer == null)
                {
                    return BadRequest($"Customer with ID {saleForCreateDto.CustomerId} not found.");
                }

                // For credit sales, update the customer's balance. A negative balance means they owe money.
                customer.CurrentBalance -= amountOwed;
            }
            else // For Cash or Card sales
            {
                if (amountOwed > 0)
                {
                    return BadRequest($"Full payment of {totalAmount:C} is required for Cash or Card sales. Only {saleForCreateDto.AmountPaid:C} was provided.");
                }
            }

            // --- Step 5: Create Sale and SaleDetail entities and update stock ---
            var newSale = new Sale
            {
                UserId = cashier.Id,
                SaleDate = DateTime.UtcNow,
                TotalAmount = totalAmount,
                PaymentMethod = saleForCreateDto.PaymentMethod,
                AmountPaid = saleForCreateDto.AmountPaid,
                AmountOwed = amountOwed,
                CustomerId = saleForCreateDto.CustomerId
            };

            await _context.Sales.AddAsync(newSale);

            // Process each item, create SaleDetails, and update stock
            foreach (var item in saleForCreateDto.SaleDetails)
            {
                var product = productsFromDb[item.ProductId];

                if (product.StockQuantity < item.Quantity)
                {
                    // This is a "just-in-case" check. For high-concurrency systems, you'd add more robust locking.
                    return Conflict($"Not enough stock for {product.Name}. Available: {product.StockQuantity}, Requested: {item.Quantity}.");
                }

                product.StockQuantity -= item.Quantity;

                var saleDetail = new SaleDetail
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price,
                    Sale = newSale // Link to the main sale object
                };
                await _context.SaleDetails.AddAsync(saleDetail);

                // Create a log for the stock movement
                var stockMovement = new StockMovement
                {
                    ProductId = product.Id,
                    UserId = cashier.Id,
                    QuantityChange = -item.Quantity,
                    Type = StockMovementType.Sale
                };
                await _context.StockMovements.AddAsync(stockMovement);
            }

            // --- Step 6: Save all changes in one atomic transaction ---
            await _context.SaveChangesAsync();

            return Ok("Sale created successfully.");
        }
    }
}