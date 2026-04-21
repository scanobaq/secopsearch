using Secop.Domain.Entities;

namespace Secop.Application.Interfaces;

public interface IAlertaService
{
    Task<int> EnviarAlertaProcesoAsync(long telegramChatId, Puntaje puntaje, Proceso proceso, Proveedor proveedor, CancellationToken ct = default);
    Task EnviarAlertaRupPorVencerAsync(long telegramChatId, Proveedor proveedor, CancellationToken ct = default);
    Task EnviarAlertaDesiertoAsync(long telegramChatId, Proceso proceso, CancellationToken ct = default);
}
