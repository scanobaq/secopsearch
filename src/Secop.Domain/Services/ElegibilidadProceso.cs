using Secop.Domain.Enums;

namespace Secop.Domain.Services;

public static class ElegibilidadProceso
{
    public const decimal PresupuestoMinimoCop = 60_000_000m;

    public static bool EsElegible(ModalidadContrato modalidad, decimal? presupuestoCop) =>
        // Temporarily disabled: direct-contracting processes remain eligible when the amount is valid.
        // modalidad != ModalidadContrato.ContratacionDirecta &&
        presupuestoCop is > 0 and >= PresupuestoMinimoCop;
}
