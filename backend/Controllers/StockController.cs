using AlfajorgeBackend.Data;
using AlfajorgeBackend.Models;
using AlfajorgeBackend.DTOs;
using AlfajorgeBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlfajorgeBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Empleado")]
public class StockController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly CatalogService _catalog;

    public StockController(AppDbContext db, AuthService auth, CatalogService catalog)
    {
        _db = db;
        _auth = auth;
        _catalog = catalog;
    }

    [HttpGet]
    public async Task<IActionResult> GetStock()
    {
        var user = await _auth.GetAuthenticatedUserAsync(User);
        if (user == null || (user.Rol != "ADMINISTRATIVO" && user.Rol != "VENDEDOR")) return Forbid();

        var stock = await _catalog.ObtenerStockAsync();
        return Ok(stock.Select(ToStockDto));
    }

    [HttpGet("disponible")]
    public async Task<IActionResult> GetAvailableStock()
    {
        var stock = await _catalog.ObtenerStockAsync();
        return Ok(stock.Where(s => s.Cantidad > 0).Select(ToStockDto));
    }

    [HttpGet("{idAlfajor:int}/{idPresentacion:int}")]
    public async Task<IActionResult> GetStockItem(int idAlfajor, int idPresentacion)
    {
        var user = await _auth.GetAuthenticatedUserAsync(User);
        if (user == null || (user.Rol != "ADMINISTRATIVO" && user.Rol != "VENDEDOR")) return Forbid();

        var item = await _catalog.ObtenerStockAsync(idAlfajor, idPresentacion);
        if (item == null) return NotFound();

        var stock = await _catalog.ObtenerStockAsync();
        var itemWithNames = stock.FirstOrDefault(s => s.IdAlfajor == idAlfajor && s.IdPresentacion == idPresentacion);
        return itemWithNames == null ? NotFound() : Ok(ToStockDto(itemWithNames));
    }

    [HttpPut("{idAlfajor:int}/{idPresentacion:int}")]
    public async Task<IActionResult> UpdateStock(int idAlfajor, int idPresentacion, [FromBody] UpdateStockRequest request)
    {
        var user = await _auth.GetAuthenticatedUserAsync(User);
        if (user == null || user.Rol != "ADMINISTRATIVO") return Forbid();

        var item = await _db.Stocks.FirstOrDefaultAsync(s => s.IdAlfajor == idAlfajor && s.IdPresentacion == idPresentacion);
        if (item == null) return NotFound();
        if (request.Quantity < 0 || request.Minimum < 0)
            return BadRequest(new { error = "El stock y el stock mínimo no pueden ser negativos." });

        item.Cantidad = request.Quantity;
        item.StockMinimo = request.Minimum;
        item.UltimaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static StockItemDto ToStockDto(Stock item) => new(
        item.IdAlfajor,
        item.IdPresentacion,
        item.AlfajorPresentacion.Alfajor.Nombre,
        item.AlfajorPresentacion.Presentacion.Nombre,
        item.Cantidad,
        item.StockMinimo,
        item.UltimaActualizacion);
}
