using AlfajorgeBackend.Data;
using AlfajorgeBackend.DTOs;
using AlfajorgeBackend.Models;
using AlfajorgeBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlfajorgeBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Administrativo")]
public class AlfajoresController : ControllerBase
{
    private readonly AppDbContext _db;

    public AlfajoresController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{id:int}/presentaciones")]
    public async Task<IActionResult> GetPresentations(int id)
    {
        if (!await _db.Alfajores.AnyAsync(alfajor => alfajor.IdAlfajor == id))
            return NotFound();

        var combinations = await _db.AlfajoresPresentacion
            .AsNoTracking()
            .Where(combination => combination.IdAlfajor == id)
            .OrderBy(combination => combination.Presentacion.CantidadUnidades)
            .Select(combination => new PriceCombinationDto(
                combination.IdAlfajor,
                combination.IdPresentacion,
                combination.Presentacion.Nombre,
                combination.Presentacion.CantidadUnidades,
                combination.Precio,
                combination.Stock == null ? 0 : combination.Stock.Cantidad,
                combination.Stock == null ? 0 : combination.Stock.StockMinimo))
            .ToListAsync();

        return Ok(combinations);
    }

    [HttpPost("{id:int}/presentaciones")]
    public async Task<IActionResult> AddPresentation(int id, [FromBody] CreateAlfajorPresentationRequest request)
    {
        if (request.PresentationId <= 0 || !PresentationPriceRules.IsValid(request.Price) ||
            request.Stock < 0 || request.StockMinimum < 0)
            return BadRequest(new { error = "La presentación, el precio y las cantidades de stock deben ser válidos." });

        if (!await _db.Alfajores.AnyAsync(alfajor => alfajor.IdAlfajor == id))
            return NotFound();
        if (!await _db.Presentaciones.AnyAsync(presentation => presentation.IdPresentacion == request.PresentationId))
            return BadRequest(new { error = "La presentación seleccionada no existe." });
        if (await _db.AlfajoresPresentacion.AnyAsync(combination =>
                combination.IdAlfajor == id && combination.IdPresentacion == request.PresentationId))
            return Conflict(new { error = "Esta presentación ya está asociada al alfajor." });

        await using var transaction = await _db.Database.BeginTransactionAsync();
        _db.AlfajoresPresentacion.Add(new AlfajorPresentacion
        {
            IdAlfajor = id,
            IdPresentacion = request.PresentationId,
            Precio = request.Price,
            Stock = new Stock
            {
                IdAlfajor = id,
                IdPresentacion = request.PresentationId,
                Cantidad = request.Stock,
                StockMinimo = request.StockMinimum,
                UltimaActualizacion = DateTime.UtcNow
            }
        });
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return CreatedAtAction(nameof(GetPresentations), new { id }, new { id, request.PresentationId });
    }

    [HttpPut("{id:int}/presentaciones/{presentationId:int}/precio")]
    public async Task<IActionResult> UpdatePresentationPrice(
        int id,
        int presentationId,
        [FromBody] UpdatePresentationPriceRequest request)
    {
        if (!PresentationPriceRules.IsValid(request.Price))
            return BadRequest(new { error = "El precio debe ser no negativo y tener como máximo dos decimales." });

        var combination = await _db.AlfajoresPresentacion
            .FirstOrDefaultAsync(item => item.IdAlfajor == id && item.IdPresentacion == presentationId);
        if (combination == null)
            return NotFound(new { error = "No existe esa combinación de alfajor y presentación." });

        combination.Precio = request.Price;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAlfajorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Presentations is not { Count: > 0 } presentations ||
            presentations.Any(p => p.PresentationId <= 0 || !PresentationPriceRules.IsValid(p.Price) || p.Stock < 0 || p.StockMinimum < 0) ||
            presentations.Select(p => p.PresentationId).Distinct().Count() != presentations.Count)
        {
            return BadRequest(new { error = "El alfajor requiere nombre y presentaciones únicas con precio y stock válidos." });
        }

        if (!await _db.TiposAlfajor.AnyAsync(t => t.IdTipo == request.TypeId) ||
            !await _db.Sabores.AnyAsync(s => s.IdSabor == request.FlavorId) ||
            !await _db.Coberturas.AnyAsync(c => c.IdCobertura == request.CoverageId))
        {
            return BadRequest(new { error = "El tipo, sabor o cobertura seleccionado no existe." });
        }

        var normalizedName = request.Name.Trim().ToLower();
        if (await _db.Alfajores.AnyAsync(alfajor =>
                alfajor.Activo &&
                alfajor.Nombre.Trim().ToLower() == normalizedName &&
                alfajor.IdTipo == request.TypeId &&
                alfajor.IdSabor == request.FlavorId &&
                alfajor.IdCobertura == request.CoverageId))
        {
            return Conflict(new { error = "Ya existe un alfajor activo con ese nombre, tipo, sabor y cobertura." });
        }

        var presentationIds = presentations.Select(p => p.PresentationId).ToArray();
        if (await _db.Presentaciones.CountAsync(p => presentationIds.Contains(p.IdPresentacion)) != presentationIds.Length)
        {
            return BadRequest(new { error = "Una o más presentaciones no existen." });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var alfajor = new Alfajor
        {
            Nombre = request.Name.Trim(),
            Descripcion = request.Description?.Trim(),
            ImagenUrl = request.ImageUrl?.Trim(),
            IdTipo = request.TypeId,
            IdSabor = request.FlavorId,
            IdCobertura = request.CoverageId,
            Activo = true,
            Presentaciones = presentations.Select(p => new AlfajorPresentacion
            {
                IdPresentacion = p.PresentationId,
                Precio = p.Price,
                Stock = new Stock { Cantidad = p.Stock, StockMinimo = p.StockMinimum }
            }).ToList()
        };
        _db.Alfajores.Add(alfajor);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (await _db.Alfajores.AnyAsync(existing =>
                    existing.Activo &&
                    existing.Nombre.Trim().ToLower() == normalizedName &&
                    existing.IdTipo == request.TypeId &&
                    existing.IdSabor == request.FlavorId &&
                    existing.IdCobertura == request.CoverageId))
            {
                return Conflict(new { error = "Ya existe un alfajor activo con ese nombre, tipo, sabor y cobertura." });
            }

            throw;
        }
        await transaction.CommitAsync();

        return Created($"/api/alfajores/{alfajor.IdAlfajor}", new { id = alfajor.IdAlfajor });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateAlfajorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "El nombre es obligatorio." });

        var entity = await _db.Alfajores.FindAsync(id);
        if (entity == null) return NotFound();
        if (!await _db.TiposAlfajor.AnyAsync(t => t.IdTipo == request.TypeId) ||
            !await _db.Sabores.AnyAsync(s => s.IdSabor == request.FlavorId) ||
            !await _db.Coberturas.AnyAsync(c => c.IdCobertura == request.CoverageId))
            return BadRequest(new { error = "El tipo, sabor o cobertura seleccionado no existe." });

        entity.Nombre = request.Name.Trim();
        entity.Descripcion = request.Description?.Trim();
        entity.ImagenUrl = request.ImageUrl?.Trim();
        entity.IdTipo = request.TypeId;
        entity.IdSabor = request.FlavorId;
        entity.IdCobertura = request.CoverageId;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Alfajores.FindAsync(id);
        if (entity == null) return NotFound();
        entity.Activo = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
