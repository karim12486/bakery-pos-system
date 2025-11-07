using AutoMapper;
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
        private readonly IMapper _mapper;

        public SalesController(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        [HttpPost]
        public async Task<IActionResult> CreateSale(SaleForCreateDto saleForCreateDto)
        {
            // --- Steps 1 & 2: Get User and Products (no change) ---
            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cashier = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (cashier == null) return Unauthorized("Cashier not found.");

            var productIds = saleForCreateDto.SaleDetails.Select(d => d.ProductId).ToList();
            var productsFromDb = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            decimal totalAmount = 0;
            foreach (var item in saleForCreateDto.SaleDetails)
            {
                if (!productsFromDb.TryGetValue(item.ProductId, out var product))
                    return BadRequest($"Product with ID {item.ProductId} not found.");
                totalAmount += product.Price * item.Quantity;
            }

            // --- NEW Step 3: Calculate Discount ---
            decimal discountAmount = 0;
            Customer customer = null;
            if (saleForCreateDto.CustomerId.HasValue)
            {
                customer = await _context.Customers.FindAsync(saleForCreateDto.CustomerId.Value);
                if (customer == null)
                    return BadRequest($"Customer with ID {saleForCreateDto.CustomerId} not found.");

                if (customer.DiscountPercentage > 0)
                {
                    discountAmount = totalAmount * (customer.DiscountPercentage / 100);
                }
            }

            var finalAmount = totalAmount - discountAmount;

            // --- Step 4: Handle Payment and Credit Logic (uses finalAmount now) ---
            var amountOwed = finalAmount - saleForCreateDto.AmountPaid;

            if (saleForCreateDto.PaymentMethod == PaymentType.Credit)
            {
                if (customer == null)
                    return BadRequest("A customer must be selected for credit sales.");

                customer.CurrentBalance -= amountOwed;
            }
            else if (amountOwed > 0)
            {
                return BadRequest($"Full payment of {finalAmount:C} is required. Only {saleForCreateDto.AmountPaid:C} was provided.");
            }

            // --- Step 5: Create Entities and Update Stock (no change to this part's logic) ---
            var newSale = new Sale
            {
                UserId = cashier.Id,
                SaleDate = DateTime.UtcNow,
                TotalAmount = totalAmount, // The subtotal before discount
                DiscountAmount = discountAmount,
                FinalAmount = finalAmount,
                PaymentMethod = saleForCreateDto.PaymentMethod,
                AmountPaid = saleForCreateDto.AmountPaid,
                AmountOwed = amountOwed,
                CustomerId = saleForCreateDto.CustomerId
            };
            await _context.Sales.AddAsync(newSale);

            foreach (var item in saleForCreateDto.SaleDetails)
            {
                // ... (The logic to update stock and create SaleDetails is exactly the same as before)
                var product = productsFromDb[item.ProductId];
                if (product.StockQuantity < item.Quantity)
                    return Conflict($"Not enough stock for {product.Name}.");

                product.StockQuantity -= item.Quantity;
                var saleDetail = new SaleDetail { ProductId = product.Id, Quantity = item.Quantity, UnitPrice = product.Price, Sale = newSale };
                await _context.SaleDetails.AddAsync(saleDetail);
                var stockMovement = new StockMovement { ProductId = product.Id, UserId = cashier.Id, QuantityChange = -item.Quantity, Type = StockMovementType.Sale };
                await _context.StockMovements.AddAsync(stockMovement);
            }

            // --- Step 6: Save Changes (no change) ---
            await _context.SaveChangesAsync();

            return Ok("Sale created successfully.");
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SaleListDto>>> GetSales([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var query = _context.Sales.AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(s => s.SaleDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                var effectiveEndDate = endDate.Value;
                if (effectiveEndDate.TimeOfDay == TimeSpan.Zero)
                {
                    effectiveEndDate = effectiveEndDate.Date.AddDays(1);
                }

                query = query.Where(s => s.SaleDate < effectiveEndDate);
            }

            var sales = await query
                .Include(s => s.User)
                .Include(s => s.Customer)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            var salesToReturn = _mapper.Map<IEnumerable<SaleListDto>>(sales);

            return Ok(salesToReturn);
        }

        // GET: api/sales/5
        // Gets the full details for a single sale.
        [HttpGet("{id}")]
        public async Task<ActionResult<SaleDetailDto>> GetSale(int id)
        {
            // Query for the sale and include ALL related data needed for the DTO
            var sale = await _context.Sales
                .Include(s => s.User)
                .Include(s => s.Customer)
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.Product) // Include the Product for each SaleDetail
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
            {
                return NotFound();
            }

            // Use AutoMapper to map the complex entity to our detailed DTO
            var saleToReturn = _mapper.Map<SaleDetailDto>(sale);

            return Ok(saleToReturn);
        }
    }
}