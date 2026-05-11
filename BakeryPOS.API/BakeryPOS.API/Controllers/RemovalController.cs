using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Common.Tenancy;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
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
    [Authorize]
    public class RemovalController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<RemovalHub> _hubContext;
        private readonly ICurrentTenant _currentTenant;

        public RemovalController(AppDbContext context, IHubContext<RemovalHub> hubContext, ICurrentTenant currentTenant)
        {
            _context = context;
            _hubContext = hubContext;
            _currentTenant = currentTenant;
        }

        // POST: api/removal/request
        // Cashier requests an item removal; notifies admins of THIS tenant only.
        [HttpPost("request")]
        public async Task<IActionResult> RequestRemoval(RemovalRequestCreateDto requestDto)
        {
            if (_currentTenant.TenantId is not int tenantId)
                return Unauthorized();

            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cashier = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (cashier == null) return Unauthorized();

            var newRequest = new RemovalRequest
            {
                ProductName = requestDto.ProductName,
                ProductPrice = requestDto.ProductPrice,
                RequestingUserId = cashier.Id
                // TenantId auto-stamped by AppDbContext.SaveChanges
            };
            await _context.RemovalRequests.AddAsync(newRequest);
            await _context.SaveChangesAsync();

            var notification = new
            {
                requestId = newRequest.Id,
                productName = newRequest.ProductName,
                cashierName = cashier.FullName,
                requestTime = newRequest.RequestTime,
                cashierConnectionId = requestDto.CashierConnectionId
            };

            // Tenant-scoped admin group — admins of OTHER tenants don't receive this.
            await _hubContext.Clients.Group(RemovalHub.AdminGroup(tenantId)).SendAsync("NewRemovalRequest", notification);

            return Ok(new { message = "Demande de suppression envoyée à l'administrateur pour approbation." });
        }

        // POST: api/removal/{requestId}/respond
        [HttpPost("{requestId:int}/respond")]
        public async Task<IActionResult> RespondToRequest(int requestId, [FromBody] RemovalResponseDto responseDto, [FromQuery] string cashierConnectionId)
        {
            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var admin = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);

            if (admin == null || !admin.Permissions.HasFlag(UserPermissions.ApproveRemovals))
                return Forbid();

            // The closed tenant filter ensures we can only mutate THIS tenant's request rows;
            // a cross-tenant requestId returns null → NotFound.
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
