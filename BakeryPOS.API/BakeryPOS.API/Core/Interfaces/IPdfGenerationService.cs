using BakeryPOS.API.DTOs;

namespace BakeryPOS.API.Core.Interfaces
{
    public interface IPdfGenerationService
    {
        byte[] GenerateDailySalesReport(DailySalesReportDto reportDto);
        byte[] GenerateMonthlySalesReport(MonthlySalesReportDto reportDto);
        byte[] GenerateSpecialProductReport(SpecialProductReportDto reportDto);
    }
}