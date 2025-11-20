namespace BakeryPOS.API.DTOs
{
    public class CustomerMonthlySpendDto
    {
        public string Month { get; set; } = string.Empty; // e.g., "Jan", "Feb"
        public decimal Amount { get; set; }
    }

    public class CustomerPaymentMethodDto
    {
        public string Method { get; set; } = string.Empty; // "Cash", "Credit"
        public int Count { get; set; }
    }

    public class CustomerTransactionDto
    {
        public int SaleId { get; set; }
        public DateTime Date { get; set; }
        public string ItemsSummary { get; set; } = string.Empty; // "3x Croissant, 1x Coffee"
        public decimal Total { get; set; }
        public decimal Discount { get; set; }
        public decimal Paid { get; set; }
        public decimal Change { get; set; } // If paid > total
        public string PaymentType { get; set; } = string.Empty;
    }
}