using MediatR;

namespace Secop.Application.UseCases.Procesos.SincronizarProcesos;

/// <summary>
/// Llama a la API de SECOP II e ingesta procesos cuya fecha de última publicación es igual
/// o posterior a <see cref="Desde"/> y, cuando se proporciona, anterior a <see cref="Hasta"/>; genera sus
/// embeddings, calcula puntajes contra todos los proveedores
/// y envía alertas Telegram para los que superen el umbral.
/// </summary>
public record SincronizarProcesosCommand(DateTime Desde, DateTime? Hasta = null) : IRequest<int>;
