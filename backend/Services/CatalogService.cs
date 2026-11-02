using AlfajorgeBackend.Data;
using AlfajorgeBackend.DTOs;
using AlfajorgeBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace AlfajorgeBackend.Services;

public class CatalogService
{
    private readonly AppDbContext _db;

    public CatalogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<AlfajorDetalleDto>> ObtenerCatalogoAsync(string? tipo = null, string? sabor = null, string? cobertura = null)
    {
        var query = _db.Alfajores
            .Include(a => a.Tipo)
            .Include(a => a.Sabor)
            .Include(a => a.Cobertura)
            .Include(a => a.Presentaciones)
                .ThenInclude(ap => ap.Presentacion)
            .Include(a => a.Presentaciones)
                .ThenInclude(ap => ap.Stock)
            .Where(a => a.Activo);

        if (!string.IsNullOrWhiteSpace(tipo))
        {
            query = query.Where(a => a.Tipo.Nombre.Contains(tipo));
        }

        if (!string.IsNullOrWhiteSpace(sabor))
        {
            query = query.Where(a => a.Sabor.Nombre.Contains(sabor));
        }

        if (!string.IsNullOrWhiteSpace(cobertura))
        {
            query = query.Where(a => a.Cobertura.Nombre.Contains(cobertura));
        }

        var alfajores = await query.OrderBy(a => a.Nombre).ToListAsync();

        return alfajores.Select(a => new AlfajorDetalleDto(
            a.IdAlfajor,
            a.Nombre,
            a.Descripcion,
            a.ImagenUrl,
            a.Tipo.Nombre,
            a.Sabor.Nombre,
            a.Cobertura.Nombre,
            a.Presentaciones
                .OrderBy(ap => ap.IdPresentacion)
                .Select(ap => new PresentacionDto(
                    ap.IdPresentacion,
                    ap.Presentacion.Nombre,
                    ap.Presentacion.CantidadUnidades,
                    ap.Precio,
                    ap.Stock == null || ap.Stock.Cantidad <= 0 ? "Sin stock" : ap.Stock.Cantidad <= 3 ? "Últimas unidades" : "Disponible",
                    ap.Stock?.Cantidad ?? 0,
                    ap.Stock?.StockMinimo ?? 0))
                .ToList(),
            a.Activo)).ToList();
    }

    public async Task<AlfajorDetalleDto?> ObtenerAlfajorAsync(int id)
    {
        var resultado = await ObtenerCatalogoAsync();
        return resultado.FirstOrDefault(a => a.Id == id);
    }

    public async Task<List<TipoAlfajor>> ObtenerTiposAsync() => await _db.TiposAlfajor.OrderBy(t => t.Nombre).ToListAsync();
    public async Task<List<Sabor>> ObtenerSaboresAsync() => await _db.Sabores.OrderBy(s => s.Nombre).ToListAsync();
    public async Task<List<Cobertura>> ObtenerCoberturasAsync() => await _db.Coberturas.OrderBy(c => c.Nombre).ToListAsync();
    public async Task<List<Presentacion>> ObtenerPresentacionesAsync() => await _db.Presentaciones.OrderBy(p => p.CantidadUnidades).ToListAsync();

    public async Task<List<Stock>> ObtenerStockAsync() => await _db.Stocks.Include(s => s.AlfajorPresentacion).ThenInclude(ap => ap.Alfajor).Include(s => s.AlfajorPresentacion).ThenInclude(ap => ap.Presentacion).OrderBy(s => s.IdAlfajor).ThenBy(s => s.IdPresentacion).ToListAsync();

    public async Task<Stock?> ObtenerStockAsync(int idAlfajor, int idPresentacion) => await _db.Stocks.FirstOrDefaultAsync(s => s.IdAlfajor == idAlfajor && s.IdPresentacion == idPresentacion);

    public async Task<List<Promocion>> ObtenerPromocionesActivasAsync() => await _db.Promociones.Where(p => p.Activa && p.FechaInicio <= DateOnly.FromDateTime(DateTime.UtcNow) && p.FechaFin >= DateOnly.FromDateTime(DateTime.UtcNow)).OrderBy(p => p.Nombre).ToListAsync();
}
