namespace Nizam.Api.DTOs
{
    public class ReportListDto
    {
        public int Id { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}