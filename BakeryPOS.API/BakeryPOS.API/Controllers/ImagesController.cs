using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Common.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeryPOS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ImagesController : ControllerBase
    {
        // 5 MB limit. Bakery/café product photos are well under this; if a customer needs more
        // they can override per-environment. Anything larger almost always indicates a UI mistake
        // (uncompressed phone photo, etc.) and should be rejected, not silently absorbed.
        private const long MaxFileSizeBytes = 5L * 1024 * 1024;

        // File-signature magic numbers — protects against extension-spoofed uploads.
        private static readonly Dictionary<string, byte[][]> AcceptedSignatures = new()
        {
            [".jpg"]  = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".png"]  = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
            // WebP files begin with "RIFF....WEBP" — we check the RIFF + WEBP markers.
            [".webp"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } } // "RIFF"; WEBP marker checked below
        };

        private readonly IWebHostEnvironment _environment;
        private readonly ICurrentTenant _currentTenant;

        public ImagesController(IWebHostEnvironment environment, ICurrentTenant currentTenant)
        {
            _environment = environment;
            _currentTenant = currentTenant;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(MaxFileSizeBytes)]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (_currentTenant.TenantId is not int tenantId)
                return Unauthorized();

            if (file == null || file.Length == 0)
                throw new DomainException("ERR_NO_FILE", "Aucun fichier téléchargé.");

            if (file.Length > MaxFileSizeBytes)
                throw new DomainException("ERR_FILE_TOO_LARGE",
                    $"Le fichier dépasse la taille maximale de {MaxFileSizeBytes / 1024 / 1024} Mo.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AcceptedSignatures.ContainsKey(extension))
                throw new DomainException("ERR_FILE_TYPE_NOT_ALLOWED",
                    "Type de fichier non autorisé. Formats acceptés : .jpg, .jpeg, .png, .webp.");

            // Read first 12 bytes (enough for RIFF + WEBP marker) for magic-number validation.
            var header = new byte[12];
            await using (var stream = file.OpenReadStream())
            {
                var read = await stream.ReadAsync(header, 0, header.Length);
                if (read < 4)
                    throw new DomainException("ERR_FILE_INVALID", "Fichier vide ou corrompu.");
            }

            if (!MatchesSignature(extension, header))
                throw new DomainException("ERR_FILE_CONTENT_MISMATCH",
                    "Le contenu du fichier ne correspond pas à son extension.");

            // Tenant-prefixed path. Two tenants can never collide; per-tenant cleanup is one rmdir.
            var webRootPath = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var tenantFolder = Path.Combine(webRootPath, "images", $"tenant-{tenantId}");
            if (!Directory.Exists(tenantFolder))
                Directory.CreateDirectory(tenantFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(tenantFolder, uniqueFileName);

            await using (var output = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(output);
            }

            // Respect X-Forwarded-Proto when behind a TLS-terminating proxy/LB — otherwise saved
            // URLs are stored as http:// and embed as mixed content in https:// receipts/clients.
            var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
            var url = $"{scheme}://{Request.Host}/images/tenant-{tenantId}/{uniqueFileName}";

            return Ok(new { imageUrl = url });
        }

        private static bool MatchesSignature(string extension, byte[] header)
        {
            if (!AcceptedSignatures.TryGetValue(extension, out var sigs)) return false;

            foreach (var sig in sigs)
            {
                if (header.Length < sig.Length) continue;
                var match = true;
                for (var i = 0; i < sig.Length; i++)
                {
                    if (header[i] != sig[i]) { match = false; break; }
                }
                if (match)
                {
                    // WebP needs an extra check: bytes 8-11 must be "WEBP".
                    if (extension == ".webp" && header.Length >= 12)
                    {
                        if (header[8] != 0x57 || header[9] != 0x45 || header[10] != 0x42 || header[11] != 0x50)
                            return false;
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
