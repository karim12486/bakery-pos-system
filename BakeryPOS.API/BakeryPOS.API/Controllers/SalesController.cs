using AutoMapper;
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
    [Authorize] // Or [HasPermission(UserPermissions.ProcessSales)] if using custom attributes
    public class SalesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;

        public SalesController(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        // GET: api/sales
        // Gets a paginated list of sales, with optional filtering by date.
        [HttpGet]
        public async Task<ActionResult<PagedResponse<SaleListDto>>> GetSales(
            [FromQuery] PaginationParams pagination,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
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

            var totalRecords = await query.CountAsync();

            var sales = await query
                .Include(s => s.User)
                .Include(s => s.Customer)
                .OrderByDescending(s => s.SaleDate) // Newest first
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            var salesToReturn = _mapper.Map<IEnumerable<SaleListDto>>(sales);

            return Ok(new PagedResponse<SaleListDto>(salesToReturn, pagination.PageNumber, pagination.PageSize, totalRecords));
        }

        // GET: api/sales/5
        // Gets the full details for a single sale.
        [HttpGet("{id}")]
        public async Task<ActionResult<SaleDetailDto>> GetSale(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.User)
                .Include(s => s.Customer)
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.Product)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
            {
                return NotFound();
            }

            var saleToReturn = _mapper.Map<SaleDetailDto>(sale);
            return Ok(saleToReturn);
        }

        // POST: api/sales
        // Process a new sale transaction
        [HttpPost]
        public async Task<IActionResult> CreateSale(SaleForCreateDto saleForCreateDto)
        {
            // 1. Validate Request
            if (saleForCreateDto.SaleDetails == null || !saleForCreateDto.SaleDetails.Any())
            {
                return BadRequest("La vente doit contenir au moins un article.");
            }

            // 2. Get Current Cashier
            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cashier = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (cashier == null)
            {
                return Unauthorized("Caissier introuvable.");
            }

            // 3. Load Products & Calculate Base Total
            var productIds = saleForCreateDto.SaleDetails.Select(d => d.ProductId).ToList();
            var productsFromDb = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            decimal totalAmount = 0; // Subtotal before discount
            foreach (var item in saleForCreateDto.SaleDetails)
            {
                if (!productsFromDb.TryGetValue(item.ProductId, out var product))
                {
                    return BadRequest($"Produit avec l'ID {item.ProductId} introuvable.");
                }
                totalAmount += product.Price * item.Quantity;
            }

            // 4. Handle Customer & Discount
            decimal discountAmount = 0;
            Customer? customer = null;

            if (saleForCreateDto.CustomerId.HasValue)
            {
                customer = await _context.Customers.FindAsync(saleForCreateDto.CustomerId.Value);
                if (customer == null)
                {
                    return BadRequest($"Client introuvable.");
                }

                // Apply discount if customer has one
                if (customer.DiscountPercentage > 0)
                {
                    discountAmount = totalAmount * (customer.DiscountPercentage / 100);
                }
            }

            decimal finalAmount = totalAmount - discountAmount;

            // 5. Handle Payment Types & Amounts
            decimal cashPaid = 0;
            decimal cardPaid = 0;
            decimal totalPaidNow = 0;

            switch (saleForCreateDto.PaymentMethod)
            {
                case PaymentType.Cash:
                    cashPaid = finalAmount;
                    totalPaidNow = finalAmount;
                    break;

                case PaymentType.Card:
                    cardPaid = finalAmount;
                    totalPaidNow = finalAmount;
                    break;

                case PaymentType.Split:
                    if (!saleForCreateDto.SplitCashAmount.HasValue || !saleForCreateDto.SplitCardAmount.HasValue)
                        return BadRequest("Pour un paiement partagé, les montants Espèces et Carte sont requis.");

                    cashPaid = saleForCreateDto.SplitCashAmount.Value;
                    cardPaid = saleForCreateDto.SplitCardAmount.Value;
                    totalPaidNow = cashPaid + cardPaid;

                    // Allow small floating point differences, but generally check equality
                    if (totalPaidNow < finalAmount)
                        return BadRequest($"Paiement insuffisant. Total requis : {finalAmount:C}, Reçu : {totalPaidNow:C}");
                    break;

                case PaymentType.Credit:
                    // For Credit transactions, 'AmountPaid' acts as the cash deposit
                    cashPaid = saleForCreateDto.AmountPaid;
                    totalPaidNow = cashPaid;
                    break;
            }

            var amountOwed = finalAmount - totalPaidNow;

            // 6. Validate Credit Logic
            if (saleForCreateDto.PaymentMethod == PaymentType.Credit)
            {
                if (customer == null)
                    return BadRequest("Un client doit être sélectionné pour la vente à crédit.");

                // Update Customer Balance (Debt increases)
                customer.CurrentBalance -= amountOwed;
            }
            else if (amountOwed > 0)
            {
                // For non-credit sales, full payment is required
                return BadRequest($"Le paiement complet de {finalAmount:C} est requis. Seulement {totalPaidNow:C} a été fourni.");
            }

            // 7. Create Sale Entity
            var newSale = new Sale
            {
                UserId = cashier.Id,
                SaleDate = DateTime.UtcNow,
                TotalAmount = totalAmount,      // Original Price
                DiscountAmount = discountAmount,// Discount Value
                FinalAmount = finalAmount,      // Price after Discount

                PaymentMethod = saleForCreateDto.PaymentMethod,
                AmountPaid = totalPaidNow,
                AmountOwed = amountOwed,

                CustomerId = saleForCreateDto.CustomerId,

                // Track breakdown for reports
                CashPaid = cashPaid,
                CardPaid = cardPaid
            };

            await _context.Sales.AddAsync(newSale);

            // 8. Process Details & Update Inventory
            foreach (var item in saleForCreateDto.SaleDetails)
            {
                var product = productsFromDb[item.ProductId];

                // Validation: Check Stock
                if (product.StockQuantity < item.Quantity)
                {
                    return Conflict($"Stock insuffisant pour {product.Name}. Disponible : {product.StockQuantity}, Demandé : {item.Quantity}.");
                }

                // Decrease Stock
                product.StockQuantity -= item.Quantity;

                // Create Detail Record
                var saleDetail = new SaleDetail
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price,
                    Sale = newSale
                };
                await _context.SaleDetails.AddAsync(saleDetail);

                // Create Stock Movement Log
                var stockMovement = new StockMovement
                {
                    ProductId = product.Id,
                    UserId = cashier.Id,
                    QuantityChange = -item.Quantity, // Negative for sale
                    Type = StockMovementType.Sale
                };
                await _context.StockMovements.AddAsync(stockMovement);
            }

            // 9. Commit Transaction
            await _context.SaveChangesAsync();

            return Ok(new { message = "Vente enregistrée avec succès.", saleId = newSale.Id });
        }
    }
}