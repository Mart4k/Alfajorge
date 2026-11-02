using AlfajorgeBackend.Services;
using Xunit;

namespace Alfajorge.Api.Tests;

public class BusinessRuleTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1250.50, true)]
    [InlineData(9999999999.99, true)]
    [InlineData(-0.01, false)]
    [InlineData(10000000000, false)]
    [InlineData(1.001, false)]
    public void ValidatesPricesAgainstDatabaseNumericRules(decimal price, bool expected) =>
        Assert.Equal(expected, PresentationPriceRules.IsValid(price));

    [Theory]
    [InlineData("PAGADA", "PREPARACION", true)]
    [InlineData("PREPARACION", "ENVIADA", true)]
    [InlineData("ENVIADA", "ENTREGADA", true)]
    [InlineData("PENDIENTE", "PAGADA", false)]
    [InlineData("PENDIENTE", "CANCELADA", false)]
    [InlineData("PAGADA", "CANCELADA", false)]
    [InlineData("ENTREGADA", "PREPARACION", false)]
    public void OnlyAllowsDocumentedOperationStatusTransitions(string current, string next, bool expected) =>
        Assert.Equal(expected, OperationStatusRules.CanTransition(current, next));
}
