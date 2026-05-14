namespace Nizam.Api.DTOs
{
    public class SaleDetailDto
    {
        public int Id { get; set; }
        public DateTime SaleDate { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public string? CustomerName { get; set; }

        public decimal TotalAmount { get; set; } // Subtotal
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }

        public string PaymentMethod { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public decimal AmountOwed { get; set; }
        public decimal CashPaid { get; set; }
        public decimal CardPaid { get; set; }

        public List<SaleItemDto> Items { get; set; } = new List<SaleItemDto>();
    }
}