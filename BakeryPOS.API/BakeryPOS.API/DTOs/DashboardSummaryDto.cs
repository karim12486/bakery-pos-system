namespace BakeryPOS.API.DTOs
{
    public class DashboardSummaryDto
    {
        // Card 1: Sales
        public decimal TodaysSales { get; set; }
        public int TodaysTransactions { get; set; }
        public decimal SalesChangePercentage { get; set; } // New
        

        // Card 3: Clients
        public int TotalClients { get; set; }
        public int NewClientsThisWeek { get; set; } // Renamed from 'NewCustomersThisWeek' to match controller
        public decimal ClientsChangePercentage { get; set; } // New

        // Card 4: Discounts
        public decimal DiscountsThisWeek { get; set; } // Changed to week based on requirements
        public int TotalDiscountTransactions { get; set; }
        public decimal DiscountsChangePercentage { get; set; } // New

        // Keep this if you had it before, otherwise remove it as the controller uses 'DiscountsThisWeek' now
        // public decimal DiscountsAppliedToday { get; set; } 
    }
}