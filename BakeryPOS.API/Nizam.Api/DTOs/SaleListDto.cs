namespace Nizam.Api.DTOs
{
    public class SaleListDto
    {
        public int Id { get; set; }
        public DateTime SaleDate { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public string? CustomerName { get; set; } // Nullable
        public decimal FinalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal AmountOwed { get; set; }
        public decimal CashPaid { get; set; }
        public decimal CardPaid { get; set; }
    }
}