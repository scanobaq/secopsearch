using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Secop.Application.Interfaces;

namespace Secop.Infrastructure.ExternalServices;

/// <summary>
/// Fase 5: Descarga el pliego PDF desde la URL del proceso y lo analiza con Claude
/// para extraer requisitos habilitantes, criterios de evaluación, documentos y fechas.
/// </summary>
public class ClaudeService : IClaudeService
{
    private readonly HttpClient _http;
    private readonly ILogger<ClaudeService> _logger;
    private const string Model = "claude-sonnet-4-20250514";
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";

    public ClaudeService(HttpClient http, ILogger<ClaudeService> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<string> AnalizarPliegoAsync(string urlProceso, CancellationToken ct)
    {
        // 1. Descargar PDF del pliego
        byte[] pdfBytes;
        try
        {
            pdfBytes = await _http.GetByteArrayAsync(urlProceso, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo descargar el pliego: {Url}", urlProceso);
            return "No se pudo descargar el pliego de condiciones.";
        }

        var pdfBase64 = Convert.ToBase64String(pdfBytes);

        // 2. Enviar a Claude con el PDF como documento
        var body = new
        {
            model = Model,
            max_tokens = 2048,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "document",
                            source = new
                            {
                                type = "base64",
                                media_type = "application/pdf",
                                data = pdfBase64
                            }
                        },
                        new
                        {
                            type = "text",
                            text = """
                                Analiza este pliego de condiciones de SECOP II y extrae en formato estructurado:

                                1. **Requisitos habilitantes** (jurídicos, financieros, técnicos)
                                2. **Criterios de evaluación** con sus pesos o puntajes
                                3. **Documentos obligatorios** para presentar oferta
                                4. **Fechas clave** (audiencias, cierre, adjudicación)

                                Sé conciso. Si algo no está claro en el documento, indícalo.
                                """
                        }
                    }
                }
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync(ApiUrl, body, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>(ct);
            return result?.Content.FirstOrDefault()?.Text ?? "Sin respuesta de Claude.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error llamando a Claude API");
            return "Error al analizar el pliego.";
        }
    }

    // --- DTOs internos de la respuesta de Claude ---

    private record ClaudeResponse(
        [property: JsonPropertyName("content")] List<ContentBlock> Content);

    private record ContentBlock(
        [property: JsonPropertyName("text")] string Text);
}
