using System.Security.Claims;
using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.DTOs.Shared;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customers;

    public CustomersController(ICustomerService customers)
    {
        _customers = customers;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<CustomerDto>>> GetCustomers(
        [FromQuery] PaginationParams pagination,
        [FromQuery] string? search,
        CancellationToken ct)
        => Ok(await _customers.ListAsync(pagination, search, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDetailDto>> GetCustomer(int id, CancellationToken ct)
    {
        var customer = await _customers.GetAsync(id, ct);
        return customer == null ? NotFound() : Ok(customer);
    }

    [HttpGet("{id:int}/transactions")]
    public async Task<ActionResult<PagedResponse<CustomerTransactionDto>>> GetCustomerTransactions(
        int id,
        [FromQuery] PaginationParams pagination,
        CancellationToken ct)
        => Ok(await _customers.GetTransactionsAsync(id, pagination, ct));

    [HttpPost]
    [HasPermission(UserPermissions.ManageCustomers)]
    public async Task<ActionResult<CustomerDto>> CreateCustomer(CustomerForCreateDto dto, CancellationToken ct)
    {
        var customer = await _customers.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
    }

    [HttpPost("{id:int}/payments")]
    [HasPermission(UserPermissions.ManageCustomers)]
    public async Task<IActionResult> RecordPayment(int id, CustomerPaymentDto dto, CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        var newBalance = await _customers.RecordPaymentAsync(id, dto, username, ct);
        return Ok(new { message = "Paiement enregistré avec succès.", newBalance });
    }

    [HttpPut("{id:int}")]
    [HasPermission(UserPermissions.ManageCustomers)]
    public async Task<IActionResult> UpdateCustomer(int id, CustomerForUpdateDto dto, CancellationToken ct)
    {
        await _customers.UpdateAsync(id, dto, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(UserPermissions.ManageCustomers)]
    public async Task<IActionResult> DeleteCustomer(int id, CancellationToken ct)
    {
        await _customers.DeleteAsync(id, ct);
        return NoContent();
    }
}
