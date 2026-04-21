namespace Secop.Domain.ValueObjects;

public sealed class CodigoUnspsc : IEquatable<CodigoUnspsc>
{
    public string Valor { get; }

    public CodigoUnspsc(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException("El código UNSPSC no puede estar vacío.", nameof(valor));

        // UNSPSC tiene formato de 8 dígitos numéricos
        var limpio = valor.Trim();
        if (limpio.Length != 8 || !limpio.All(char.IsDigit))
            throw new ArgumentException($"El código UNSPSC debe tener exactamente 8 dígitos numéricos. Valor recibido: '{valor}'", nameof(valor));

        Valor = limpio;
    }

    public string Segmento    => Valor[..2];
    public string Familia     => Valor[2..4];
    public string Clase       => Valor[4..6];
    public string Producto    => Valor[6..8];
    public string CodigoClase => Valor[..6];

    public bool Equals(CodigoUnspsc? other) => other is not null && Valor == other.Valor;
    public override bool Equals(object? obj) => obj is CodigoUnspsc other && Equals(other);
    public override int GetHashCode() => Valor.GetHashCode();
    public override string ToString() => Valor;

    public static bool operator ==(CodigoUnspsc? left, CodigoUnspsc? right) =>
        left?.Equals(right) ?? right is null;
    public static bool operator !=(CodigoUnspsc? left, CodigoUnspsc? right) => !(left == right);
}
