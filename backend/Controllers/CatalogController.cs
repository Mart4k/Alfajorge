using AlfajorgeBackend.Data;
using AlfajorgeBackend.DTOs;
using AlfajorgeBackend.Models;
using AlfajorgeBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlfajorgeBackend.Controllers;

[ApiController]
[Route("api")]
public class CatalogController : ControllerBase
{
    private readonly CatalogService _catalog;
    private readonly AppDbContext _db;

    public CatalogController(CatalogService catalog, AppDbContext db)
    {
        _catalog = catalog;
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet("alfajores")]
    public async Task<IActionResult> GetAlfajores([FromQuery] string? tipo, [FromQuery] string? sabor, [FromQuery] string? cobertura)
    {
        var alfajores = await _catalog.ObtenerCatalogoAsync(tipo, sabor, cobertura);
        return Ok(alfajores.Select(a => new CatalogAlfajorDto(
            a.Id, a.Nombre, a.Descripcion, a.ImagenUrl, a.Tipo, a.Sabor, a.Cobertura,
            a.Presentaciones.Select(p => new CatalogPresentationDto(p.Id, p.Nombre, p.CantidadUnidades, p.Precio, p.Disponibilidad)).ToList())));
    }

    [AllowAnonymous]
    [HttpGet("alfajores/{id:int}")]
    public async Task<IActionResult> GetAlfajor(int id)
    {
        var alfajor = await _catalog.ObtenerAlfajorAsync(id);
        return alfajor == null
            ? NotFound()
            : Ok(new CatalogAlfajorDto(alfajor.Id, alfajor.Nombre, alfajor.Descripcion, alfajor.ImagenUrl, alfajor.Tipo,
                alfajor.Sabor, alfajor.Cobertura,
                alfajor.Presentaciones.Select(p => new CatalogPresentationDto(p.Id, p.Nombre, p.CantidadUnidades, p.Precio, p.Disponibilidad)).ToList()));
    }

    [AllowAnonymous]
    [HttpGet("tipos")]
    public async Task<IActionResult> GetTipos() =>
        Ok((await _catalog.ObtenerTiposAsync()).Select(t => new LookupDto(t.IdTipo, t.Nombre, t.Descripcion)));

    [AllowAnonymous]
    [HttpGet("sabores")]
    public async Task<IActionResult> GetSabores() =>
        Ok((await _catalog.ObtenerSaboresAsync()).Select(s => new LookupDto(s.IdSabor, s.Nombre, s.Descripcion)));

    [AllowAnonymous]
    [HttpGet("coberturas")]
    public async Task<IActionResult> GetCoberturas() =>
        Ok((await _catalog.ObtenerCoberturasAsync()).Select(c => new LookupDto(c.IdCobertura, c.Nombre, c.Descripcion)));

    [AllowAnonymous]
    [HttpGet("presentaciones")]
    public async Task<IActionResult> GetPresentaciones() =>
        Ok((await _catalog.ObtenerPresentacionesAsync()).Select(p =>
            new PresentationLookupDto(p.IdPresentacion, p.Nombre, p.CantidadUnidades, 0m, "Disponible")));

    [AllowAnonymous]
    [HttpGet("promociones")]
    public async Task<IActionResult> GetPromociones() =>
        Ok(await _db.Promociones.AsNoTracking().Include(p => p.Alfajores).OrderBy(p => p.Nombre)
            .Select(p => new PromotionDto(p.IdPromocion, p.Nombre, p.Descripcion, p.PorcentajeDescuento, p.FechaInicio, p.FechaFin,
                p.Alfajores.Select(a => a.IdAlfajor).ToList()))
            .ToListAsync());

