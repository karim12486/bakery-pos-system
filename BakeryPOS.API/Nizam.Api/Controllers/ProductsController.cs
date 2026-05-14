using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.DTOs.Shared;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly IProductService _products;

    public ProductsController(IProductService products)
    {
        _products = products;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductDto>>> GetProducts(
        [FromQuery] int? categoryId,
        [FromQuery] string? search,
        [FromQuery] PaginationParams pagination,
        CancellationToken ct)
        => Ok(await _products.ListAsync(categoryId, search, pagination, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id, CancellationToken ct)
    {
        var product = await _products.GetAsync(id, ct);
        return product == null ? NotFound() : Ok(product);
    }

    [HttpGet("barcode/{barcode}")]
    public async Task<ActionResult<ProductDto>> GetProductByBarcode(string barcode, CancellationToken ct)
    {
        var product = await _products.GetByBarcodeAsync(barcode, ct);
        return product == null ? NotFound(new { message = "Produit introuvable" }) : Ok(product);
    }

    [HttpPost]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult<ProductDto>> CreateProduct(ProductForCreateDto dto, CancellationToken ct)
    {
        var product = await _products.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<IActionResult> UpdateProduct(int id, ProductForUpdateDto dto, CancellationToken ct)
    {
        await _products.UpdateAsync(id, dto, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
    {
        await _products.SoftDeleteAsync(id, ct);
        return NoContent();
    }
}
