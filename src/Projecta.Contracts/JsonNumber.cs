using System;
using System.Globalization;
using System.Numerics;
using Projecta.Mapping;

namespace Projecta.Contracts;

// Exact decimal arithmetic on JSON number lexemes, without a decimal/double rounding conversion.
internal readonly record struct JsonNumber(BigInteger Coefficient, int Exponent)
{
    internal static JsonNumber Parse(string text)
    {
        if (text.Length > 4096) throw new ProjectionConfigurationException("Number exceeds contract profile limits.");
        int e = text.IndexOfAny(new[] { 'e', 'E' });
        string mantissa = e < 0 ? text : text[..e];
        int exponent = e < 0 ? 0 : int.Parse(text[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        int point = mantissa.IndexOf('.');
        if (point >= 0) { exponent = checked(exponent - (mantissa.Length - point - 1)); mantissa = mantissa.Remove(point, 1); }
        if (exponent is < -4096 or > 4096) throw new ProjectionConfigurationException("Number exponent exceeds contract profile limits.");
        return new(BigInteger.Parse(mantissa, CultureInfo.InvariantCulture), exponent);
    }
    internal bool IsInteger => Exponent >= 0 || Coefficient % BigInteger.Pow(10, -Exponent) == 0;
    internal int Compare(JsonNumber other)
    {
        int common = Math.Min(Exponent, other.Exponent);
        return (Coefficient * BigInteger.Pow(10, Exponent - common)).CompareTo(other.Coefficient * BigInteger.Pow(10, other.Exponent - common));
    }
    internal bool IsMultipleOf(JsonNumber other)
    {
        int common = Math.Min(Exponent, other.Exponent);
        return Coefficient * BigInteger.Pow(10, Exponent - common) % (other.Coefficient * BigInteger.Pow(10, other.Exponent - common)) == 0;
    }
}
