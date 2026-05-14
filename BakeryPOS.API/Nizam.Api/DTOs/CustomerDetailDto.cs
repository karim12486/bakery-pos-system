namespace Nizam.Api.DTOs
{
    public class CustomerDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? PhoneNumber { get; set; }
        public decimal DiscountPercentage { get; set; }
        public bool IsActive { get; set; }
        public string? ImageUrl { get; set; } 
        public DateTime CreatedAt { get; set; } 

        // Stats
        public decimal TotalSpent { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal OutstandingBalance { get; set; }
        public bool HasPendingPayments { get; set; }
        public decimal CurrentBalance { get; set; }

        // --- NEW ANALYTICS DATA ---
        public List<CustomerMonthlySpendDto> MonthlySpending { get; set; } = new();
        public List<CustomerPaymentMethodDto> PaymentMethods { get; set; } = new();
        public List<CustomerTransactionDto> Transactions { get; set; } = new();
    }
}