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

        // --- PROPERTY CHANGED ---
        // We will now store the path to the generated PDF file on the server.
        public string PdfFilePath { get; set; } = string.Empty;
    }
}