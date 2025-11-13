namespace BakeryPOS.API.DTOs
{
    public class SpecialProductReportDto
    {
        public DateTime ReportDate { get; set; }
        public List<SpecialProductItemDto> ProductDetails { get; set; } = new List<SpecialProductItemDto>();

    }
}