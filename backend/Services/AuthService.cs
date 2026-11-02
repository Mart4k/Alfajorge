using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using AlfajorgeBackend.Data;
using AlfajorgeBackend.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace AlfajorgeBackend.Services;

public sealed class DatabaseRoleClaimsTransformation(AppDbContext db) : IClaimsTransformation
{
    private const string RefreshedClaim = "alfajorge:role-refreshed";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated ||
            identity.HasClaim(RefreshedClaim, "true"))
        {
            return principal;
        }

        var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, NumberStyles.None, CultureInfo.InvariantCulture, out var idPersona))
        {
            return principal;
        }

        var role = await db.Personas.AsNoTracking()
            .Where(p => p.IdPersona == idPersona && p.Activo)
            .Select(p => p.Empleado != null ? p.Empleado.Cargo : p.Cliente != null ? "CLIENTE" : null)
            .FirstOrDefaultAsync();

        foreach (var claim in identity.FindAll(identity.RoleClaimType).ToArray())
        {
            identity.RemoveClaim(claim);
        }
        if (role is "CLIENTE" or "VENDEDOR" or "ADMINISTRATIVO")
        {
            identity.AddClaim(new Claim(identity.RoleClaimType, role));
        }
        identity.AddClaim(new Claim(RefreshedClaim, "true"));
        return principal;
    }
}

public sealed record AuthUser(int IdPersona, string Nombre, string Apellido, string Email, string Rol);

public class AuthService
{
    private static readonly PasswordHasher<Persona> PasswordHasher = new();
    private static readonly SemaphoreSlim InitialAdministratorLock = new(1, 1);
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public AuthService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<Persona?> RegistrarClienteAsync(string dni, string nombre, string apellido, string email, string password, string? telefono, string? direccion, string? localidad, string? codigoPostal)
    {
        dni = dni.Trim();
        email = email.Trim().ToLowerInvariant();
        nombre = nombre.Trim();
        apellido = apellido.Trim();

        if (string.IsNullOrWhiteSpace(dni) || string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(apellido) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        if (await _db.Personas.AnyAsync(p => p.Dni == dni || p.Email.ToLower() == email))
        {
            return null;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var persona = new Persona
        {
            Dni = dni,
            Nombre = nombre,
            Apellido = apellido,
            Email = email,
            PasswordHash = HashPassword(password),
            Telefono = telefono?.Trim(),
            Activo = true
        };

        _db.Personas.Add(persona);
        await _db.SaveChangesAsync();

        _db.Clientes.Add(new Cliente
        {
            IdPersona = persona.IdPersona,
            Direccion = EmptyToNull(direccion),
            Localidad = EmptyToNull(localidad),
            CodigoPostal = EmptyToNull(codigoPostal)
        });
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return persona;
    }

    public async Task<Persona?> CreateInitialAdministratorAsync(string dni, string nombre, string apellido, string email, string password)
    {
        await InitialAdministratorLock.WaitAsync();
        try
        {
            if (await _db.Empleados.AnyAsync())
            {
                return null;
            }

            dni = dni.Trim();
            email = email.Trim().ToLowerInvariant();
            if (await _db.Personas.AnyAsync(persona => persona.Dni == dni || persona.Email.ToLower() == email))
            {
                return null;
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();
            if (await _db.Empleados.AnyAsync())
            {
                return null;
            }

            var persona = new Persona
            {
                Dni = dni,
                Nombre = nombre.Trim(),
                Apellido = apellido.Trim(),
                Email = email,
                PasswordHash = HashPassword(password),
                Activo = true
            };
            _db.Personas.Add(persona);
            await _db.SaveChangesAsync();
            _db.Empleados.Add(new Empleado { IdPersona = persona.IdPersona, Cargo = "ADMINISTRATIVO" });
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return persona;
        }
        finally
        {
            InitialAdministratorLock.Release();
        }
    }

    public async Task<Persona?> LoginAsync(string email, string password)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.Email.ToLower() == normalizedEmail && p.Activo);
        if (persona == null || !VerifyPassword(password, persona.PasswordHash))
        {
            return null;
        }

        if (IsLegacyPasswordHash(persona.PasswordHash))
        {
            persona.PasswordHash = HashPassword(password);
            await _db.SaveChangesAsync();
        }

        return persona;
    }

    public async Task<AuthUser?> GetAuthenticatedUserAsync(ClaimsPrincipal principal)
    {
        var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, NumberStyles.None, CultureInfo.InvariantCulture, out var idPersona))
        {
            return null;
        }

        var persona = await _db.Personas.AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdPersona == idPersona && p.Activo);
        if (persona == null)
        {
            return null;
        }

        var rol = await GetRoleAsync(idPersona);
        return string.IsNullOrWhiteSpace(rol)
            ? null
            : new AuthUser(persona.IdPersona, persona.Nombre, persona.Apellido, persona.Email, rol);
    }

    public async Task<string> GetRoleAsync(int personaId)
    {
        var empleado = await _db.Empleados.AsNoTracking().FirstOrDefaultAsync(e => e.IdPersona == personaId);
        if (empleado != null)
        {
            return empleado.Cargo;
        }

        var cliente = await _db.Clientes.AsNoTracking().AnyAsync(c => c.IdPersona == personaId);
        return cliente ? "CLIENTE" : "";
    }

    public string GenerateToken(Persona persona, string role)
    {
        var key = _configuration["ALFAJORGE_TOKEN_SECRET"]
            ?? throw new InvalidOperationException("Falta configurar ALFAJORGE_TOKEN_SECRET.");
        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Falta configurar Jwt:Issuer.");
        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Falta configurar Jwt:Audience.");
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, persona.IdPersona.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, persona.Nombre),
            new Claim(ClaimTypes.Surname, persona.Apellido),
            new Claim(ClaimTypes.Email, persona.Email),
            new Claim(ClaimTypes.Role, role)
        };
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string HashPassword(string password) =>
        PasswordHasher.HashPassword(new Persona(), password);

    public static bool VerifyPassword(string providedPassword, string storedHash)
    {
        if (IsLegacyPasswordHash(storedHash))
        {
            var computed = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(providedPassword));
            var expected = Convert.FromHexString(storedHash);
            return CryptographicOperations.FixedTimeEquals(computed, expected);
        }

        return PasswordHasher.VerifyHashedPassword(new Persona(), storedHash, providedPassword)
            != PasswordVerificationResult.Failed;
    }

    private static bool IsLegacyPasswordHash(string hash) =>
        hash.Length == 64 && hash.All(Uri.IsHexDigit);

    private static string? EmptyToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
