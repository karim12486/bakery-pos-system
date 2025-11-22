using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BakeryPOS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // All actions require authentication
    public class RemovalController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<RemovalHub> _hubContext;

        public RemovalController(AppDbContext context, IHubContext<RemovalHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // POST: api/removal/request
        // Endpoint for a cashier to request an item removal
        [HttpPost("request")]
        public async Task<IActionResult> RequestRemoval(RemovalRequestCreateDto requestDto)
        {
            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cashier = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (cashier == null) return Unauthorized();

            // 1. Create and save the request to the database
            var newRequest = new RemovalRequest
            {
                ProductName = requestDto.ProductName,
                ProductPrice = requestDto.ProductPrice,
                RequestingUserId = cashier.Id
            };
            await _context.RemovalRequests.AddAsync(newRequest);
            await _context.SaveChangesAsync();

            // 2. Prepare the notification payload for admins
            var notification = new
            {
                requestId = newRequest.Id,
                productName = newRequest.ProductName,
                cashierName = cashier.FullName,
                requestTime = newRequest.RequestTime,
                cashierConnectionId = requestDto.CashierConnectionId // Pass this through
            };

            // 3. Use the Hub to send a real-time message to all connected admins
            await _hubContext.Clients.Group("Admins").SendAsync("NewRemovalRequest", notification);

            return Ok(new { message = "Demande de suppression envoyée à l'administrateur pour approbation." });
        }

        // The only change is fixing the typo in [FromQuery]
        [HttpPost("{requestId}/respond")]
        public async Task<IActionResult> RespondToRequest(int requestId, [FromBody] RemovalResponseDto responseDto, [FromQuery] string cashierConnectionId)
        {
            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var admin = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);

            if (admin == null || !admin.Permissions.HasFlag(Core.Enums.UserPermissions.Admin))
                return Forbid();

            var request = await _context.RemovalRequests
                                .Include(r => r.RequestingUser)
                                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null) return NotFound("Requête introuvable.");
            if (request.Status != RequestStatus.Pending) return BadRequest("Cette demande a déjà été traitée.");

            request.Status = responseDto.IsApproved ? RequestStatus.Approved : RequestStatus.Rejected;
            request.ApprovingUserId = admin.Id;
            await _context.SaveChangesAsync();

            if (string.IsNullOrEmpty(cashierConnectionId))
            {
                return BadRequest("L'identifiant de connexion du caissier est manquant.");
            }

            var update = new
            {
                requestId = request.Id,
                productName = request.ProductName,
                isApproved = responseDto.IsApproved,
                adminName = admin.FullName
            };

            await _hubContext.Clients.Client(cashierConnectionId).SendAsync("RemovalRequestStatusChanged", update);

            return Ok(new { message = $"Demande {request.Status}." });
        }
    }
}