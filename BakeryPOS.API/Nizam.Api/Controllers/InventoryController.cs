using System.Security.Claims;
using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventory;

        public InventoryController(IInventoryService inventory)
        {
            _inventory = inventory;
        }

        [HasPermission(UserPermissions.ManageInventory)]
        [HttpPost("add")]
        public async Task<IActionResult> AddStock(StockAdditionDto dto, CancellationToken ct)
        {
            var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
            var (productName, newQuantity) = await _inventory.AddStockAsync(dto, username, ct);
            return Ok($"Stock pour '{productName}' mis à jour avec succès. Nouvelle quantité : {newQuantity}");
        }

        [HasPermission(UserPermissions.ManageInventory)]
        [HttpGet("history/{productId:int}")]
        public async Task<ActionResult<IEnumerable<StockMovementDto>>> GetStockHistory(int productId, CancellationToken ct)
        {
            var movements = await _inventory.GetStockHistoryAsync(productId, ct);
            return movements.Count == 0
                ? NotFound($"Aucun historique de stock trouvé pour le produit portant l'identifiant {productId}.")
                : Ok(movements);
        }
    }
}
