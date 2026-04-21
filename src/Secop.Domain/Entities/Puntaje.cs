using Secop.Domain.Enums;

namespace Secop.Domain.Entities;

public class Puntaje
{
    public Guid Id { get; private set; }
    public string ProcesoId { get; private set; }
    public Guid ProveedorId { get; private set; }
    public float PuntajeTotal { get; private set; }
    public float PuntajeSimilitud { get; private set; }    // 35 puntos máx
    public float PuntajeRequisitos { get; private set; }   // 25 puntos máx
    public float PuntajeTiempo { get; private set; }       // 20 puntos máx
    public float PuntajeCompetencia { get; private set; }  // 12 puntos máx
    public float PuntajeEntidad { get; private set; }      // 8 puntos máx
    public EtiquetaProceso Etiqueta { get; private set; }
    public List<string> Advertencias { get; private set; }
    public bool EsInhabilitado { get; private set; }
    public DateTime CalculadoEn { get; private set; }

    private Puntaje() { ProcesoId = null!; Advertencias = null!; }

    public Puntaje(
        string procesoId,
        Guid proveedorId,
        float puntajeTotal,
        float puntajeSimilitud,
        float puntajeRequisitos,
        float puntajeTiempo,
        float puntajeCompetencia,
        float puntajeEntidad,
        EtiquetaProceso etiqueta,
        List<string> advertencias)
    {
        Id = Guid.NewGuid();
        ProcesoId = procesoId;
        ProveedorId = proveedorId;
        PuntajeTotal = puntajeTotal;
        PuntajeSimilitud = puntajeSimilitud;
        PuntajeRequisitos = puntajeRequisitos;
        PuntajeTiempo = puntajeTiempo;
        PuntajeCompetencia = puntajeCompetencia;
        PuntajeEntidad = puntajeEntidad;
        Etiqueta = etiqueta;
        Advertencias = advertencias;
        EsInhabilitado = false;
        CalculadoEn = DateTime.UtcNow;
    }

    public void AgregarAdvertencia(string advertencia)
    {
        if (string.IsNullOrWhiteSpace(advertencia))
            return;
        if (!Advertencias.Contains(advertencia, StringComparer.OrdinalIgnoreCase))
            Advertencias.Add(advertencia);
    }

    /// <summary>
    /// Crea un puntaje inhabilitante (RUP vencido u otro requisito bloqueante).
    /// El puntaje total es 0 y la etiqueta siempre es Descartar.
    /// </summary>
    public static Puntaje Inhabilitado(string procesoId, Guid proveedorId, List<string> advertencias) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProcesoId = procesoId,
            ProveedorId = proveedorId,
            PuntajeTotal = 0,
            PuntajeSimilitud = 0,
            PuntajeRequisitos = 0,
            PuntajeTiempo = 0,
            PuntajeCompetencia = 0,
            PuntajeEntidad = 0,
            Etiqueta = EtiquetaProceso.Descartar,
            Advertencias = advertencias,
            EsInhabilitado = true,
            CalculadoEn = DateTime.UtcNow
        };
}
