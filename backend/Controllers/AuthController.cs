using AlfajorgeBackend.DTOs;
using AlfajorgeBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Net;
using AlfajorgeBackend.Data;

namespace AlfajorgeBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly AppDbContext _db;

    public AuthController(AuthService authService, AppDbContext db)
    {
        _authService = authService;
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet("setup-status")]
    public async Task<IActionResult> GetSetupStatus()
    {
        if (!IsLocalRequest())
        {
            return NotFound();
        }

        return Ok(new { setupRequired = !await _db.Empleados.AnyAsync() });
    }

    [AllowAnonymous]
    [HttpPost("setup-admin")]
    public async Task<IActionResult> SetupAdministrator([FromBody] InitialAdministratorRequest request)
    {
        if (!IsLocalRequest())
        {
            return NotFound();
        }
        if (string.IsNullOrWhiteSpace(request.Dni) || string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.LastName) || string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 10 ||
            request.Dni.Length > 20 || request.Name.Length > 60 || request.LastName.Length > 60 ||
            request.Email.Length > 150 || !new EmailAddressAttribute().IsValid(request.Email))
        {
            return BadRequest(new { error = "Completá todos los campos con un email válido y una contraseña de al menos 10 caracteres." });
        }

        var persona = await _authService.CreateInitialAdministratorAsync(
            request.Dni, request.Name, request.LastName, request.Email, request.Password);
        if (persona == null)
        {
            return Conflict(new { error = "La configuración inicial ya se completó o el DNI/correo ya está registrado." });
        }

        return CreatedAtAction(nameof(Me), null, new SessionDto(
            _authService.GenerateToken(persona, "ADMINISTRATIVO"),
            new SessionUserDto(persona.IdPersona, persona.Nombre, persona.Apellido, persona.Email, "ADMINISTRATIVO")));
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterClientRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Dni) || string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.LastName) || string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Faltan datos obligatorios." });
        }

        if (request.Password.Length < 10)
        {
            return BadRequest(new { error = "La contraseña debe tener al menos 10 caracteres." });
        }
        if (request.Dni.Length > 20 || request.Name.Length > 60 || request.LastName.Length > 60 ||
            request.Email.Length > 150 || !new EmailAddressAttribute().IsValid(request.Email) ||
            request.Phone?.Length > 30 || request.Address?.Length > 200 ||
            request.City?.Length > 100 || request.PostalCode?.Length > 15)
        {
            return BadRequest(new { error = "Uno o más datos superan el formato o la longitud permitidos." });
        }

        var persona = await _authService.RegistrarClienteAsync(
            request.Dni,
            request.Name,
            request.LastName,
            request.Email,
            request.Password,
            request.Phone,
            request.Address,
            request.City,
            request.PostalCode);
        if (persona == null)
        {
            return Conflict(new { error = "El cliente ya existe o los datos están duplicados." });
        }

        var role = await _authService.GetRoleAsync(persona.IdPersona);
        return CreatedAtAction(nameof(Me), null, new SessionDto(
            _authService.GenerateToken(persona, role),
            new SessionUserDto(persona.IdPersona, persona.Nombre, persona.Apellido, persona.Email, role)));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Email y contraseña son obligatorios." });
        }

        var persona = await _authService.LoginAsync(request.Email, request.Password);
        if (persona == null)
        {
            return Unauthorized(new { error = "Credenciales inválidas." });
        }

        var role = await _authService.GetRoleAsync(persona.IdPersona);
        if (role is not ("CLIENTE" or "VENDEDOR" or "ADMINISTRATIVO"))
        {
            return Unauthorized(new { error = "Usuario sin rol válido." });
        }

        return Ok(new SessionDto(
            _authService.GenerateToken(persona, role),
            new SessionUserDto(persona.IdPersona, persona.Nombre, persona.Apellido, persona.Email, role)));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _authService.GetAuthenticatedUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { error = "No autenticado." });
        }

        return Ok(new SessionUserDto(user.IdPersona, user.Nombre, user.Apellido, user.Email, user.Rol));
    }

    private bool IsLocalRequest()
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        return address != null && (IPAddress.IsLoopback(address) ||
            address.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(address.MapToIPv4()));
    }
}
