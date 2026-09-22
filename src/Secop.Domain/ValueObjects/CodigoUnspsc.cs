namespace Secop.Domain.ValueObjects;

public sealed class CodigoUnspsc : IEquatable<CodigoUnspsc>
{
    public string Valor { get; }

    public CodigoUnspsc(string valor)
    {
        if (!EsCodigoCanonico(valor, out var limpio))
            throw new ArgumentException($"El código UNSPSC debe tener exactamente 8 dígitos numéricos. Valor recibido: '{valor}'", nameof(valor));

        Valor = limpio;
    }

    public string Segmento    => Valor[..2];
    public string Familia     => Valor[2..4];
    public string Clase       => Valor[4..6];
    public string Producto    => Valor[6..8];
    public string CodigoClase => Valor[..6];

    public static bool TryCreate(string? valor, out CodigoUnspsc? codigo)
    {
        codigo = null;
        if (!EsCodigoCanonico(valor, out _))
            return false;

        codigo = new CodigoUnspsc(valor!);
        return true;
    }

    public bool ComparteClaseCon(CodigoUnspsc otro) => CodigoClase == otro.CodigoClase;

    private static bool EsCodigoCanonico(string? valor, out string limpio)
    {
        limpio = valor?.Trim() ?? string.Empty;
        return limpio.Length == 8 && limpio.All(caracter => caracter is >= '0' and <= '9');
    }

    public bool Equals(CodigoUnspsc? other) => other is not null && Valor == other.Valor;
    public override bool Equals(object? obj) => obj is CodigoUnspsc other && Equals(other);
    public override int GetHashCode() => Valor.GetHashCode();
    public override string ToString() => Valor;

    public static bool operator ==(CodigoUnspsc? left, CodigoUnspsc? right) =>
        left?.Equals(right) ?? right is null;
    public static bool operator !=(CodigoUnspsc? left, CodigoUnspsc? right) => !(left == right);
}
