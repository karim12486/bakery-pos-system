using Nizam.Api.DTOs;

namespace Nizam.Api.Core.Interfaces
{
    public interface IPdfGenerationService
    {
        byte[] GenerateDailySalesReport(DailySalesReportDto reportDto);
        byte[] GenerateMonthlySalesReport(MonthlySalesReportDto reportDto);
        byte[] GenerateSpecialProductReport(SpecialProductReportDto reportDto);
    }
}