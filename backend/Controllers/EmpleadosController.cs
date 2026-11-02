using AlfajorgeBackend.Data;
using AlfajorgeBackend.DTOs;
using AlfajorgeBackend.Models;
using AlfajorgeBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AlfajorgeBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Administrativo")]
public class EmpleadosController : ControllerBase
{
    private readonly AppDbContext _db;

    public EmpleadosController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetEmpleados()
    {
        var employees = await _db.Empleados
            .AsNoTracking()
            .Include(e => e.Persona)
            .Include(e => e.Vendedor)
            .OrderBy(e => e.Persona.Apellido)
            .ThenBy(e => e.Persona.Nombre)
            .Select(e => new EmployeeDto(
                e.IdPersona,
                e.Persona.Dni,
                e.Persona.Nombre,
                e.Persona.Apellido,
                e.Persona.Email,
                e.Persona.Telefono,
                e.Cargo,
                e.Vendedor != null))
            .ToListAsync();

        return Ok(employees);
    }

    [HttpPost]
    public async Task<IActionResult> CrearEmpleado([FromBody] CreateEmployeeRequest request)
    {
        if (request.Cargo is not ("VENDEDOR" or "ADMINISTRATIVO"))
            return BadRequest(new { error = "Cargo inválido." });
        if (string.IsNullOrWhiteSpace(request.Dni) || string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.LastName) || string.IsNullOrWhiteSpace(request.Email) ||
            request.Password.Length < 10 || request.Dni.Length > 20 ||
            request.Name.Length > 60 || request.LastName.Length > 60 ||
            request.Email.Length > 150 || !new EmailAddressAttribute().IsValid(request.Email) ||
            request.Phone?.Length > 30)
            return BadRequest(new { error = "Complete todos los campos obligatorios y use una contraseña de al menos 10 caracteres." });

        var dni = request.Dni.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Personas.AnyAsync(p => p.Dni == dni || p.Email.ToLower() == email))
            return Conflict(new { error = "DNI o email ya existen." });

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var persona = new Persona
        {
            Dni = dni,
            Nombre = request.Name.Trim(),
            Apellido = request.LastName.Trim(),
            Email = email,
            PasswordHash = AuthService.HashPassword(request.Password),
            Telefono = request.Phone?.Trim(),
            Activo = true
        };
        _db.Personas.Add(persona);
        await _db.SaveChangesAsync();

        var employee = new Empleado { IdPersona = persona.IdPersona, Cargo = request.Cargo };
        _db.Empleados.Add(employee);
        await _db.SaveChangesAsync();

        if (request.Cargo == "VENDEDOR")
        {
            _db.Vendedores.Add(new Vendedor { IdPersona = persona.IdPersona });
            await _db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return Created($"/api/empleados/{persona.IdPersona}", new EmployeeDto(
            persona.IdPersona, persona.Dni, persona.Nombre, persona.Apellido, persona.Email, persona.Telefono,
            employee.Cargo, request.Cargo == "VENDEDOR"));
    }
}
