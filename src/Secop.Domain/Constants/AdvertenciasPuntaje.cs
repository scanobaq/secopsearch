namespace Secop.Domain.Constants;

public static class AdvertenciasPuntaje
{
    public const string RupVencido             = "RUP vencido — propuesta sería rechazada automáticamente";
    public const string PlazoMuyCorto          = "Plazo muy corto — posible proceso dirigido";
    public const string EncontradoPorTexto     = "Encontrado por coincidencia de palabra clave (sin código UNSPSC declarado)";
    public const string PosibleRegimenEspecial = "Posible régimen especial de contratación — requiere revisión manual";
    public const string SoloEsal               = "Solo aplica a ESAL (Decreto 092 de 2017) — proveedor no tiene perfil ESAL";
    public const string ConOfertas             = "Proceso con ofertas dentro de régimen especial publicitario";
}
