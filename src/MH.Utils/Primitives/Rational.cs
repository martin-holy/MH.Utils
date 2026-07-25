namespace MH.Utils.Primitives;

public readonly record struct Rational(uint Numerator, uint Denominator);

public static class RationalExtensions {
  public static double ToDouble(this Rational value) {
    if (value.Denominator == 0) return 0;

    return (double)value.Numerator / value.Denominator;
  }
}