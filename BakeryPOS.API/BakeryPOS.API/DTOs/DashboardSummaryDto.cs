namespace BakeryPOS.API.DTOs
{
    public class DashboardSummaryDto
    {
        public decimal TodaysSales { get; set; }
        public int TodaysTransactions { get; set; }
        public int NewCustomersThisWeek { get; set; }
        public decimal DiscountsAppliedToday { get; set; }
    }
}