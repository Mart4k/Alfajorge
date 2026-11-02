using AlfajorgeBackend.Data;
using AlfajorgeBackend.DTOs;
using AlfajorgeBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlfajorgeBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FacturasController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public FacturasController(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    [HttpGet]
    public async Task<IActionResult> GetFacturas()
    {
        var user = await _auth.GetAuthenticatedUserAsync(User);
        if (user == null || user.Rol != "ADMINISTRATIVO") return Forbid();

        var facturas = await _db.Facturas.AsNoTracking().OrderByDescending(f => f.Fecha)
            .Select(f => new InvoiceDto(f.IdFactura, f.NumeroFactura, f.Fecha, f.Total))
            .ToListAsync();
        return Ok(facturas);
    }

    [HttpGet("mias")]
    public async Task<IActionResult> GetMisFacturas()
    {
        var user = await _auth.GetAuthenticatedUserAsync(User);
        if (user == null) return Forbid();

        var query = _db.Facturas.AsNoTracking().AsQueryable();
        if (user.Rol == "CLIENTE")
            query = query.Where(f => f.Operacion.IdCliente == user.IdPersona);
        else if (user.Rol == "VENDEDOR")
            query = query.Where(f => f.Operacion.IdVendedor == user.IdPersona);
        else if (user.Rol != "ADMINISTRATIVO")
            return Forbid();

        var facturas = await query.OrderByDescending(f => f.Fecha)
            .Select(f => new InvoiceDto(f.IdFactura, f.NumeroFactura, f.Fecha, f.Total))
            .ToListAsync();
        return Ok(facturas);
    }
}
