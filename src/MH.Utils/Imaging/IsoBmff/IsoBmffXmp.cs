using System;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.IsoBmff;

internal class IsoBmffXmp {
  private static ReadOnlySpan<byte> _xmpUuid => [
    0xBE, 0x7A, 0xCF, 0xCB, 0x97, 0xA9, 0x42, 0xE8,
    0x9C, 0x71, 0x99, 0x94, 0x91, 0xE3, 0xAF, 0xAC
  ];

  public static IsoBmffMetadataEntry? Find(Stream stream, IsoBmffReader reader) =>
    reader.Find(IsoBmffTypes.Uuid) is { } uuid
      ? _readXmp(stream, uuid)
      : null;

  private static IsoBmffMetadataEntry? _readXmp(Stream stream, IsoBmffBox uuid) {
    if (!_isXmp(stream, uuid)) return null;

    var length = checked((int)uuid.DataSize - 16);
    var buffer = new byte[length];

    stream.ReadExactly(buffer);

    var xmp = Encoding.UTF8.GetString(buffer);

    if (!xmp.StartsWith("<?xpacket", StringComparison.Ordinal))
      return null;

    return new IsoBmffMetadataEntry("xmp", xmp, uuid, uuid);
  }

  private static bool _isXmp(Stream stream, IsoBmffBox box) {
    if (box.Type != IsoBmffTypes.Uuid || box.DataSize <= 16)
      return false;

    Span<byte> buffer = stackalloc byte[16];

    stream.Position = box.DataOffset;
    stream.ReadExactly(buffer);

    return buffer.SequenceEqual(_xmpUuid);
  }
}