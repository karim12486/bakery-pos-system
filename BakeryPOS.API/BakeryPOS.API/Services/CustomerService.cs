using AutoMapper;
using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.DTOs.Shared;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Services;

public interface ICustomerService
{
    Task<PagedResponse<CustomerDto>> ListAsync(PaginationParams pagination, string? search, CancellationToken ct);
    Task<CustomerDetailDto?> GetAsync(int id, CancellationToken ct);
    Task<PagedResponse<CustomerTransactionDto>> GetTransactionsAsync(int customerId, PaginationParams pagination, CancellationToken ct);
    Task<CustomerDto> CreateAsync(CustomerForCreateDto dto, CancellationToken ct);
    Task UpdateAsync(int id, CustomerForUpdateDto dto, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
    Task<decimal> RecordPaymentAsync(int customerId, CustomerPaymentDto dto, string username, CancellationToken ct);
}

public sealed class CustomerService : ICustomerService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public CustomerService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PagedResponse<CustomerDto>> ListAsync(PaginationParams pagination, string? search, CancellationToken ct)
    {
        var query = _context.Customers.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(search) || (c.PhoneNumber != null && c.PhoneNumber.Contains(search)));
        }

        var totalRecords = await query.CountAsync(ct);
        var customers = await query
            .OrderBy(c => c.Name)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

        return new PagedResponse<CustomerDto>(_mapper.Map<IEnumerable<CustomerDto>>(customers),
            pagination.PageNumber, pagination.PageSize, totalRecords);
    }

    public async Task<CustomerDetailDto?> GetAsync(int id, CancellationToken ct)
    {
        var customer = await _context.Customers
            .Include(c => c.Sales).ThenInclude(s => s.SaleDetails).ThenInclude(sd => sd.Product)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (customer == null) return null;

        var dto = _mapper.Map<CustomerDetailDto>(customer);

        dto.MonthlySpending = customer.Sales
            .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
            .Select(g => new CustomerMonthlySpendDto
            {
                Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM"),
                Amount = g.Sum(s => s.FinalAmount)
            })
            .ToList();

        dto.PaymentMethods = customer.Sales
            .GroupBy(s => s.PaymentMethod)
            .Select(g => new CustomerPaymentMethodDto { Method = g.Key.ToString(), Count = g.Count() })
            .ToList();

        dto.Transactions = customer.Sales
            .OrderByDescending(s => s.SaleDate)
            .Take(10)
            .Select(s => new CustomerTransactionDto
            {
                Date = s.SaleDate,
                Total = s.FinalAmount,
                Discount = s.DiscountAmount,
                Paid = s.AmountPaid,
                PaymentType = s.PaymentMethod.ToString(),
                ItemsSummary = string.Join(", ", s.SaleDetails.Select(sd => $"{sd.Quantity}x {sd.Product.Name}"))
            })
            .ToList();

        return dto;
    }

    public async Task<PagedResponse<CustomerTransactionDto>> GetTransactionsAsync(int customerId, PaginationParams pagination, CancellationToken ct)
    {
        if (!await _context.Customers.AnyAsync(c => c.Id == customerId, ct))
            throw new DomainNotFoundException("ERR_CUSTOMER_NOT_FOUND", "Client introuvable.");

        var query = _context.Sales
            .Include(s => s.SaleDetails).ThenInclude(sd => sd.Product)
            .Where(s => s.CustomerId == customerId);

        var totalRecords = await query.CountAsync(ct);
        var sales = await query
            .OrderByDescending(s => s.SaleDate)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

        var dtos = sales.Select(s => new CustomerTransactionDto
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

        return new PagedResponse<CustomerTransactionDto>(dtos, pagination.PageNumber, pagination.PageSize, totalRecords);
    }

    public async Task<CustomerDto> CreateAsync(CustomerForCreateDto dto, CancellationToken ct)
    {
        var newCustomer = _mapper.Map<Customer>(dto);
        await _context.Customers.AddAsync(newCustomer, ct);
        await _context.SaveChangesAsync(ct);
        return _mapper.Map<CustomerDto>(newCustomer);
    }

    public async Task UpdateAsync(int id, CustomerForUpdateDto dto, CancellationToken ct)
    {
        var customer = await _context.Customers.FindAsync(new object?[] { id }, ct)
            ?? throw new DomainNotFoundException("ERR_CUSTOMER_NOT_FOUND", "Client introuvable.");

        _mapper.Map(dto, customer);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var customer = await _context.Customers.FindAsync(new object?[] { id }, ct)
            ?? throw new DomainNotFoundException("ERR_CUSTOMER_NOT_FOUND", "Client introuvable.");

        if (await _context.Sales.AnyAsync(s => s.CustomerId == id, ct))
            throw new DomainConflictException("ERR_CUSTOMER_HAS_SALES",
                "Impossible de supprimer un client ayant un historique de ventes. Veuillez plutôt le désactiver.");

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<decimal> RecordPaymentAsync(int customerId, CustomerPaymentDto dto, string username, CancellationToken ct)
    {
        var customer = await _context.Customers.FindAsync(new object?[] { customerId }, ct)
            ?? throw new DomainNotFoundException("ERR_CUSTOMER_NOT_FOUND", "Client introuvable.");

        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username, ct)
            ?? throw new DomainException("ERR_CASHIER_NOT_FOUND", "Caissier introuvable.", StatusCodes.Status401Unauthorized);

        using var tx = await _context.Database.BeginTransactionAsync(ct);

        await _context.CustomerPayments.AddAsync(new CustomerPayment
        {
            CustomerId = customer.Id,
            UserId = user.Id,
            AmountPaid = dto.AmountPaid,
            Notes = dto.Notes
        }, ct);

        // Payment reduces the customer's debt (negative balance gets closer to 0).
        customer.CurrentBalance += dto.AmountPaid;

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return customer.CurrentBalance;
    }
}
