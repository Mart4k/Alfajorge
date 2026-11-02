using AlfajorgeBackend.Data;
using AlfajorgeBackend.DTOs;
using AlfajorgeBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace AlfajorgeBackend.Services;

public sealed class SaleConflictException(string message) : Exception(message);

public class OperacionService
{
    private static readonly HashSet<string> PaymentMethods =
        ["EFECTIVO", "TRANSFERENCIA", "TARJETA", "MERCADO_PAGO"];
    private readonly AppDbContext _db;

    public OperacionService(AppDbContext db)
    {
        _db = db;
    }

    public Task<PurchaseResult> CrearCompraWebAsync(AuthUser usuario, PurchaseRequest request) =>
        CreateSaleAsync(usuario, usuario.IdPersona, "WEB", request);

    public async Task<PurchaseResult> CrearVentaPresencialAsync(AuthUser usuario, int idCliente, PurchaseRequest request)
    {
        if (usuario.Rol != "VENDEDOR")
            throw new UnauthorizedAccessException("Solo un vendedor puede registrar una venta presencial.");

        var employeeIsSeller = await _db.Empleados.AnyAsync(e =>
            e.IdPersona == usuario.IdPersona && e.Cargo == "VENDEDOR" &&
            _db.Vendedores.Any(v => v.IdPersona == e.IdPersona));
        if (!employeeIsSeller)
            throw new UnauthorizedAccessException("El usuario no tiene un registro de vendedor válido.");

        return await CreateSaleAsync(usuario, idCliente, "PRESENCIAL", request);
    }

    private async Task<PurchaseResult> CreateSaleAsync(AuthUser usuario, int idCliente, string origin, PurchaseRequest request)
    {
        if (!PaymentMethods.Contains(request.PaymentMethod))
            throw new InvalidOperationException("El método de pago no es válido.");
        if (request.Items == null || request.Items.Count == 0)
            throw new InvalidOperationException("La compra debe incluir al menos un producto.");
        if (request.Items.Any(i => i.AlfajorId <= 0 || i.PresentationId <= 0 || i.Quantity <= 0))
            throw new InvalidOperationException("Los productos y las cantidades deben ser válidos.");
        if (request.Items.GroupBy(i => new { i.AlfajorId, i.PresentationId }).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Cada alfajor y presentación debe aparecer una sola vez en la compra.");

        if (!await _db.Clientes.AnyAsync(c => c.IdPersona == idCliente))
            throw new InvalidOperationException("Cliente no válido.");

        var isPaidAtPointOfSale = origin == "PRESENCIAL" && request.PaymentMethod == "EFECTIVO";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var details = new List<DetalleOperacion>();
        var total = 0m;

        await using var transaction = await _db.Database.BeginTransactionAsync();
        foreach (var item in request.Items)
        {
            var presentation = await _db.AlfajoresPresentacion
                .AsNoTracking()
                .Include(ap => ap.Alfajor)
                .Include(ap => ap.Presentacion)
                .Where(ap => ap.IdAlfajor == item.AlfajorId && ap.IdPresentacion == item.PresentationId && ap.Alfajor.Activo)
                .Select(ap => new { ap.Precio, ap.Alfajor.Nombre, PresentationName = ap.Presentacion.Nombre })
                .FirstOrDefaultAsync();
            if (presentation == null)
                throw new InvalidOperationException($"No existe el alfajor/presentación solicitado ({item.AlfajorId}/{item.PresentationId}).");

            var stock = await _db.Stocks.AsNoTracking()
                .Where(s => s.IdAlfajor == item.AlfajorId && s.IdPresentacion == item.PresentationId)
                .Select(s => (int?)s.Cantidad)
                .FirstOrDefaultAsync();
            if (stock == null || stock.Value < item.Quantity)
                throw new SaleConflictException($"Stock insuficiente para {presentation.Nombre} ({presentation.PresentationName}).");

            var discounts = await _db.PromocionesAlfajor
                .Where(pa => pa.IdAlfajor == item.AlfajorId &&
                    pa.Promocion.Activa &&
                    pa.Promocion.FechaInicio <= today &&
                    pa.Promocion.FechaFin >= today)
                .Select(pa => pa.Promocion.PorcentajeDescuento)
                .ToListAsync();
            var discount = discounts.Count == 0 ? 0m : discounts.Max();
            var unitPrice = decimal.Round(presentation.Precio * (100m - discount) / 100m, 2, MidpointRounding.AwayFromZero);
            var subtotal = unitPrice * item.Quantity;
            total += subtotal;
            details.Add(new DetalleOperacion
            {
                IdAlfajor = item.AlfajorId,
                IdPresentacion = item.PresentationId,
                Cantidad = item.Quantity,
                PrecioUnitario = unitPrice,
                Subtotal = subtotal
            });
        }

        if (total <= 0)
            throw new InvalidOperationException("El total debe ser mayor a cero para generar un pago.");

        if (isPaidAtPointOfSale)
        {
            foreach (var detail in details)
            {
                var updated = await _db.Stocks
                    .Where(s => s.IdAlfajor == detail.IdAlfajor &&
                        s.IdPresentacion == detail.IdPresentacion &&
                        s.Cantidad >= detail.Cantidad)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(s => s.Cantidad, s => s.Cantidad - detail.Cantidad)
                        .SetProperty(s => s.UltimaActualizacion, DateTime.UtcNow));
                if (updated != 1)
                    throw new SaleConflictException("El stock cambió mientras se registraba la venta. Recargá y volvé a intentar.");
            }
        }

        var operation = new Operacion
        {
            IdCliente = idCliente,
            IdVendedor = origin == "PRESENCIAL" ? usuario.IdPersona : null,
            Origen = origin,
            Estado = isPaidAtPointOfSale ? "PAGADA" : "PENDIENTE",
            Total = total,
            Detalles = details
        };
        _db.Operaciones.Add(operation);
        await _db.SaveChangesAsync();

        _db.Pagos.Add(new Pago
        {
            IdOperacion = operation.IdOperacion,
            Metodo = request.PaymentMethod,
            Monto = total,
            Estado = isPaidAtPointOfSale ? "APROBADO" : "PENDIENTE"
        });

        string? invoiceNumber = null;
        if (isPaidAtPointOfSale)
        {
            invoiceNumber = $"FAC-P-{DateTime.UtcNow:yyyyMMdd}-{operation.IdOperacion}";
            _db.Facturas.Add(new Factura
            {
                IdOperacion = operation.IdOperacion,
                NumeroFactura = invoiceNumber,
                Total = total
            });
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new PurchaseResult(operation.IdOperacion, total, operation.Estado, invoiceNumber);
    }
}
