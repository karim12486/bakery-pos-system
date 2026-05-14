namespace Nizam.Api.DTOs
{
    public class FinancialStatsDto
    {
        public string Label { get; set; } = string.Empty; // e.g. "Jan", "Week 1"
        public decimal Revenue { get; set; }
        public decimal Expenses { get; set; }
        public decimal Profit => Revenue - Expenses; // Optional helper
    }
}