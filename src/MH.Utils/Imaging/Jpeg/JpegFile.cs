using System.IO;
using System.Text;

namespace MH.Utils.Imaging.Jpeg;

public class JpegFile {
  public ushort Width { get; private set; }
  public ushort Height { get; private set; }

  public JpegFile(string filePath) {
    if (_readSize(filePath) is { } size) {
      Width = size.Width;
      Height = size.Height;
    }
  }

  private static (ushort Width, ushort Height)? _readSize(string filePath) {
    using var stream = File.OpenRead(filePath);
    return _readSize(stream);
  }

  private static (ushort Width, ushort Height)? _readSize(Stream stream) {
    using var br = new BinaryReader(stream, Encoding.ASCII, true);

    if (br.ReadByte() != 0xFF || br.ReadByte() != 0xD8)
      throw new InvalidDataException("Not a JPEG file.");

    while (stream.Position < stream.Length) {
      if (br.ReadByte() != 0xFF)
        continue;

      byte marker = br.ReadByte();

      while (marker == 0xFF)
        marker = br.ReadByte();

      if (_isStartOfFrame(marker)) {
        ushort length = _readUInt16(br);

        if (length < 7)
          throw new InvalidDataException("Invalid JPEG SOF segment.");

        br.ReadByte(); // precision

        ushort height = _readUInt16(br);
        ushort width = _readUInt16(br);

        return (width, height);
      }

      if (marker == 0xDA || marker == 0xD9)
        break;

      if (!_hasLength(marker))
        continue;

      ushort segmentLength = _readUInt16(br);

      if (segmentLength < 2)
        throw new InvalidDataException("Invalid JPEG segment.");

      stream.Seek(segmentLength - 2, SeekOrigin.Current);
    }

    return null;
  }

  private static bool _isStartOfFrame(byte marker) =>
    marker is
      0xC0 or 0xC1 or 0xC2 or 0xC3 or
      0xC5 or 0xC6 or 0xC7 or
      0xC9 or 0xCA or 0xCB or
      0xCD or 0xCE or 0xCF;

  private static bool _hasLength(byte marker) =>
    marker switch {
      0xD8 or 0xD9 => false, // SOI, EOI
      >= 0xD0 and <= 0xD7 => false, // RST0-RST7
      0x01 => false, // TEM
      _ => true
    };

  private static ushort _readUInt16(BinaryReader reader) {
    byte high = reader.ReadByte();
    byte low = reader.ReadByte();

    return (ushort)((high << 8) | low);
  }
}