namespace Pikvew.Core;

/// <summary>Compares numeric runs without parsing them into a bounded integer.</summary>
public sealed class NaturalNameComparer : IComparer<string>
{
    public static NaturalNameComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        var a = 0;
        var b = 0;
        while (a < x.Length && b < y.Length)
        {
            if (char.IsAsciiDigit(x[a]) && char.IsAsciiDigit(y[b]))
            {
                var startA = a;
                var startB = b;
                while (a < x.Length && char.IsAsciiDigit(x[a])) a++;
                while (b < y.Length && char.IsAsciiDigit(y[b])) b++;
                var significantA = startA;
                var significantB = startB;
                while (significantA < a && x[significantA] == '0') significantA++;
                while (significantB < b && y[significantB] == '0') significantB++;
                var result = (a - significantA).CompareTo(b - significantB);
                if (result != 0) return result;
                result = x.AsSpan(significantA, a - significantA)
                    .CompareTo(y.AsSpan(significantB, b - significantB), StringComparison.Ordinal);
                if (result != 0) return result;
                result = (a - startA).CompareTo(b - startB);
                if (result != 0) return result;
            }
            else
            {
                var result = char.ToUpperInvariant(x[a]).CompareTo(char.ToUpperInvariant(y[b]));
                if (result != 0) return result;
                a++;
                b++;
            }
        }
        return (x.Length - a).CompareTo(y.Length - b);
    }
}
