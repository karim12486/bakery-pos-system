using AutoMapper;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
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
        public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers()
        {
            var customers = await _context.Customers
                .OrderBy(c => c.Name)
                .ToListAsync();

            var customerDtos = _mapper.Map<IEnumerable<CustomerDto>>(customers);
            return Ok(customerDtos);
        }

        // GET: api/customers/5
        // Gets details for a single customer.
        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerDto>> GetCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);

            if (customer == null)
            {
                return NotFound();
            }

            var customerDto = _mapper.Map<CustomerDto>(customer);
            return Ok(customerDto);
        }

        // POST: api/customers
        // Creates a new customer. Restricted to users with the ManageCustomers permission.
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
        [HttpPost("{id}/payments")]
        public async Task<IActionResult> RecordPayment(int id, CustomerPaymentDto paymentDto)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound("Customer not found.");
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

            return Ok(new { message = "Payment recorded successfully.", newBalance = customer.CurrentBalance });
        }
    }
}