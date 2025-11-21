using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeryPOS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ImagesController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        public ImagesController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded." });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Invalid file type." });
            }

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";

            // --- FIX: Handle null WebRootPath ---
            // If WebRootPath is null, default to the current directory + "wwwroot"
            string webRootPath = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            var uploadsFolder = Path.Combine(webRootPath, "images");
            // -------------------------------------

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Fix the URL construction for production (using Request.Host which includes domain)
            // Note: When behind a proxy/load balancer (like AWS/Cloudflare), Request.Scheme might be http.
            // You might need to force https if your domain is https.
            var url = $"{Request.Scheme}://{Request.Host}/images/{uniqueFileName}";

            return Ok(new { imageUrl = url });
        }
    }
}