using AlfajorgeBackend.Data;
using AlfajorgeBackend.DTOs;
using AlfajorgeBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlfajorgeBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Administrativo")]
public class PagosController : ControllerBase
{
    private readonly AppDbContext _db;

    public PagosController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetPagos()
    {
        var payments = await _db.Pagos.AsNoTracking()
            .OrderByDescending(p => p.Fecha)
            .Select(p => new PaymentDto(p.IdPago, p.IdOperacion, p.Metodo, p.Monto, p.Estado, p.ReferenciaExterna, p.Fecha))
            .ToListAsync();
        return Ok(payments);
    }

    [HttpPost("{id:int}/confirmar")]
    public async Task<IActionResult> ConfirmarPago(int id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var payment = await _db.Pagos.FirstOrDefaultAsync(p => p.IdPago == id);
        if (payment == null) return NotFound();
        if (payment.Estado != "PENDIENTE")
            return Conflict(new { error = "Solo se pueden confirmar pagos pendientes." });

        var operation = await _db.Operaciones
            .Include(o => o.Detalles)
            .FirstOrDefaultAsync(o => o.IdOperacion == payment.IdOperacion);
        if (operation == null)
            return Conflict(new { error = "La operación asociada no existe." });
        if (operation.Estado != "PENDIENTE" || payment.Monto != operation.Total || operation.Detalles.Count == 0)
            return Conflict(new { error = "El monto del pago no coincide con una operación válida." });

        var claimed = await _db.Operaciones
            .Where(item => item.IdOperacion == operation.IdOperacion && item.Estado == "PENDIENTE")
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Estado, "PAGADA"));
        if (claimed != 1)
            return Conflict(new { error = "La operación ya cambió de estado y el pago no puede confirmarse." });
        operation.Estado = "PAGADA";

        foreach (var detail in operation.Detalles)
        {
            var updated = await _db.Stocks
                .Where(s => s.IdAlfajor == detail.IdAlfajor &&
                    s.IdPresentacion == detail.IdPresentacion &&
                    s.Cantidad >= detail.Cantidad)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.Cantidad, s => s.Cantidad - detail.Cantidad)
                    .SetProperty(s => s.UltimaActualizacion, DateTime.UtcNow));
            if (updated != 1)
                return Conflict(new { error = "No hay stock suficiente para confirmar esta operación." });
        }

        payment.Estado = "APROBADO";
        var invoice = await _db.Facturas.AnyAsync(f => f.IdOperacion == operation.IdOperacion);
        if (!invoice)
        {
            _db.Facturas.Add(new Models.Factura
            {
                IdOperacion = operation.IdOperacion,
                NumeroFactura = $"FAC-{DateTime.UtcNow:yyyyMMdd}-{operation.IdOperacion}",
                Total = operation.Total
            });
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new PaymentDto(payment.IdPago, payment.IdOperacion, payment.Metodo, payment.Monto, payment.Estado, payment.ReferenciaExterna, payment.Fecha));
    }
}
