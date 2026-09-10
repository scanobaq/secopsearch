using Secop.Domain.Enums;

namespace Secop.Domain.Entities;

public class Puntaje
{
    public Guid Id { get; private set; }
    public string ProcesoId { get; private set; }
    public Guid ProveedorId { get; private set; }
    public float RelevanciaPorcentaje { get; private set; }
    public EstadoElegibilidad Elegibilidad { get; private set; }
    public EstadoAccionabilidad Accionabilidad { get; private set; }
    public RecomendacionAutomatica? RecomendacionAutomatica { get; private set; }
    public List<string> Razones { get; private set; }
    public DateTime CalculadoEn { get; private set; }

    public bool EsAlertable =>
        RecomendacionAutomatica == Secop.Domain.Enums.RecomendacionAutomatica.Analyze &&
        Elegibilidad != EstadoElegibilidad.Ineligible &&
        Accionabilidad != EstadoAccionabilidad.InsufficientTime;

    // Campos heredados conservados únicamente para escribir las columnas existentes.
    private float PuntajeTotal { get; set; }
    private float PuntajeSimilitud { get; set; }
    private float PuntajeRequisitos { get; set; }
    private float PuntajeTiempo { get; set; }
    private float PuntajeCompetencia { get; set; }
    private float PuntajeEntidad { get; set; }
    private EtiquetaProceso Etiqueta { get; set; }
    private List<string> Advertencias { get; set; }
    private bool EsInhabilitado { get; set; }

    private Puntaje()
    {
        ProcesoId = null!;
        Razones = null!;
        Advertencias = null!;
    }

    public Puntaje(
        string procesoId,
        Guid proveedorId,
        float relevanciaPorcentaje,
        EstadoElegibilidad elegibilidad,
        EstadoAccionabilidad accionabilidad,
        RecomendacionAutomatica? recomendacionAutomatica,
        List<string> razones,
        DateTime calculadoEn)
    {
        if (relevanciaPorcentaje is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(relevanciaPorcentaje));

        Id = Guid.NewGuid();
        ProcesoId = procesoId;
        ProveedorId = proveedorId;
        RelevanciaPorcentaje = relevanciaPorcentaje;
        Elegibilidad = elegibilidad;
        Accionabilidad = accionabilidad;
        RecomendacionAutomatica = recomendacionAutomatica;
        Razones = razones;
        CalculadoEn = calculadoEn;

        PuntajeTotal = 0;
        PuntajeSimilitud = 0;
        PuntajeRequisitos = 0;
        PuntajeTiempo = 0;
        PuntajeCompetencia = 0;
        PuntajeEntidad = 0;
        Etiqueta = recomendacionAutomatica.HasValue
            ? EtiquetaProceso.Analizar
            : EtiquetaProceso.Descartar;
        Advertencias = [.. razones];
        EsInhabilitado = elegibilidad == EstadoElegibilidad.Ineligible;
    }

    public void AgregarRazon(string razon)
    {
        if (string.IsNullOrWhiteSpace(razon))
            return;
        if (Razones.Contains(razon, StringComparer.OrdinalIgnoreCase))
            return;

        Razones.Add(razon);
        Advertencias.Add(razon);
    }
}
