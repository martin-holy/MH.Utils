using System;

namespace MH.Utils.Imaging.Tiff;

public enum TiffType : ushort {
  Byte = 1,          // 8-bit unsigned integer
  Ascii = 2,         // 8-bit byte containing a 7-bit ASCII character
  Short = 3,         // 16-bit unsigned integer
  Long = 4,          // 32-bit unsigned integer
  Rational = 5,      // Two LONGs: numerator/denominator

  SByte = 6,         // 8-bit signed integer
  Undefined = 7,     // 8-bit uninterpreted data
  SShort = 8,        // 16-bit signed integer
  SLong = 9,         // 32-bit signed integer
  SRational = 10,    // Two SLONGs: numerator/denominator

  Float = 11,        // IEEE single precision
  Double = 12        // IEEE double precision
}

public static class TiffTypeExtensions {
  public static int GetCount(this TiffType type, int dataLength) {
    int size = type.GetSize();

    if (dataLength % size != 0)
      throw new InvalidOperationException(
        $"Data length ({dataLength}) is not a multiple of TIFF type '{type}' size ({size}).");

    return dataLength / size;
  }

  public static int GetDataLength(this TiffType type, int count) =>
    checked(type.GetSize() * count);

  public static int GetSize(this TiffType type) =>
    type switch {
      TiffType.Byte => 1,
      TiffType.Ascii => 1,
      TiffType.Short => 2,
      TiffType.Long => 4,
      TiffType.Rational => 8,
      TiffType.SByte => 1,
      TiffType.Undefined => 1,
      TiffType.SShort => 2,
      TiffType.SLong => 4,
      TiffType.SRational => 8,
      TiffType.Float => 4,
      TiffType.Double => 8,
      _ => throw new NotSupportedException($"Unsupported TIFF type: {type}")
    };

  public static bool IsInline(this TiffType type, int count) =>
     type.GetDataLength(count) <= 4;
}