using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.DTOs.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BakeryPOS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // All actions in this controller require the user to be logged in
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IReportGenerationService _reportService; // Add this
        private readonly IPdfGenerationService _pdfService;

        public ReportsController(AppDbContext context,
                                 IReportGenerationService reportService,
                                 IPdfGenerationService pdfService)
        {
            _context = context;
            _reportService = reportService;
            _pdfService = pdfService;
        }

        // GET: api/reports
        // Gets a list of all previously generated reports.
        // GET: api/reports?pageNumber=1&pageSize=10&type=DailySummary&date=2025-11-22
        [HttpGet]
        public async Task<ActionResult<PagedResponse<ReportListDto>>> GetReports(
            [FromQuery] PaginationParams pagination,
            [FromQuery] ReportType? type,  // Filter by Report Type (optional)
            [FromQuery] DateTime? date)    // Filter by specific Date (optional)
        {
            var query = _context.Reports.AsQueryable();

            // 1. Apply Type Filter
            if (type.HasValue)
            {
                query = query.Where(r => r.Type == type.Value);
            }

            // 2. Apply Date Filter (Compare Date part only, ignoring time)
            if (date.HasValue)
            {
                query = query.Where(r => r.GeneratedAt.Date == date.Value.Date);
            }

            // 3. Get Total Count (after filters)
            var totalRecords = await query.CountAsync();

            // 4. Apply Sorting and Paging
            var reports = await query
                .OrderByDescending(r => r.GeneratedAt) // Newest first
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .Select(r => new ReportListDto
                {
                    Id = r.Id,
                    GeneratedAt = r.GeneratedAt,
                    Type = r.Type.ToString()
                })
                .ToListAsync();

            return Ok(new PagedResponse<ReportListDto>(reports, pagination.PageNumber, pagination.PageSize, totalRecords));
        }

        // GET: api/reports/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetReport(int id)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report == null) return NotFound();
            // Return a placeholder message or the path, as the JSON data is no longer stored
            return Ok(new { message = "Report file exists.", path = report.PdfFilePath });
        }


        // --- Now, let's rewrite the test endpoint to use the injected services ---
        [HttpGet("test/current-month-pdf")]
        public async Task<IActionResult> GetCurrentMonthReportAsPdf()
        {
            // 1. Generate the report data for the current month
            var now = DateTime.UtcNow;
            var monthlyReportDto = await _reportService.GenerateMonthlySalesReportAsync(now.Year, now.Month);

            // 2. Generate the PDF from the data
            var pdfBytes = _pdfService.GenerateMonthlySalesReport(monthlyReportDto);

            // 3. Return the PDF as a downloadable file
            string fileName = $"Monthly_Report_{now:yyyy-MM}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        // GET: api/reports/{id}/download
        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadReport(int id)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report == null) return NotFound("Report not found.");

            if (string.IsNullOrEmpty(report.PdfFilePath) || !System.IO.File.Exists(report.PdfFilePath))
            {
                return NotFound("The report PDF file could not be found on the server.");
            }

            var pdfBytes = await System.IO.File.ReadAllBytesAsync(report.PdfFilePath);
            var fileName = Path.GetFileName(report.PdfFilePath);

            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}