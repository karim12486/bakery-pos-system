namespace BakeryPOS.API.Core.Entities
{
    public enum ReportType
    {
        DailySummary,
        MonthlySummary,
        ProductPerformance
    }

    public class Report
    {
        public int Id { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public ReportType Type { get; set; }

        // We will store the full report DTO here as a serialized JSON string.
        // This is very flexible, as different report types have different data structures.
        public string ReportDataJson { get; set; } = string.Empty;
    }
}