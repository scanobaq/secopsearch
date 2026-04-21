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

    private Proceso()
    {
        Id = null!; Titulo = null!; Objeto = null!;
        NombreEntidad = null!; NitEntidad = null!;
        DepartamentoEntidad = null!; UrlProceso = null!;
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
        string urlProceso)
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
    }

    public bool EstaVigente() => FechaCierre > DateTime.UtcNow;

    public bool EsDesierto() => Estado == EstadoProceso.Desierto;

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
