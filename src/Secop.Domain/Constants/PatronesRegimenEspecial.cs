namespace Secop.Domain.Constants;

/// <summary>
/// Patrones de nombre de entidad que sugieren régimen especial de contratación
/// (universidades públicas, empresas sociales del estado, empresas de servicios públicos,
/// empresas industriales y comerciales del estado, entidades financieras estatales).
/// Solo se usan para ADVERTIR — nunca para descartar automáticamente (ver SPEC-04).
/// </summary>
public static class PatronesRegimenEspecial
{
    public static readonly string[] Patrones =
    [
        "UNIVERSIDAD",
        "E.S.E",
        "ESE ",
        "E.S.P",
        "ESP ",
        "E.I.C.E",
        "EICE ",
        "FINANCIERA ESTATAL",
        "FIDUCIARIA",
        "FINDETER",
        "FINAGRO",
        "BANCOLDEX",
        "FONDO NACIONAL DEL AHORRO"
    ];
}
