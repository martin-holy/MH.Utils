using System;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.Tiff;

public static class JpegTiffWriter {
  public static bool Write(string srcPath, byte[] tiff) {
    var tmpPath = srcPath + ".tmp";

    try {
      using (var input = File.OpenRead(srcPath))
      using (var output = File.Create(tmpPath)) {
        Write(input, output, tiff);
      }

      File.Delete(srcPath);
      File.Move(tmpPath, srcPath);

      return true;
    }
    catch (Exception ex) {
      Log.Error(ex, srcPath);

      try {
        if (File.Exists(tmpPath))
          File.Delete(tmpPath);
      }
      catch { }

      return false;
    }
  }

  public static void Write(Stream input, Stream output, byte[] tiff) {
    using var br = new BinaryReader(input, Encoding.ASCII, leaveOpen: true);

    // SOI
    ByteU.CopyBytes(input, output, 2);

    bool exifWritten = false;
    bool insertAfterAppSegments = true;
    Span<byte> header = stackalloc byte[ExifU.ExifHeader.Length];

    while (input.Position + 4 <= input.Length) {
      if (br.ReadByte() != 0xFF)
        continue;

      byte marker = br.ReadByte();

      while (marker == 0xFF)
        marker = br.ReadByte();

      // Start of Scan
      if (marker == 0xDA) {
        if (!exifWritten) {
          _writeExifSegment(output, tiff);
          exifWritten = true;
        }

        output.WriteByte(0xFF);
        output.WriteByte(0xDA);

        input.CopyTo(output);
        return;
      }

      ushort segLen = ByteU.ReadBigEndianUInt16(br);

      if (segLen < 2)
        throw new InvalidDataException("Invalid JPEG segment.");

      int payloadLen = segLen - 2;

      // APP1 - check whether it contains EXIF
      if (marker == 0xE1) {
        if (payloadLen >= header.Length) {
          input.ReadExactly(header);

          if (header.SequenceEqual(ExifU.ExifHeader)) {
            // Skip old EXIF
            input.Seek(payloadLen - header.Length, SeekOrigin.Current);
            continue;
          }

          // Not EXIF, restore consumed header
          output.WriteByte(0xFF);
          output.WriteByte(marker);
          ByteU.WriteBigEndianUInt16(output, segLen);

          output.Write(header);

          ByteU.CopyBytes(input, output, payloadLen - header.Length);

          insertAfterAppSegments = false;

          continue;
        }
      }

      if (!exifWritten && insertAfterAppSegments && marker != 0xE0 && marker != 0xE1) {
        _writeExifSegment(output, tiff);
        exifWritten = true;
        insertAfterAppSegments = false;
      }

      output.WriteByte(0xFF);
      output.WriteByte(marker);
      ByteU.WriteBigEndianUInt16(output, segLen);

      ByteU.CopyBytes(input, output, payloadLen);
    }

    if (!exifWritten)
      throw new InvalidDataException("Failed to write EXIF.");
  }

  private static void _writeExifSegment(Stream stream, byte[] tiff) {
    stream.WriteByte(0xFF);
    stream.WriteByte(0xE1);

    const int MaxAppPayload = 65533;

    int payloadLength = ExifU.ExifHeader.Length + tiff.Length;

    if (payloadLength > MaxAppPayload)
      throw new InvalidOperationException("EXIF is too large for a JPEG APP1 segment.");

    ByteU.WriteBigEndianUInt16(stream, (ushort)(payloadLength + 2));

    stream.Write(ExifU.ExifHeader);
    stream.Write(tiff);
  }
}
