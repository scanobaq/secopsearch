using Secop.Domain.Enums;

namespace Secop.Domain.Entities;

public class Proceso
{
    public string Id { get; private set; }
    public string Titulo { get; private set; }
    public string Objeto { get; private set; }
    public decimal Presupuesto { get; private set; }
    public DateTime FechaCierre { get; private set; }
    public DateTime FechaPublicacion { get; private set; }
    public ModalidadContrato Modalidad { get; private set; }
    public EstadoProceso Estado { get; private set; }
    public string NombreEntidad { get; private set; }
    public string NitEntidad { get; private set; }
    public string DepartamentoEntidad { get; private set; }
    public string UrlProceso { get; private set; }
    public float[]? Embedding { get; private set; }
    public DateTime SincronizadoEn { get; private set; }

    // ── Clasificación y filtros (SPEC-01 a SPEC-09) ─────────────────────────
    public ClasificacionRegimen Clasificacion { get; private set; }
    public bool EsConOfertas { get; private set; }
    public string? TipoContrato { get; private set; }

    // ── Adjudicación real ────────────────────────────────────────────────────
    public string? AdjudicadoA { get; private set; }
    public decimal? ValorAdjudicacion { get; private set; }
    public DateTime? FechaAdjudicacion { get; private set; }

    // ── Categorías UNSPSC adicionales (más allá de codigo_principal) ────────
    public List<string> CategoriasAdicionales { get; private set; }

    // ── Señales de competencia (para el componente de puntaje) ──────────────
    public int? ProveedoresInvitados { get; private set; }
    public int? ProveedoresQueManifestaron { get; private set; }
    public int? RespuestasAlProcedimiento { get; private set; }
    public int? ConteoRespuestasOfertas { get; private set; }
    public int? ProveedoresUnicosCon { get; private set; }

    private readonly bool _esSoloEsal;

    private Proceso()
    {
        Id = null!; Titulo = null!; Objeto = null!;
        NombreEntidad = null!; NitEntidad = null!;
        DepartamentoEntidad = null!; UrlProceso = null!;
        CategoriasAdicionales = [];
    }

    public Proceso(
        string id,
        string titulo,
        string objeto,
        decimal presupuesto,
        DateTime fechaCierre,
        DateTime fechaPublicacion,
        ModalidadContrato modalidad,
        EstadoProceso estado,
        string nombreEntidad,
        string nitEntidad,
        string departamentoEntidad,
        string urlProceso,
        ClasificacionRegimen clasificacion = ClasificacionRegimen.Ley80,
        bool esConOfertas = false,
        string? tipoContrato = null,
        string? adjudicadoA = null,
        decimal? valorAdjudicacion = null,
        DateTime? fechaAdjudicacion = null,
        List<string>? categoriasAdicionales = null,
        int? proveedoresInvitados = null,
        int? proveedoresQueManifestaron = null,
        int? respuestasAlProcedimiento = null,
        int? conteoRespuestasOfertas = null,
        int? proveedoresUnicosCon = null)
    {
        Id = id;
        Titulo = titulo;
        Objeto = objeto;
        Presupuesto = presupuesto;
        FechaCierre = fechaCierre;
        FechaPublicacion = fechaPublicacion;
        Modalidad = modalidad;
        Estado = estado;
        NombreEntidad = nombreEntidad;
        NitEntidad = nitEntidad;
        DepartamentoEntidad = departamentoEntidad;
        UrlProceso = urlProceso;
        SincronizadoEn = DateTime.UtcNow;

        Clasificacion = clasificacion;
        EsConOfertas = esConOfertas;
        TipoContrato = tipoContrato;
        AdjudicadoA = adjudicadoA;
        ValorAdjudicacion = valorAdjudicacion;
        FechaAdjudicacion = fechaAdjudicacion;
        CategoriasAdicionales = categoriasAdicionales ?? [];
        ProveedoresInvitados = proveedoresInvitados;
        ProveedoresQueManifestaron = proveedoresQueManifestaron;
        RespuestasAlProcedimiento = respuestasAlProcedimiento;
        ConteoRespuestasOfertas = conteoRespuestasOfertas;
        ProveedoresUnicosCon = proveedoresUnicosCon;

        _esSoloEsal = tipoContrato?.Contains("092", StringComparison.OrdinalIgnoreCase) == true &&
                      tipoContrato.Contains("2017", StringComparison.OrdinalIgnoreCase);
    }

    public bool EstaVigente() => FechaCierre > DateTime.UtcNow;

    public bool EsDesierto() => Estado == EstadoProceso.Desierto;

    /// <summary>
    /// Indica si el proceso solo aplica a Entidades Sin Ánimo de Lucro (Decreto 092 de 2017).
    /// </summary>
    public bool EsSoloEsal() => _esSoloEsal;

    /// <summary>
    /// Cuenta días hábiles (lunes–viernes) entre hoy y la fecha de cierre.
    /// No descuenta festivos colombianos (simplificación fase 1).
    /// </summary>
    public int DiasHabilesRestantes()
    {
        var hoy = DateTime.UtcNow.Date;
        var cierre = FechaCierre.Date;

        if (cierre <= hoy) return 0;

        int dias = 0;
        var fecha = hoy.AddDays(1);
        while (fecha <= cierre)
        {
            if (fecha.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                dias++;
            fecha = fecha.AddDays(1);
        }
        return dias;
    }

    public void AsignarEmbedding(float[] embedding) => Embedding = embedding;

    public void ActualizarEstado(EstadoProceso nuevoEstado) => Estado = nuevoEstado;
}
