using System;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.Tiff;

public static class JpegTiffReader {
  public static TiffReader? ReadFrom(string path) {
    using var fs = File.OpenRead(path);
    return ReadFrom(fs);
  }

  public static TiffReader? ReadFrom(Stream stream) {
    using var br = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    if (stream.Length < 4)
      return null;

    if (br.ReadByte() != 0xFF || br.ReadByte() != 0xD8)
      return null;

    Span<byte> header = stackalloc byte[ExifU.ExifHeader.Length];

    while (stream.Position + 4 <= stream.Length) {
      if (br.ReadByte() != 0xFF)
        continue;

      byte marker = br.ReadByte();

      while (marker == 0xFF)
        marker = br.ReadByte();

      if (marker == 0xDA || marker == 0xD9)
        break;

      ushort segLen = ByteU.ReadBigEndianUInt16(br);

      if (segLen < 2)
        throw new InvalidDataException("Invalid JPEG segment.");

      int payloadLen = segLen - 2;

      if (marker != 0xE1) {
        stream.Seek(payloadLen, SeekOrigin.Current);
        continue;
      }

      if (payloadLen < ExifU.ExifHeader.Length) {
        stream.Seek(payloadLen, SeekOrigin.Current);
        continue;
      }

      stream.ReadExactly(header);

      if (!header.SequenceEqual(ExifU.ExifHeader)) {
        stream.Seek(payloadLen - header.Length, SeekOrigin.Current);
        continue;
      }

      return new TiffReader(br.ReadBytes(payloadLen - header.Length));
    }

    return null;
  }
}