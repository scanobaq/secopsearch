using MediatR;

namespace Secop.Application.UseCases.Procesos.SincronizarProcesos;

/// <summary>
/// Llama a la API de SECOP II, ingesta los procesos nuevos desde <see cref="Desde"/>,
/// genera sus embeddings, calcula puntajes contra todos los proveedores
/// y envía alertas Telegram para los que superen el umbral.
/// </summary>
public record SincronizarProcesosCommand(DateTime Desde, DateTime? Hasta = null) : IRequest<int>;
