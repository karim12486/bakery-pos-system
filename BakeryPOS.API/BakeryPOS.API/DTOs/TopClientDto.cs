namespace BakeryPOS.API.DTOs
{
    public class TopClientDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalSpent { get; set; }
        public int TotalOrders { get; set; }
        public decimal OutstandingBalance { get; set; } // The amount they currently owe
    }
}