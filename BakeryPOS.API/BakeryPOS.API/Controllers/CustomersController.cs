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
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace BakeryPOS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // All customer actions require a logged-in user
    public class CustomersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;

        public CustomersController(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        // GET: api/customers
        // Gets a list of all customers. Accessible to all logged-in users.
        [HttpGet]
        public async Task<ActionResult<PagedResponse<CustomerDto>>> GetCustomers(
            [FromQuery] PaginationParams pagination,
            [FromQuery] string? search)
        {
            // Start query without the IsActive check
            var query = _context.Customers.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(search) || (c.PhoneNumber != null && c.PhoneNumber.Contains(search)));
            }

            var totalRecords = await query.CountAsync();

            var customers = await query
                .OrderBy(c => c.Name)
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            var customerDtos = _mapper.Map<IEnumerable<CustomerDto>>(customers);

            return Ok(new PagedResponse<CustomerDto>(customerDtos, pagination.PageNumber, pagination.PageSize, totalRecords));
        }

        // GET: api/customers/{id}/transactions
        // Dedicated endpoint for paginated transaction history
        [HttpGet("{id}/transactions")]
        public async Task<ActionResult<PagedResponse<CustomerTransactionDto>>> GetCustomerTransactions(
            int id,
            [FromQuery] PaginationParams pagination)
        {
            // Ensure customer exists
            var customerExists = await _context.Customers.AnyAsync(c => c.Id == id);
            if (!customerExists) return NotFound("Customer not found.");

            var query = _context.Sales
                .Include(s => s.SaleDetails).ThenInclude(sd => sd.Product)
                .Where(s => s.CustomerId == id);

            var totalRecords = await query.CountAsync();

            var sales = await query
                .OrderByDescending(s => s.SaleDate)
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            // Map manually to the Transaction DTO (same logic as before)
            var transactionDtos = sales.Select(s => new CustomerTransactionDto
            {
                SaleId = s.Id,
                Date = s.SaleDate,
                Total = s.FinalAmount,
                Discount = s.DiscountAmount,
                Paid = s.AmountPaid,
                Change = s.AmountPaid > s.FinalAmount ? s.AmountPaid - s.FinalAmount : 0,
                PaymentType = s.PaymentMethod.ToString(),
                ItemsSummary = string.Join(", ", s.SaleDetails.Select(sd => $"{sd.Quantity}x {sd.Product.Name}"))
            }).ToList();

            return Ok(new PagedResponse<CustomerTransactionDto>(transactionDtos, pagination.PageNumber, pagination.PageSize, totalRecords));
        }

        // GET: api/customers/5
        // Gets detailed information for a single customer, including calculated stats.
        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerDetailDto>> GetCustomer(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Sales).ThenInclude(s => s.SaleDetails).ThenInclude(sd => sd.Product)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null) return NotFound();

            var dto = _mapper.Map<CustomerDetailDto>(customer);

            // 1. Monthly Spending Trend (Last 12 months)
            dto.MonthlySpending = customer.Sales
                .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                .Select(g => new CustomerMonthlySpendDto
                {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM"),
                    Amount = g.Sum(s => s.FinalAmount)
                })
                .ToList();

            // 2. Payment Methods Pie Chart
            dto.PaymentMethods = customer.Sales
                .GroupBy(s => s.PaymentMethod)
                .Select(g => new CustomerPaymentMethodDto
                {
                    Method = g.Key.ToString(),
                    Count = g.Count()
                })
                .ToList();

            // 3. Transaction History
            dto.Transactions = customer.Sales
                .OrderByDescending(s => s.SaleDate)
                .Take(10) // Last 10 transactions
                .Select(s => new CustomerTransactionDto
                {
                    Date = s.SaleDate,
                    Total = s.FinalAmount,
                    Discount = s.DiscountAmount,
                    Paid = s.AmountPaid,
                    PaymentType = s.PaymentMethod.ToString(),
                    // Helper to format string "3x Prod A, 2x Prod B"
                    ItemsSummary = string.Join(", ", s.SaleDetails.Select(sd => $"{sd.Quantity}x {sd.Product.Name}"))
                })
                .ToList();

            return Ok(dto);
        }

        // POST: api/customers
        // Creates a new customer. Restricted to users with the ManageCustomers permission.
        [HasPermission(UserPermissions.ManageCustomers)]
        [HttpPost]
        public async Task<ActionResult<CustomerDto>> CreateCustomer(CustomerForCreateDto customerForCreateDto)
        {
            var newCustomer = _mapper.Map<Customer>(customerForCreateDto);

            await _context.Customers.AddAsync(newCustomer);
            await _context.SaveChangesAsync();

            var customerToReturn = _mapper.Map<CustomerDto>(newCustomer);

            return CreatedAtAction(nameof(GetCustomer), new { id = newCustomer.Id }, customerToReturn);
        }

        // POST: api/customers/{id}/payments
        // Records a new payment made by a customer.
        [HasPermission(UserPermissions.ManageCustomers)]
        [HttpPost("{id}/payments")]
        public async Task<IActionResult> RecordPayment(int id, CustomerPaymentDto paymentDto)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound("Client introuvable.");
            }

            // --- Get the current user who is recording the payment ---
            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                return Unauthorized(); // Should not happen
            }

            // --- Create the payment record ---
            var newPayment = new CustomerPayment
            {
                CustomerId = customer.Id,
                UserId = user.Id,
                AmountPaid = paymentDto.AmountPaid,
                Notes = paymentDto.Notes
            };
            await _context.CustomerPayments.AddAsync(newPayment);

            // --- Update the customer's balance ---
            // Adding a payment reduces their debt (moves the balance towards 0 or positive)
            customer.CurrentBalance += paymentDto.AmountPaid;

            // --- Save all changes ---
            await _context.SaveChangesAsync();

            return Ok(new { message = "Paiement enregistré avec succès.", newBalance = customer.CurrentBalance });
        }

        // PUT: api/customers/5
        // Updates an existing customer's details.
        [HasPermission(UserPermissions.ManageCustomers)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, CustomerForUpdateDto customerForUpdateDto)
        {
            var customerFromDb = await _context.Customers.FindAsync(id);

            if (customerFromDb == null)
            {
                return NotFound();
            }

            // Use AutoMapper to update the entity with the values from the DTO
            _mapper.Map(customerForUpdateDto, customerFromDb);

            await _context.SaveChangesAsync();

            return NoContent(); // Standard response for a successful update
        }

        // DELETE: api/customers/5
        // Deletes a customer.
        [HasPermission(UserPermissions.ManageCustomers)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customerFromDb = await _context.Customers.FindAsync(id);

            if (customerFromDb == null)
            {
                return NotFound();
            }

            // Hard Delete: Before deleting, check if the customer has any sales history.
            // If they do, a "hard delete" could corrupt historical data.
            // A "soft delete" (setting an IsActive flag) is often safer.
            // For now, we will proceed with a hard delete but add a check.

            var hasSales = await _context.Sales.AnyAsync(s => s.CustomerId == id);
            if (hasSales)
            {
                return BadRequest("Impossible de supprimer un client ayant un historique de ventes. Veuillez plutôt le désactiver.");
            }

            _context.Customers.Remove(customerFromDb);
            await _context.SaveChangesAsync();

            return NoContent(); // Standard response for a successful delete
        }
    }
}