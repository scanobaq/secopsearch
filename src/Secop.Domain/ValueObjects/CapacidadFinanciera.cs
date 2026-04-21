namespace Secop.Domain.ValueObjects;

public sealed class CapacidadFinanciera : IEquatable<CapacidadFinanciera>
{
    public decimal Valor { get; }

    public CapacidadFinanciera(decimal valor)
    {
        if (valor < 0)
            throw new ArgumentException("La capacidad financiera no puede ser negativa.", nameof(valor));

        Valor = valor;
    }

    /// <summary>
    /// Verifica si esta capacidad financiera cubre el porcentaje mínimo requerido
    /// sobre el presupuesto de un proceso (generalmente 15% según reglamentación SECOP).
    /// </summary>
    public bool CubreRequisito(decimal presupuestoProceso, decimal porcentajeMinimo = 0.15m) =>
        Valor >= presupuestoProceso * porcentajeMinimo;

    public bool Equals(CapacidadFinanciera? other) => other is not null && Valor == other.Valor;
    public override bool Equals(object? obj) => obj is CapacidadFinanciera other && Equals(other);
    public override int GetHashCode() => Valor.GetHashCode();
    public override string ToString() => Valor.ToString("C0");

    public static bool operator ==(CapacidadFinanciera? left, CapacidadFinanciera? right) =>
        left?.Equals(right) ?? right is null;
    public static bool operator !=(CapacidadFinanciera? left, CapacidadFinanciera? right) => !(left == right);
    public static bool operator >=(CapacidadFinanciera left, CapacidadFinanciera right) =>
        left.Valor >= right.Valor;
    public static bool operator <=(CapacidadFinanciera left, CapacidadFinanciera right) =>
        left.Valor <= right.Valor;
}
