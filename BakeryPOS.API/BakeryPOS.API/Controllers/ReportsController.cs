using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
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

        public ReportsController(AppDbContext context)
        {
            _context = context;
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
            var reportData = JsonSerializer.Deserialize<object>(report.ReportDataJson);

            return Ok(reportData);
        }
    }
}