using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Secop.Application.Interfaces;

namespace Secop.Infrastructure.ExternalServices;

public class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    
    private const string Model = "text-embedding-3-small"; //ToDo: Make this configurable on environment variables openai_embedding_model

    public OpenAiEmbeddingService(HttpClient http) => _http = http;

    public async Task<float[]> GenerarEmbeddingAsync(string texto, CancellationToken ct)
    {
        var body = new { input = texto, model = Model };
        var response = await _http.PostAsJsonAsync("https://api.openai.com/v1/embeddings", body, ct); //ToDo: Make this configurable on environment variables openai_api_url
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = System.Text.Json.JsonSerializer.Deserialize<OpenAiEmbeddingResponse>(json);
        return result!.Data[0].Embedding;
    }

    public Task<float> CalcularSimilitudAsync(float[] v1, float[] v2)
    {
        // Similitud coseno: dot(v1,v2) / (|v1| * |v2|)
        float dot = 0f, mag1 = 0f, mag2 = 0f;
        for (int i = 0; i < v1.Length; i++)
        {
            dot  += v1[i] * v2[i];
            mag1 += v1[i] * v1[i];
            mag2 += v2[i] * v2[i];
        }
        float denom = MathF.Sqrt(mag1) * MathF.Sqrt(mag2);
        return Task.FromResult(denom == 0f ? 0f : dot / denom);
    }

    // --- DTOs internos de la respuesta OpenAI ---

    private class OpenAiEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData> Data { get; set; } = [];
    }

    private class EmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
