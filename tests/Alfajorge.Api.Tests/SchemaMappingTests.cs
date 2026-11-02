using AlfajorgeBackend.Data;
using AlfajorgeBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Alfajorge.Api.Tests;

public class SchemaMappingTests
{
    private static readonly HashSet<string> OfficialTables =
    [
        "persona", "cliente", "empleado", "vendedor", "tipo_alfajor", "sabor", "cobertura",
        "presentacion", "alfajor", "alfajor_presentacion", "stock", "operacion",
        "detalle_operacion", "pago", "factura", "promocion", "promocion_alfajor"
    ];

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void MapsExactlyTheOfficialSeventeenTables()
    {
        using var context = CreateContext();
        var mappedTables = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(name => name != null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(OfficialTables.SetEquals(mappedTables));
        Assert.Equal(17, mappedTables.Count);
    }

    [Fact]
    public void KeepsAllOfficialCompositePrimaryKeysAndDetailForeignKey()
    {
        using var context = CreateContext();
        var model = context.Model;

        AssertPrimaryKey(model, typeof(AlfajorPresentacion), nameof(AlfajorPresentacion.IdAlfajor), nameof(AlfajorPresentacion.IdPresentacion));
        AssertPrimaryKey(model, typeof(Stock), nameof(Stock.IdAlfajor), nameof(Stock.IdPresentacion));
        AssertPrimaryKey(model, typeof(PromocionAlfajor), nameof(PromocionAlfajor.IdPromocion), nameof(PromocionAlfajor.IdAlfajor));

        var detailEntity = model.FindEntityType(typeof(DetalleOperacion))!;
        var productPresentationForeignKey = detailEntity.GetForeignKeys().Single(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(AlfajorPresentacion));
        Assert.Equal(
            new[] { nameof(DetalleOperacion.IdAlfajor), nameof(DetalleOperacion.IdPresentacion) },
            productPresentationForeignKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.NoAction, productPresentationForeignKey.DeleteBehavior);

        var operationForeignKey = detailEntity.GetForeignKeys().Single(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Operacion));
        Assert.Equal(DeleteBehavior.Cascade, operationForeignKey.DeleteBehavior);
    }

    [Fact]
    public void PreservesSellerAndInvoiceRelationshipRules()
    {
        using var context = CreateContext();
        var model = context.Model;

        var operationEntity = model.FindEntityType(typeof(Operacion))!;
        var sellerForeignKey = operationEntity.GetForeignKeys().Single(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Vendedor));
        Assert.True(sellerForeignKey.Properties.Single().IsNullable);
        Assert.Equal(DeleteBehavior.NoAction, sellerForeignKey.DeleteBehavior);

        var invoiceEntity = model.FindEntityType(typeof(Factura))!;
        var operationInvoiceForeignKey = invoiceEntity.GetForeignKeys().Single();
        Assert.Equal(nameof(Factura.IdOperacion), operationInvoiceForeignKey.Properties.Single().Name);
        Assert.Equal(DeleteBehavior.NoAction, operationInvoiceForeignKey.DeleteBehavior);
        Assert.Contains(invoiceEntity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(Factura.IdOperacion));
        Assert.Contains(invoiceEntity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(Factura.NumeroFactura));
    }

    private static void AssertPrimaryKey(IModel model, Type entityType, params string[] propertyNames)
    {
        var key = model.FindEntityType(entityType)!.FindPrimaryKey()!;
        Assert.Equal(propertyNames, key.Properties.Select(property => property.Name));
    }
}
