namespace AlfajorgeBackend.Services;

public static class OperationStatusRules
{
    public static bool CanTransition(string current, string next) =>
        (current, next) switch
        {
            ("PAGADA", "PREPARACION") => true,
            ("PREPARACION", "ENVIADA") => true,
            ("ENVIADA", "ENTREGADA") => true,
            _ => false
        };
}
