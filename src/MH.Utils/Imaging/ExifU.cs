using System;

namespace MH.Utils.Imaging;

public static class ExifU {
  public static ReadOnlySpan<byte> ExifHeader => "Exif\0\0"u8;
  public static ReadOnlySpan<byte> AsciiHeader => "ASCII\0\0\0"u8;
  public static ReadOnlySpan<byte> UnicodeHeader => "UNICODE\0"u8;
  public static ReadOnlySpan<byte> JisHeader => "JIS\0\0\0\0\0"u8;
}