using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.IsoBmff;

internal class IsoBmffXmp {
  private static ReadOnlySpan<byte> _xmpUuid => [
    0xBE, 0x7A, 0xCF, 0xCB, 0x97, 0xA9, 0x42, 0xE8,
    0x9C, 0x71, 0x99, 0x94, 0x91, 0xE3, 0xAF, 0xAC
  ];

  public static IsoBmffMetadataEntry? Find(Stream stream, IsoBmffReader reader) {
    var end = stream.Length;
    stream.Position = 0;

    while (stream.Position < end) {
      if (reader.ReadBox(end) is not { } box)
        break;

      if (box.Type == IsoBmffTypes.Uuid && _readXmp(stream, box) is { } entry)
        return entry;

      stream.Position = box.End;
    }

    return null;
  }

  private static IsoBmffMetadataEntry? _readXmp(Stream stream, IsoBmffBox uuid) {
    if (!_isXmp(stream, uuid)) return null;

    var length = checked((int)uuid.DataSize - 16);
    var buffer = new byte[length];

    stream.ReadExactly(buffer);

    var xmp = Encoding.UTF8.GetString(buffer);

    if (!xmp.StartsWith("<?xpacket", StringComparison.Ordinal))
      return null;

    return new IsoBmffMetadataEntry("xmp", xmp, uuid);
  }

  private static bool _isXmp(Stream stream, IsoBmffBox box) {
    if (box.Type != IsoBmffTypes.Uuid || box.DataSize <= 16)
      return false;

    Span<byte> buffer = stackalloc byte[16];

    stream.Position = box.DataOffset;
    stream.ReadExactly(buffer);

    return buffer.SequenceEqual(_xmpUuid);
  }

  public static byte[] CreateData(byte[] packet) =>
    _createData(_xmpUuid, packet);

  public static byte[] CreateData(Stream stream, IsoBmffBox box, byte[] packet) {
    Span<byte> uuid = stackalloc byte[16];

    stream.Position = box.DataOffset;
    stream.ReadExactly(uuid);

    return _createData(uuid, packet);
  }

  private static byte[] _createData(ReadOnlySpan<byte> uuid, byte[] packet) {
    var data = new byte[checked(uuid.Length + packet.Length)];

    uuid.CopyTo(data);
    packet.CopyTo(data, uuid.Length);

    return data;
  }

  public static IsoBmffNode UpdateNode(IsoBmffMetadataEntry? xmpEntry, IsoBmffNode? xmpNode, byte[] packet, List<IsoBmffNode> nodes, Stream stream) {
    byte[] data;

    if (xmpEntry?.Item is { } originalBox) {
      if (xmpNode is null) {
        var index = nodes.FindIndex(originalBox, throwIfNotFound: true);

        xmpNode = nodes[index];
      }

      data = CreateData(stream, originalBox, packet);
    }
    else {
      data = CreateData(packet);

      if (xmpNode is null) {
        var box = new IsoBmffBox(-1, 8, checked(8 + data.LongLength), IsoBmffTypes.Uuid);

        xmpNode = nodes.InsertTopLevelNode(new IsoBmffNode(box, -1, data: data));
      }
    }

    nodes.ReplaceData(xmpNode, data);

    return xmpNode;
  }
}