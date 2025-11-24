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
                return BadRequest("La vente doit contenir au moins un article.");

            // 2. Get Current Cashier
            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cashier = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (cashier == null) return Unauthorized("Caissier introuvable.");

            // 3. Load Products
            var productIds = saleForCreateDto.SaleDetails.Select(d => d.ProductId).ToList();
            var productsFromDb = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            decimal totalAmount = 0;
            foreach (var item in saleForCreateDto.SaleDetails)
            {
                if (!productsFromDb.TryGetValue(item.ProductId, out var product))
                    return BadRequest($"Produit avec l'ID {item.ProductId} introuvable.");
                totalAmount += product.Price * item.Quantity;
            }

            // 4. Handle Customer & Discount
            decimal discountAmount = 0;
            Customer? customer = null;

            if (saleForCreateDto.CustomerId.HasValue)
            {
                customer = await _context.Customers.FindAsync(saleForCreateDto.CustomerId.Value);
                if (customer == null) return BadRequest($"Client introuvable.");

                if (customer.DiscountPercentage > 0)
                    discountAmount = totalAmount * (customer.DiscountPercentage / 100);
            }

            decimal finalAmount = totalAmount - discountAmount;

            // 5. Handle Payment Amounts (New Logic)
            decimal cashPaid = 0;
            decimal cardPaid = 0;
            decimal totalPaidNow = 0;
            decimal changeGiven = 0;

            switch (saleForCreateDto.PaymentMethod)
            {
                case PaymentType.Cash:
                    // Logic: If AmountPaid is provided (non-zero), use it as the tendered amount/deposit.
                    // If it's 0, assume they are paying the FULL amount (Standard Cashier behavior).
                    decimal cashTendered = saleForCreateDto.AmountPaid > 0 ? saleForCreateDto.AmountPaid : finalAmount;

                    if (cashTendered >= finalAmount)
                    {
                        // Full Payment (with potential change)
                        totalPaidNow = finalAmount;
                        cashPaid = finalAmount;
                        changeGiven = cashTendered - finalAmount;
                    }
                    else
                    {
                        // Partial Payment (Deposit)
                        totalPaidNow = cashTendered;
                        cashPaid = cashTendered;
                        changeGiven = 0;
                    }
                    break;

                case PaymentType.Card:
                    // Logic: If AmountPaid is provided, use it as deposit. Otherwise assume full.
                    decimal cardTendered = saleForCreateDto.AmountPaid > 0 ? saleForCreateDto.AmountPaid : finalAmount;

                    // Cap at final amount (Card can't give change usually)
                    if (cardTendered > finalAmount) cardTendered = finalAmount;

                    totalPaidNow = cardTendered;
                    cardPaid = cardTendered;
                    break;

                case PaymentType.Split:
                    if (!saleForCreateDto.SplitCashAmount.HasValue || !saleForCreateDto.SplitCardAmount.HasValue)
                        return BadRequest("Pour un paiement partagé, les montants Espèces et Carte sont requis.");

                    cashPaid = saleForCreateDto.SplitCashAmount.Value;
                    cardPaid = saleForCreateDto.SplitCardAmount.Value;
                    totalPaidNow = cashPaid + cardPaid;

                    // Check for overpayment in split (treat excess as change from cash portion)
                    if (totalPaidNow > finalAmount)
                    {
                        changeGiven = totalPaidNow - finalAmount;
                        totalPaidNow = finalAmount;
                        // Adjust cashPaid to reflect actual revenue, not tendered
                        cashPaid = cashPaid - changeGiven;
                    }
                    break;

                case PaymentType.Credit:
                    // Legacy support: Treats AmountPaid as Cash Deposit
                    cashPaid = saleForCreateDto.AmountPaid;
                    totalPaidNow = cashPaid;
                    break;
            }

            var amountOwed = finalAmount - totalPaidNow;

            // 6. Validate Debt/Credit Logic (The Critical Update)
            if (amountOwed > 0.001m) // Use small epsilon for float math safety
            {
                // If there is money owed, we MUST have a customer
                if (customer == null)
                {
                    return BadRequest($"Paiement incomplet. Il reste {amountOwed:C} à payer. Un client doit être sélectionné pour enregistrer la dette.");
                }

                // Update Customer Balance (Increase Debt)
                customer.CurrentBalance -= amountOwed;
            }

            // 7. Create Sale Entity
            var newSale = new Sale
            {
                UserId = cashier.Id,
                SaleDate = DateTime.UtcNow,
                TotalAmount = totalAmount,
                DiscountAmount = discountAmount,
                FinalAmount = finalAmount,

                PaymentMethod = saleForCreateDto.PaymentMethod,
                AmountPaid = totalPaidNow,
                AmountOwed = amountOwed,
                ChangeGiven = changeGiven,

                CustomerId = saleForCreateDto.CustomerId,

                // These track the actual revenue method
                CashPaid = cashPaid,
                CardPaid = cardPaid
            };

            await _context.Sales.AddAsync(newSale);

            // 8. Process Details & Inventory (No changes)
            foreach (var item in saleForCreateDto.SaleDetails)
            {
                var product = productsFromDb[item.ProductId];
                if (product.StockQuantity < item.Quantity)
                    return Conflict($"Stock insuffisant pour {product.Name}.");

                product.StockQuantity -= item.Quantity;
                var saleDetail = new SaleDetail { ProductId = product.Id, Quantity = item.Quantity, UnitPrice = product.Price, Sale = newSale };
                await _context.SaleDetails.AddAsync(saleDetail);
                var stockMovement = new StockMovement { ProductId = product.Id, UserId = cashier.Id, QuantityChange = -item.Quantity, Type = StockMovementType.Sale };
                await _context.StockMovements.AddAsync(stockMovement);
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Vente enregistrée avec succès.", saleId = newSale.Id, change = changeGiven });
        }
    }
}