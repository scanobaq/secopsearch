using System.Text.Json.Serialization;
using Secop.Domain.Entities;
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
    public string UrlProceso { get; init; } = null!;

    public Guid ProveedorId { get; init; }
    public string ProveedorNombre { get; init; } = null!;

    public float RelevanciaPorcentaje { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EstadoElegibilidad Elegibilidad { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EstadoAccionabilidad Accionabilidad { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecomendacionAutomatica? RecomendacionAutomatica { get; init; }

    public List<string> Razones { get; init; } = [];
    public bool EsAlertable { get; init; }

    public static PuntajeDto Desde(Puntaje evaluacion, Proceso proceso, Proveedor proveedor) =>
        new()
        {
            Id = evaluacion.Id,
            ProcesoId = proceso.Id,
            ProcesoTitulo = proceso.Titulo,
            NombreEntidad = proceso.NombreEntidad,
            Presupuesto = proceso.Presupuesto,
            FechaCierre = proceso.FechaCierre,
            UrlProceso = proceso.UrlProceso,
            ProveedorId = proveedor.Id,
            ProveedorNombre = proveedor.Nombre,
            RelevanciaPorcentaje = evaluacion.RelevanciaPorcentaje,
            Elegibilidad = evaluacion.Elegibilidad,
            Accionabilidad = evaluacion.Accionabilidad,
            RecomendacionAutomatica = evaluacion.RecomendacionAutomatica,
            Razones = [.. evaluacion.Razones],
            EsAlertable = evaluacion.EsAlertable
        };
}
