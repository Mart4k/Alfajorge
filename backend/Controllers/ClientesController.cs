using AlfajorgeBackend.Data;
using AlfajorgeBackend.DTOs;
using AlfajorgeBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlfajorgeBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Empleado")]
public class ClientesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public ClientesController(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    [HttpGet]
    public async Task<IActionResult> GetClientes()
    {
        var user = await _auth.GetAuthenticatedUserAsync(User);
        if (user == null || (user.Rol != "ADMINISTRATIVO" && user.Rol != "VENDEDOR")) return Forbid();

        var clientes = await _db.Clientes
            .AsNoTracking()
            .Include(c => c.Persona)
            .OrderBy(c => c.Persona.Apellido)
            .ThenBy(c => c.Persona.Nombre)
            .Select(c => new ClientDto(
                c.IdPersona,
                c.Persona.Dni,
                c.Persona.Nombre,
                c.Persona.Apellido,
                c.Persona.Email,
                c.Persona.Telefono,
                c.Direccion,
                c.Localidad,
                c.CodigoPostal))
            .ToListAsync();

        return Ok(clientes);
    }
}
