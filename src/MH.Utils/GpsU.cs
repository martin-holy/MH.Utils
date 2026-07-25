using MH.Utils.Primitives;
using System;

namespace MH.Utils;

public static class GpsU {
  public static Rational[] ToDms(double value) {
    value = Math.Abs(value);

    int deg = (int)Math.Floor(value);
    value = (value - deg) * 60.0;

    int min = (int)Math.Floor(value);
    double sec = (value - min) * 60.0;

    // Keep 4 decimal digits for seconds
    int secScaled = (int)Math.Round(sec * 10000);

    // Normalize overflow (rare but possible due to rounding)
    if (secScaled >= 60 * 10000) {
      secScaled = 0;
      min++;

      if (min >= 60) {
        min = 0;
        deg++;
      }
    }

    return [
      new Rational((uint)deg, 1),
      new Rational((uint)min, 1),
      new Rational((uint)secScaled, 10000)
    ];
  }

  public static double FromDms(double degrees, double minutes, double seconds) =>
    degrees + minutes / 60.0 + seconds / 3600.0;

  public static double FromDms(Rational degrees, Rational minutes, Rational seconds) =>
    FromDms(degrees.ToDouble(), minutes.ToDouble(), seconds.ToDouble());
}