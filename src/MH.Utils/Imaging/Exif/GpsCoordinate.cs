using System;

namespace MH.Utils.Imaging.Exif;

public readonly record struct GpsCoordinate(double Latitude, double Longitude);

public static class GpsCoordinateExtensions {
  public static bool AlmostEquals(this GpsCoordinate? a, GpsCoordinate? b, double eps = 1e-6) =>
    _almostEqual(a?.Latitude, b?.Latitude, eps) &&
    _almostEqual(a?.Longitude, b?.Longitude, eps);

  private static bool _almostEqual(double? a, double? b, double eps = 1e-6) {
    if (a == null || b == null) return a == b;
    return Math.Abs(a.Value - b.Value) < eps;
  }
}