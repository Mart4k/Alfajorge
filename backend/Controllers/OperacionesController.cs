using AlfajorgeBackend.Data;
using AlfajorgeBackend.DTOs;
using AlfajorgeBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlfajorgeBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OperacionesController : ControllerBase
{
    private readonly OperacionService _operacionService;
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public OperacionesController(OperacionService operacionService, AppDbContext db, AuthService auth)
    {
        _operacionService = operacionService;
        _db = db;
        _auth = auth;
    }

    [HttpPost("web")]
    [Authorize(Policy = "Cliente")]
    public async Task<IActionResult> CrearCompraWeb([FromBody] PurchaseRequest request)
    {
        var user = await _auth.GetAuthenticatedUserAsync(User);
        if (user == null || user.Rol != "CLIENTE") return Unauthorized(new { error = "El cliente debe iniciar sesión." });

        try
        {
            var resultado = await _operacionService.CrearCompraWebAsync(user, request);
            return Ok(resultado);
        }
        catch (SaleConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("presencial")]
    [Authorize(Policy = "Vendedor")]
    public async Task<IActionResult> CrearVentaPresencial([FromQuery] int idCliente, [FromBody] PurchaseRequest request)
    {
        var user = await _auth.GetAuthenticatedUserAsync(User);
        if (user == null || user.Rol != "VENDEDOR") return Forbid();

        try
        {
            var resultado = await _operacionService.CrearVentaPresencialAsync(user, idCliente, request);
            return Ok(resultado);
        }
        catch (SaleConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    [Authorize(Policy = "Administrativo")]
    public async Task<IActionResult> GetAll()
    {
        var user = await _auth.GetAuthenticatedUserAsync(User);
        if (user == null || user.Rol != "ADMINISTRATIVO") return Forbid();

        var operaciones = await _db.Operaciones
            .Include(o => o.Cliente).ThenInclude(c => c.Persona)
            .OrderByDescending(o => o.Fecha)
            .ToListAsync();

        return Ok(operaciones.Select(o => new OperationDto(
            o.IdOperacion, o.Fecha, o.Origen, o.Estado, o.Total,
            $"{o.Cliente.Persona.Nombre} {o.Cliente.Persona.Apellido}")));
    }

    [HttpGet("mias")]
    [Authorize]
    public async Task<IActionResult> GetMias()
    {
        var user = await _auth.GetAuthenticatedUserAsync(User);
        if (user == null) return Unauthorized();

        if (user.Rol == "CLIENTE")
        {
            var operaciones = await _db.Operaciones
                .Include(o => o.Cliente).ThenInclude(c => c.Persona)
                .Where(o => o.IdCliente == user.IdPersona)
                .OrderByDescending(o => o.Fecha)
                .ToListAsync();

            return Ok(operaciones.Select(o => new OperationDto(o.IdOperacion, o.Fecha, o.Origen, o.Estado, o.Total,
                $"{o.Cliente.Persona.Nombre} {o.Cliente.Persona.Apellido}")));
        }

        if (user.Rol == "VENDEDOR" || user.Rol == "ADMINISTRATIVO")
        {
            var operaciones = await _db.Operaciones
                .Include(o => o.Cliente).ThenInclude(c => c.Persona)
                .Where(o => o.IdVendedor == user.IdPersona || user.Rol == "ADMINISTRATIVO")
                .OrderByDescending(o => o.Fecha)
                .ToListAsync();

            return Ok(operaciones.Select(o => new OperationDto(o.IdOperacion, o.Fecha, o.Origen, o.Estado, o.Total,
                $"{o.Cliente.Persona.Nombre} {o.Cliente.Persona.Apellido}")));
        }

        return Forbid();
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _auth.GetAuthenticatedUserAsync(User);
        if (user == null) return Unauthorized();

        var operacion = await _db.Operaciones
            .Include(o => o.Detalles)
                .ThenInclude(d => d.AlfajorPresentacion)
                    .ThenInclude(ap => ap.Alfajor)
            .Include(o => o.Detalles)
                .ThenInclude(d => d.AlfajorPresentacion)
                    .ThenInclude(ap => ap.Presentacion)
            .Include(o => o.Cliente).ThenInclude(c => c.Persona)
            .FirstOrDefaultAsync(o => o.IdOperacion == id);

        if (operacion == null) return NotFound();

        if (user.Rol == "CLIENTE" && operacion.IdCliente != user.IdPersona) return Forbid();
        if (user.Rol == "VENDEDOR" && operacion.IdVendedor != user.IdPersona) return Forbid();

        return Ok(new OperationDetailDto(
            operacion.IdOperacion, operacion.Fecha, operacion.Origen, operacion.Estado, operacion.Total,
            $"{operacion.Cliente.Persona.Nombre} {operacion.Cliente.Persona.Apellido}",
            operacion.Detalles.Select(detail => new OperationLineDto(
                detail.AlfajorPresentacion.Alfajor.Nombre,
                detail.AlfajorPresentacion.Presentacion.Nombre,
                detail.Cantidad,
                detail.PrecioUnitario,
                detail.Subtotal)).ToList()));
    }

    [HttpPut("{id:int}/estado")]
    [Authorize(Policy = "Administrativo")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeOperationStatusRequest request)
    {
        var operation = await _db.Operaciones.FirstOrDefaultAsync(item => item.IdOperacion == id);
        if (operation == null) return NotFound();
        if (!OperationStatusRules.CanTransition(operation.Estado, request.Status))
            return Conflict(new { error = $"No se permite cambiar la operación de {operation.Estado} a {request.Status}." });

        var updated = await _db.Operaciones
            .Where(item => item.IdOperacion == id && item.Estado == operation.Estado)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Estado, request.Status));
        return updated == 1
            ? NoContent()
            : Conflict(new { error = "La operación cambió de estado y debe volver a consultarse." });
    }

    [HttpPost("{id:int}/cancelar")]
    [Authorize]
    public async Task<IActionResult> CancelPending(int id)
    {
        var user = await _auth.GetAuthenticatedUserAsync(User);
        if (user == null) return Unauthorized();

        var operation = await _db.Operaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdOperacion == id);
        if (operation == null) return NotFound();

        var canManageOperation = user.Rol == "ADMINISTRATIVO" ||
            user.Rol == "CLIENTE" && operation.IdCliente == user.IdPersona ||
            user.Rol == "VENDEDOR" && operation.IdVendedor == user.IdPersona;
        if (!canManageOperation) return Forbid();
        if (operation.Estado != "PENDIENTE")
            return Conflict(new { error = "Solo se pueden cancelar operaciones pendientes. Las operaciones pagadas requieren un proceso real de devolución y reembolso." });

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var cancelled = await _db.Operaciones
            .Where(item => item.IdOperacion == id && item.Estado == "PENDIENTE")
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Estado, "CANCELADA"));
        if (cancelled != 1)
            return Conflict(new { error = "La operación ya cambió de estado y no se puede cancelar." });

        await _db.Pagos
            .Where(payment => payment.IdOperacion == id && payment.Estado == "PENDIENTE")
            .ExecuteUpdateAsync(setters => setters.SetProperty(payment => payment.Estado, "RECHAZADO"));

        await transaction.CommitAsync();
        return NoContent();
    }
}
