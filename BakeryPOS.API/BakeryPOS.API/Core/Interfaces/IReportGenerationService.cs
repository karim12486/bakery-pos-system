using BakeryPOS.API.DTOs;

namespace BakeryPOS.API.Core.Interfaces
{
    public interface IReportGenerationService
    {
        Task<DailySalesReportDto> GenerateDailySalesReportAsync(DateTime date);
    }
}