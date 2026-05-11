using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Common.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace BakeryPOS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ImagesController : ControllerBase
    {
        // 5 MB limit. Bakery/café product photos are well under this; anything larger is almost
        // always a UI mistake (uncompressed phone photo, etc.) and should be rejected.
        private const long MaxFileSizeBytes = 5L * 1024 * 1024;

        // Files live OUTSIDE wwwroot/ so the static-file middleware doesn't serve them anonymously.
        // Reads go through this controller, which enforces tenant scoping. Path is:
        //   {ContentRoot}/content/images/tenant-{id}/{guid}.{ext}
        public const string ContentImagesRelativeRoot = "content/images";

        // File-signature magic numbers — protects against extension-spoofed uploads.
        private static readonly Dictionary<string, byte[][]> AcceptedSignatures = new()
        {
            [".jpg"]  = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".png"]  = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
            [".webp"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } } // "RIFF"; WEBP marker checked below
        };

        private readonly IWebHostEnvironment _environment;
        private readonly ICurrentTenant _currentTenant;
        private readonly FileExtensionContentTypeProvider _mimeProvider = new();

        public ImagesController(IWebHostEnvironment environment, ICurrentTenant currentTenant)
        {
            _environment = environment;
            _currentTenant = currentTenant;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(MaxFileSizeBytes)]
        public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct)
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

            // Magic-number validation against extension spoofing.
            var header = new byte[12];
            await using (var stream = file.OpenReadStream())
            {
                var read = await stream.ReadAsync(header, 0, header.Length, ct);
                if (read < 4)
                    throw new DomainException("ERR_FILE_INVALID", "Fichier vide ou corrompu.");
            }
            if (!MatchesSignature(extension, header))
                throw new DomainException("ERR_FILE_CONTENT_MISMATCH",
                    "Le contenu du fichier ne correspond pas à son extension.");

            var tenantFolder = TenantImagesPath(tenantId);
            Directory.CreateDirectory(tenantFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(tenantFolder, uniqueFileName);
            await using (var output = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(output, ct);
            }

            // URL is served by THIS controller's GetImage action — clients fetching it must
            // present a Bearer token for the same tenant. X-Forwarded-Proto wins over Scheme
            // when behind a TLS-terminating proxy so the saved URL is https:// in production.
            var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
            var url = $"{scheme}://{Request.Host}/api/images/{tenantId}/{uniqueFileName}";

            return Ok(new { imageUrl = url });
        }

        /// <summary>
        /// Serves a tenant-scoped image. The path includes the tenantId so links are
        /// deterministic, but the controller VERIFIES the requesting JWT's tenant matches
        /// before streaming the file — so a token for Tenant A cannot fetch Tenant B's images.
        /// </summary>
        [HttpGet("{tenantId:int}/{filename}")]
        public IActionResult GetImage(int tenantId, string filename)
        {
            if (_currentTenant.TenantId != tenantId)
                return Forbid();

            // Defence against path traversal in `filename`. Filenames are server-generated GUIDs
            // so they should never contain separators, but cheap to enforce.
            if (filename.Contains('/') || filename.Contains('\\') || filename.Contains(".."))
                return BadRequest();

            var fullPath = Path.Combine(TenantImagesPath(tenantId), filename);
            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            if (!_mimeProvider.TryGetContentType(filename, out var mime))
                mime = "application/octet-stream";

            // Long cache — files are immutable (GUID filename). Reduces re-fetch traffic for the
            // POS app rendering many product images on every screen.
            Response.Headers.CacheControl = "private, max-age=86400";
            return PhysicalFile(fullPath, mime);
        }

        private string TenantImagesPath(int tenantId) =>
            Path.Combine(_environment.ContentRootPath, ContentImagesRelativeRoot, $"tenant-{tenantId}");

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
                    // WebP: bytes 8-11 must be "WEBP" — the RIFF magic alone is shared with AVI/WAV.
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
