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