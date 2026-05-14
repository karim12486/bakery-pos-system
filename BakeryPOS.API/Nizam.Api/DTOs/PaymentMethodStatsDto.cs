namespace Nizam.Api.DTOs
{
    public class PaymentMethodStatsDto
    {
        public string MethodName { get; set; } = string.Empty; // "Cash", "Card"
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}