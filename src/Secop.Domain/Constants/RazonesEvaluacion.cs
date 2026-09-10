namespace Secop.Domain.Constants;

public static class RazonesEvaluacion
{
    public const string RupVencido = "RUP vencido; el proveedor no puede participar";
    public const string SoloEsal = "Proceso restringido a ESAL; el proveedor no tiene perfil ESAL";
    public const string CapacidadInsuficiente = "Capacidad financiera insuficiente; requiere revisión manual";
    public const string Elegible = "RUP vigente y capacidad financiera suficiente";
    public const string FechaCierreDesconocida = "Fecha de cierre ausente o no utilizable; requiere revisión manual";
    public const string PlazoVencido = "La fecha de cierre ya transcurrió";
    public const string PosibleRegimenEspecial = "Posible régimen especial de contratación; requiere revisión manual";

    public static string PlazoUrgente(int diasHabiles) =>
        $"Quedan {diasHabiles} días hábiles; requiere atención urgente";

    public static string PlazoAccionable(int diasHabiles) =>
        $"Quedan {diasHabiles} días hábiles para el cierre";
}
