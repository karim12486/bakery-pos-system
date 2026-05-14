using Nizam.Api.DTOs;

namespace Nizam.Api.Core.Interfaces
{
    public interface IReportGenerationService
    {
        Task<DailySalesReportDto> GenerateDailySalesReportAsync(DateTime date);
        Task<SpecialProductReportDto> GenerateSpecialProductReportAsync(DateTime date); // <-- ADD THIS LINE
        Task<MonthlySalesReportDto> GenerateMonthlySalesReportAsync(int year, int month);
    }
}