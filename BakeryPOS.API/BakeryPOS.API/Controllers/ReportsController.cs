using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using BakeryPOS.API.Core.Interfaces;

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
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReportListDto>>> GetReports()
        {
            var reports = await _context.Reports
                .OrderByDescending(r => r.GeneratedAt)
                .Select(r => new ReportListDto
                {
                    Id = r.Id,
                    GeneratedAt = r.GeneratedAt,
                    Type = r.Type.ToString()
                })
                .ToListAsync();

            return Ok(reports);
        }

        // GET: api/reports/5
        // Gets the full data for a single report by its ID.
        [HttpGet("{id}")]
        public async Task<IActionResult> GetReport(int id)
        {
            var report = await _context.Reports.FindAsync(id);

            if (report == null)
            {
                return NotFound();
            }

            // Since the data is stored as a JSON string, we return it as a raw JSON object.
            // This is very flexible for the frontend.
            //var reportData = JsonSerializer.Deserialize<object>(report.ReportDataJson);

            //return Ok(reportData);
            return Ok($"This will eventually download the file at: {report.PdfFilePath}");
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
        // Downloads the PDF file for a specific report.
        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadReport(int id)
        {
            // 1. Find the report record in the database
            var report = await _context.Reports.FindAsync(id);
            if (report == null)
            {
                return NotFound("Report not found.");
            }

            // 2. Check if the file actually exists on the server
            if (string.IsNullOrEmpty(report.PdfFilePath) || !System.IO.File.Exists(report.PdfFilePath))
            {
                return NotFound("The report PDF file could not be found on the server.");
            }

            // 3. Read the file into a byte array
            var pdfBytes = await System.IO.File.ReadAllBytesAsync(report.PdfFilePath);

            // 4. Determine the file name from the path
            var fileName = Path.GetFileName(report.PdfFilePath);

            // 5. Return the file as a downloadable attachment
            // The "application/pdf" MIME type tells the browser how to handle the file.
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}