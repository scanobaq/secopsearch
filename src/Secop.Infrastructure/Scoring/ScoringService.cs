using Microsoft.Extensions.Logging;
using Secop.Application.Interfaces;
using Secop.Domain.Constants;
using Secop.Domain.Entities;
using Secop.Domain.Enums;

namespace Secop.Infrastructure.Scoring;

/// <summary>
/// Conserva el nombre histórico del servicio, pero produce una evaluación dimensional.
/// </summary>
public class ScoringService(TimeProvider timeProvider, ILogger<ScoringService> logger) : IScoringService
{
    public Task<Puntaje> CalcularAsync(Proveedor proveedor, Proceso proceso, float similitud)
    {
        var ahora = timeProvider.GetUtcNow();
        var razones = new List<string>();
        var elegibilidad = EvaluarElegibilidad(proveedor, proceso, razones);
        var accionabilidad = EvaluarAccionabilidad(proceso.FechaCierre, ahora, razones);
        var relevancia = Math.Clamp(similitud * 100f, 0f, 100f);
        var recomendacion = elegibilidad != EstadoElegibilidad.Ineligible &&
                            accionabilidad != EstadoAccionabilidad.InsufficientTime
            ? RecomendacionAutomatica.Analyze
            : (RecomendacionAutomatica?)null;

        logger.LogInformation(
            "Evaluación calculada para Proceso {ProcesoId} y Proveedor {ProveedorId}: Relevancia={Relevancia}, Elegibilidad={Elegibilidad}, Accionabilidad={Accionabilidad}, Recomendación={Recomendacion}",
            proceso.Id, proveedor.Id, relevancia, elegibilidad, accionabilidad, recomendacion);

        return Task.FromResult(new Puntaje(
            proceso.Id,
            proveedor.Id,
            relevancia,
            elegibilidad,
            accionabilidad,
            recomendacion,
            razones,
            ahora.UtcDateTime));
    }

    private static EstadoElegibilidad EvaluarElegibilidad(
        Proveedor proveedor,
        Proceso proceso,
        List<string> razones)
    {
        // Política temporal de experimentación: se asume RUP vigente; DEBE restaurarse y validarse antes de producción.
        if (proceso.EsSoloEsal())
            razones.Add(RazonesEvaluacion.SoloEsal);

        if (razones.Count > 0)
            return EstadoElegibilidad.Ineligible;

        if (!proveedor.CubreCapacidadFinanciera(proceso.Presupuesto))
        {
            razones.Add(RazonesEvaluacion.CapacidadInsuficiente);
            return EstadoElegibilidad.RequiresReview;
        }

        razones.Add(RazonesEvaluacion.Elegible);
        return EstadoElegibilidad.Eligible;
    }

    private static EstadoAccionabilidad EvaluarAccionabilidad(
        DateTime fechaCierre,
        DateTimeOffset ahora,
        List<string> razones)
    {
        if (fechaCierre <= DateTime.UnixEpoch || fechaCierre == DateTime.MaxValue)
        {
            razones.Add(RazonesEvaluacion.FechaCierreDesconocida);
            return EstadoAccionabilidad.UnknownDate;
        }

        var cierreUtc = fechaCierre.Kind == DateTimeKind.Local
            ? fechaCierre.ToUniversalTime()
            : DateTime.SpecifyKind(fechaCierre, DateTimeKind.Utc);
        if (cierreUtc <= ahora.UtcDateTime)
        {
            razones.Add(RazonesEvaluacion.PlazoVencido);
            return EstadoAccionabilidad.InsufficientTime;
        }

        var diasHabiles = ContarDiasHabiles(ahora.UtcDateTime.Date, cierreUtc.Date);
        if (diasHabiles <= 5)
        {
            razones.Add(RazonesEvaluacion.PlazoUrgente(diasHabiles));
            return EstadoAccionabilidad.Urgent;
        }

        razones.Add(RazonesEvaluacion.PlazoAccionable(diasHabiles));
        return EstadoAccionabilidad.Actionable;
    }

    private static int ContarDiasHabiles(DateTime desde, DateTime hasta)
    {
        var dias = 0;
        for (var fecha = desde.AddDays(1); fecha <= hasta; fecha = fecha.AddDays(1))
        {
            if (fecha.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                dias++;
        }

        return dias;
    }
}