    [AllowAnonymous]
    [HttpGet("promociones/activas")]
    public async Task<IActionResult> GetPromocionesActivas()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var promotions = await _db.Promociones.AsNoTracking()
            .Include(p => p.Alfajores)
            .Where(p => p.Activa && p.FechaInicio <= today && p.FechaFin >= today)
            .OrderBy(p => p.Nombre)
            .Select(p => new PromotionDto(p.IdPromocion, p.Nombre, p.Descripcion, p.PorcentajeDescuento,
                p.FechaInicio, p.FechaFin, p.Alfajores.Select(a => a.IdAlfajor).ToList()))
            .ToListAsync();
        return Ok(promotions);
    }

    [Authorize(Policy = "Administrativo")]
    [HttpPost("promociones")]
    public async Task<IActionResult> CrearPromocion([FromBody] SavePromotionRequest request)
    {
        if (!ValidPromotion(request))
        {
            return BadRequest(new { error = "La promoción requiere nombre, descuento entre 0 y 100 y fechas válidas." });
        }

        if (!await ValidAlfajorIdsAsync(request.Alfajores))
            return BadRequest(new { error = "Una o más promociones apuntan a alfajores inexistentes." });

        var promocion = new Promocion
        {
            Nombre = request.Name.Trim(),
            Descripcion = request.Description?.Trim(),
            PorcentajeDescuento = request.Discount,
            FechaInicio = request.Starts,
            FechaFin = request.Ends,
            Activa = request.Active
        };
        if (request.Alfajores != null)
        {
            promocion.Alfajores = request.Alfajores.Distinct()
                .Select(id => new PromocionAlfajor { IdAlfajor = id })
                .ToList();
        }
        _db.Promociones.Add(promocion);
        await _db.SaveChangesAsync();
        return Created($"/api/promociones/{promocion.IdPromocion}", promocion.IdPromocion);
    }

    [Authorize(Policy = "Administrativo")]
    [HttpPut("promociones/{id:int}")]
    public async Task<IActionResult> UpdatePromocion(int id, [FromBody] SavePromotionRequest request)
    {
        if (!ValidPromotion(request))
        {
            return BadRequest(new { error = "La promoción requiere nombre, descuento entre 0 y 100 y fechas válidas." });
        }
        if (!await ValidAlfajorIdsAsync(request.Alfajores))
            return BadRequest(new { error = "Una o más promociones apuntan a alfajores inexistentes." });

        var existing = await _db.Promociones.FindAsync(id);
        if (existing == null) return NotFound();
        existing.Nombre = request.Name.Trim();
        existing.Descripcion = request.Description?.Trim();
        existing.PorcentajeDescuento = request.Discount;
        existing.FechaInicio = request.Starts;
        existing.FechaFin = request.Ends;
        existing.Activa = request.Active;
        if (request.Alfajores != null)
        {
            var currentLinks = await _db.PromocionesAlfajor.Where(link => link.IdPromocion == id).ToListAsync();
            _db.PromocionesAlfajor.RemoveRange(currentLinks);
            foreach (var alfajorId in request.Alfajores.Distinct())
                _db.PromocionesAlfajor.Add(new PromocionAlfajor { IdPromocion = id, IdAlfajor = alfajorId });
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "Administrativo")]
    [HttpDelete("promociones/{id:int}")]
    public async Task<IActionResult> DeletePromocion(int id)
    {
        var item = await _db.Promociones.FindAsync(id);
        if (item == null) return NotFound();
        _db.Promociones.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "Administrativo")]
    [HttpPost("tipos")]
    public async Task<IActionResult> CrearTipo([FromBody] LookupCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "El nombre es obligatorio." });
        var item = new TipoAlfajor { Nombre = request.Name.Trim(), Descripcion = request.Description?.Trim() };
        _db.TiposAlfajor.Add(item);
        await _db.SaveChangesAsync();
        return Created($"/api/tipos/{item.IdTipo}", new LookupDto(item.IdTipo, item.Nombre, item.Descripcion));
    }

    [Authorize(Policy = "Administrativo")]
    [HttpPut("tipos/{id:int}")]
    public async Task<IActionResult> UpdateTipo(int id, [FromBody] LookupCreateRequest request)
    {
        var item = await _db.TiposAlfajor.FindAsync(id);
        if (item == null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "El nombre es obligatorio." });
        item.Nombre = request.Name.Trim();
        item.Descripcion = request.Description?.Trim();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "Administrativo")]
    [HttpDelete("tipos/{id:int}")]
    public Task<IActionResult> DeleteTipo(int id) => DeleteLookupAsync(_db.TiposAlfajor, id);

    [Authorize(Policy = "Administrativo")]
    [HttpPost("sabores")]
    public async Task<IActionResult> CrearSabor([FromBody] LookupCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "El nombre es obligatorio." });
        var item = new Sabor { Nombre = request.Name.Trim(), Descripcion = request.Description?.Trim() };
        _db.Sabores.Add(item);
        await _db.SaveChangesAsync();
        return Created($"/api/sabores/{item.IdSabor}", new LookupDto(item.IdSabor, item.Nombre, item.Descripcion));
    }

    [Authorize(Policy = "Administrativo")]
    [HttpPut("sabores/{id:int}")]
    public async Task<IActionResult> UpdateSabor(int id, [FromBody] LookupCreateRequest request)
    {
        var item = await _db.Sabores.FindAsync(id);
        if (item == null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "El nombre es obligatorio." });
        item.Nombre = request.Name.Trim();
        item.Descripcion = request.Description?.Trim();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "Administrativo")]
    [HttpDelete("sabores/{id:int}")]
    public Task<IActionResult> DeleteSabor(int id) => DeleteLookupAsync(_db.Sabores, id);

    [Authorize(Policy = "Administrativo")]
    [HttpPost("coberturas")]
    public async Task<IActionResult> CrearCobertura([FromBody] LookupCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "El nombre es obligatorio." });
        var item = new Cobertura { Nombre = request.Name.Trim(), Descripcion = request.Description?.Trim() };
        _db.Coberturas.Add(item);
        await _db.SaveChangesAsync();
        return Created($"/api/coberturas/{item.IdCobertura}", new LookupDto(item.IdCobertura, item.Nombre, item.Descripcion));
    }

    [Authorize(Policy = "Administrativo")]
    [HttpPut("coberturas/{id:int}")]
    public async Task<IActionResult> UpdateCobertura(int id, [FromBody] LookupCreateRequest request)
    {
        var item = await _db.Coberturas.FindAsync(id);
        if (item == null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "El nombre es obligatorio." });
        item.Nombre = request.Name.Trim();
        item.Descripcion = request.Description?.Trim();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "Administrativo")]
    [HttpDelete("coberturas/{id:int}")]
    public Task<IActionResult> DeleteCobertura(int id) => DeleteLookupAsync(_db.Coberturas, id);

    [Authorize(Policy = "Administrativo")]
    [HttpPost("presentaciones")]
    public async Task<IActionResult> CrearPresentacion([FromBody] LookupCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Units is null or <= 0)
            return BadRequest(new { error = "El nombre y una cantidad de unidades positiva son obligatorios." });
        var item = new Presentacion { Nombre = request.Name.Trim(), CantidadUnidades = request.Units.Value };
        _db.Presentaciones.Add(item);
        await _db.SaveChangesAsync();
        return Created($"/api/presentaciones/{item.IdPresentacion}",
            new PresentationLookupDto(item.IdPresentacion, item.Nombre, item.CantidadUnidades, 0m, "Disponible"));
    }

    [Authorize(Policy = "Administrativo")]
    [HttpPut("presentaciones/{id:int}")]
    public async Task<IActionResult> UpdatePresentacion(int id, [FromBody] LookupCreateRequest request)
    {
        var item = await _db.Presentaciones.FindAsync(id);
        if (item == null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Units is null or <= 0)
            return BadRequest(new { error = "El nombre y una cantidad de unidades positiva son obligatorios." });
        item.Nombre = request.Name.Trim();
        item.CantidadUnidades = request.Units.Value;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "Administrativo")]
    [HttpDelete("presentaciones/{id:int}")]
    public Task<IActionResult> DeletePresentacion(int id) => DeleteLookupAsync(_db.Presentaciones, id);

    private async Task<IActionResult> DeleteLookupAsync<TEntity>(DbSet<TEntity> set, int id) where TEntity : class
    {
        var item = await set.FindAsync(id);
        if (item == null) return NotFound();
        set.Remove(item);
        try
        {
            await _db.SaveChangesAsync();
            return NoContent();
        }
        catch (DbUpdateException)
        {
            _db.Entry(item).State = EntityState.Unchanged;
            return Conflict(new { error = "No se puede eliminar porque el registro está siendo utilizado." });
        }
    }

    private async Task<bool> ValidAlfajorIdsAsync(List<int>? ids)
    {
        if (ids == null || ids.Count == 0) return true;
        var uniqueIds = ids.Distinct().ToArray();
        return uniqueIds.All(id => id > 0) &&
            await _db.Alfajores.CountAsync(alfajor => uniqueIds.Contains(alfajor.IdAlfajor)) == uniqueIds.Length;
    }

    private static bool ValidPromotion(SavePromotionRequest promotion) =>
        !string.IsNullOrWhiteSpace(promotion.Name) &&
        promotion.Discount is > 0 and <= 100 &&
        promotion.Ends >= promotion.Starts;
}
