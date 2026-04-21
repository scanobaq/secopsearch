using Secop.Domain.Enums;

namespace Secop.Application.DTOs;

public class PuntajeDto
{
    public Guid Id { get; init; }
    public string ProcesoId { get; init; } = null!;
    public string ProcesoTitulo { get; init; } = null!;
    public string NombreEntidad { get; init; } = null!;
    public decimal Presupuesto { get; init; }
    public DateTime FechaCierre { get; init; }
    public int DiasHabilesRestantes { get; init; }
    public string UrlProceso { get; init; } = null!;

    public Guid ProveedorId { get; init; }
    public string ProveedorNombre { get; init; } = null!;

    public float PuntajeTotal { get; init; }
    public float PuntajeSimilitud { get; init; }
    public float PuntajeRequisitos { get; init; }
    public float PuntajeTiempo { get; init; }
    public float PuntajeCompetencia { get; init; }
    public float PuntajeEntidad { get; init; }
    public EtiquetaProceso Etiqueta { get; init; }
    public List<string> Advertencias { get; init; } = [];
    public bool EsInhabilitado { get; init; }
}
