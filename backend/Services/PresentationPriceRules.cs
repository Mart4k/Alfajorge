namespace AlfajorgeBackend.Services;

public static class PresentationPriceRules
{
    public static bool IsValid(decimal price) =>
        price >= 0 && price <= 9_999_999_999.99m && decimal.Round(price, 2) == price;
}
