using Microsoft.Extensions.Logging;
using Secop.Application.Interfaces;
using Secop.Domain.Entities;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Secop.Infrastructure.ExternalServices.Telegram;

public class TelegramAlertaService : IAlertaService
{
    private readonly ITelegramBotClient _bot;
    private readonly ILogger<TelegramAlertaService> _logger;

    public TelegramAlertaService(ITelegramBotClient bot, ILogger<TelegramAlertaService> logger)
    {
        _bot    = bot;
        _logger = logger;
    }

    public async Task<int> EnviarAlertaProcesoAsync(
        long telegramChatId, Puntaje puntaje, Proceso proceso, Proveedor proveedor, CancellationToken ct)
    {
        var texto = ProcesoMessageFormatter.Formatear(puntaje, proceso, proveedor);

        var teclado = new InlineKeyboardMarkup(
        [
            [
                InlineKeyboardButton.WithCallbackData("📄 Ver pliego",    $"pliego_{proceso.Id}"),
                InlineKeyboardButton.WithCallbackData("✅ Voy a proponer", $"proponer_{proceso.Id}"),
                InlineKeyboardButton.WithCallbackData("❌ Descartar",     $"descartar_{proceso.Id}")
            ]
        ]);

        try
        {
            var msg = await _bot.SendMessage(
                chatId: telegramChatId,
                text: texto,
                parseMode: ParseMode.Html,
                replyMarkup: teclado,
                cancellationToken: ct);

            _logger.LogInformation("Alerta enviada — Proceso: {Id} → Chat: {ChatId}", proceso.Id, telegramChatId);
            return msg.MessageId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando alerta Telegram — Chat: {ChatId}", telegramChatId);
            return 0;
        }
    }

    public async Task EnviarAlertaRupPorVencerAsync(long telegramChatId, Proveedor proveedor, CancellationToken ct)
    {
        var dias = proveedor.DiasParaVencimientoRup();
        var texto = $"""
            ⚠️ <b>ALERTA RUP</b>

            El RUP de <b>{EscapeHtml(proveedor.Nombre)}</b> vence en <b>{dias} días</b>.
            Renuévalo antes del {proveedor.RupVigencia:dd/MM/yyyy} para no perder procesos activos.
            """;

        await _bot.SendMessage(telegramChatId, texto, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    public async Task EnviarAlertaDesiertoAsync(long telegramChatId, Proceso proceso, CancellationToken ct)
    {
        var texto = $"""
            🔄 <b>PROCESO DESIERTO</b>

            📋 {EscapeHtml(proceso.Titulo)}
            🏛 {EscapeHtml(proceso.NombreEntidad)}
            💰 {proceso.Presupuesto:C0}

            Este proceso quedó desierto — podría volver a convocarse.
            🔗 <a href="{proceso.UrlProceso}">Ver en SECOP II</a>
            """;

        await _bot.SendMessage(telegramChatId, texto, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
