using AlfajorgeBackend.Data;
using AlfajorgeBackend.Models;
using AlfajorgeBackend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Alfajorge.Api.Tests;

public class LocalDatabaseInitializerTests
{
    [Fact]
    public async Task CreatesLocalSchemaSeedsLookupsAndBootstrapsAdministratorIdempotently()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ALFAJORGE_INITIAL_ADMIN_EMAIL"] = "admin@example.com",
                ["ALFAJORGE_INITIAL_ADMIN_PASSWORD"] = "local-admin-password"
            })
            .Build();

        await using var db = new AppDbContext(options);
        await LocalDatabaseInitializer.InitializeAsync(db, configuration);

        Assert.Equal(2, await db.TiposAlfajor.CountAsync());
        Assert.Equal(3, await db.Sabores.CountAsync());
        Assert.Equal(3, await db.Coberturas.CountAsync());
        Assert.Equal(3, await db.Presentaciones.CountAsync());
        var administrator = await db.Empleados.Include(employee => employee.Persona).SingleAsync();
        Assert.Equal("ADMINISTRATIVO", administrator.Cargo);
        Assert.Equal("admin@example.com", administrator.Persona.Email);
        Assert.True(AuthService.VerifyPassword("local-admin-password", administrator.Persona.PasswordHash));

        await LocalDatabaseInitializer.InitializeAsync(db, configuration);
        Assert.Single(await db.Empleados.ToListAsync());
        Assert.Equal(2, await db.TiposAlfajor.CountAsync());
    }

    [Fact]
    public async Task CreatesOnlyOneAdministratorAndStoresItsPasswordAsAHash()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var auth = new AuthService(db, new ConfigurationBuilder().Build());

        var admin = await auth.CreateInitialAdministratorAsync(
            "12345678", "Admin", "Local", "admin@example.com", "local-admin-password");
        var secondAdmin = await auth.CreateInitialAdministratorAsync(
            "87654321", "Other", "Local", "other@example.com", "another-admin-password");

        Assert.NotNull(admin);
        Assert.Equal("admin@example.com", admin.Email);
        Assert.True(AuthService.VerifyPassword("local-admin-password", admin.PasswordHash));
        Assert.Null(secondAdmin);
        Assert.Single(await db.Empleados.ToListAsync());
    }

    [Fact]
    public async Task PreventsDuplicateActiveAlfajoresIgnoringNameCaseAndWhitespace()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await LocalDatabaseInitializer.InitializeAsync(db, new ConfigurationBuilder().Build());
        var typeId = await db.TiposAlfajor.Select(item => item.IdTipo).FirstAsync();
        var flavorId = await db.Sabores.Select(item => item.IdSabor).FirstAsync();
        var coverageId = await db.Coberturas.Select(item => item.IdCobertura).FirstAsync();

        db.Alfajores.Add(new Alfajor
        {
            Nombre = "Dulce Especial",
            IdTipo = typeId,
            IdSabor = flavorId,
            IdCobertura = coverageId
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        db.Alfajores.Add(new Alfajor
        {
            Nombre = "  DULCE ESPECIAL ",
            IdTipo = typeId,
            IdSabor = flavorId,
            IdCobertura = coverageId
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ArchivesExistingDuplicateAlfajoresWithoutDeletingTheirRecords()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var type = new TipoAlfajor { Nombre = "Clásico" };
        var flavor = new Sabor { Nombre = "Dulce de leche" };
        var coverage = new Cobertura { Nombre = "Chocolate negro" };
        db.AddRange(type, flavor, coverage);
        await db.SaveChangesAsync();
        db.Alfajores.AddRange(
            new Alfajor
            {
                Nombre = "Dulce Especial",
                IdTipo = type.IdTipo,
                IdSabor = flavor.IdSabor,
                IdCobertura = coverage.IdCobertura
            },
            new Alfajor
            {
                Nombre = " dulce especial ",
                IdTipo = type.IdTipo,
                IdSabor = flavor.IdSabor,
                IdCobertura = coverage.IdCobertura
            });
        await db.SaveChangesAsync();

        await LocalDatabaseInitializer.InitializeAsync(db, new ConfigurationBuilder().Build());

        Assert.Equal(2, await db.Alfajores.CountAsync());
        Assert.Single(await db.Alfajores.Where(item => item.Activo).ToListAsync());
        Assert.Single(await db.Alfajores.Where(item => !item.Activo).ToListAsync());
    }

}
