namespace Secop.Application.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> GenerarEmbeddingAsync(string texto, CancellationToken ct = default);
    Task<float> CalcularSimilitudAsync(float[] v1, float[] v2);
}
