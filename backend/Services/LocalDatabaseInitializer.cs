using AlfajorgeBackend.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using AlfajorgeBackend.Models;

namespace AlfajorgeBackend.Services;

public static class LocalDatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, IConfiguration configuration)
    {
        await db.Database.EnsureCreatedAsync();
        var archivedDuplicates = await db.Database.ExecuteSqlRawAsync(
            "UPDATE alfajor SET activo = 0 WHERE activo = 1 AND id_alfajor NOT IN (" +
            "SELECT MIN(id_alfajor) FROM alfajor WHERE activo = 1 " +
            "GROUP BY LOWER(TRIM(nombre)), id_tipo, id_sabor, id_cobertura)");
        if (archivedDuplicates > 0)
        {
            Console.WriteLine(
                $"Se archivaron {archivedDuplicates} alfajores duplicados preexistentes; se conservaron sus registros.");
        }

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_alfajor_unique_active_variant " +
            "ON alfajor (LOWER(TRIM(nombre)), id_tipo, id_sabor, id_cobertura) WHERE activo = 1");

        if (!await db.TiposAlfajor.AnyAsync())
        {
            db.TiposAlfajor.AddRange(
                new TipoAlfajor { Nombre = "Clásico" },
                new TipoAlfajor { Nombre = "Premium" });
        }
        if (!await db.Sabores.AnyAsync())
        {
            db.Sabores.AddRange(
                new Sabor { Nombre = "Dulce de leche" },
                new Sabor { Nombre = "Chocolate" },
                new Sabor { Nombre = "Frutilla" });
        }
        if (!await db.Coberturas.AnyAsync())
        {
            db.Coberturas.AddRange(
                new Cobertura { Nombre = "Chocolate negro" },
                new Cobertura { Nombre = "Chocolate blanco" },
                new Cobertura { Nombre = "Sin cobertura" });
        }
        if (!await db.Presentaciones.AnyAsync())
        {
            db.Presentaciones.AddRange(
                new Presentacion { Nombre = "Unidad", CantidadUnidades = 1 },
                new Presentacion { Nombre = "Caja x 6", CantidadUnidades = 6 },
                new Presentacion { Nombre = "Caja x 12", CantidadUnidades = 12 });
        }

        await db.SaveChangesAsync();
        await CreateInitialAdministratorAsync(db, configuration);
    }

    private static async Task CreateInitialAdministratorAsync(AppDbContext db, IConfiguration configuration)
    {
        if (await db.Empleados.AnyAsync())
        {
            return;
        }

        var email = configuration["ALFAJORGE_INITIAL_ADMIN_EMAIL"];
        var password = configuration["ALFAJORGE_INITIAL_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) ||
            password.Length < 10 || !new EmailAddressAttribute().IsValid(email))
        {
            throw new InvalidOperationException(
                "Para crear el administrador inicial configurá un email válido y una contraseña de al menos 10 caracteres en ALFAJORGE_INITIAL_ADMIN_EMAIL y ALFAJORGE_INITIAL_ADMIN_PASSWORD.");
        }

        email = email.Trim().ToLowerInvariant();
        const string dni = "LOCAL-ADMIN";
        if (await db.Personas.AnyAsync(persona => persona.Dni == dni || persona.Email.ToLower() == email))
        {
            throw new InvalidOperationException(
                "No se pudo crear el administrador inicial porque su email o DNI ya está registrado.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var person = new Persona
        {
            Dni = dni,
            Nombre = "Administrador",
            Apellido = "Local",
            Email = email,
            PasswordHash = AuthService.HashPassword(password),
            Activo = true
        };
        db.Personas.Add(person);
        await db.SaveChangesAsync();
        db.Empleados.Add(new Empleado { IdPersona = person.IdPersona, Cargo = "ADMINISTRATIVO" });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
