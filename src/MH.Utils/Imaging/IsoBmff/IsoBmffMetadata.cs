using MH.Utils.Imaging.Xmp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffMetadata(Stream stream, IsoBmffReader reader) {
  private static ReadOnlySpan<byte> _xmpUuid => "BE7ACFCB97A942E89C71999491E3AFAC"u8;
  private bool _xmpRead;
  private XmpMetadata? _xmp;

  public XmpMetadata Xmp => _getXmp();

  public List<IsoBmffMetadataEntry> Read(IsoBmffBox moov) {
    var result = new List<IsoBmffMetadataEntry>();

    _readMeta(moov, result);

    if (reader.FindChild(moov, IsoBmffTypes.Udta) is { } udta)
      _readMeta(udta, result);

    return result;
  }

  private void _readMeta(IsoBmffBox parent, List<IsoBmffMetadataEntry> result) {
    if (reader.FindChild(parent, IsoBmffTypes.Meta) is not { } meta)
      return;

    var childrenOffset = GetChildrenOffset(meta);

    var keys = reader.FindChild(meta, IsoBmffTypes.Keys, childrenOffset);
    var ilst = reader.FindChild(meta, IsoBmffTypes.Ilst, childrenOffset);

    if (ilst is { } box)
      _readItems(box, keys, result);
  }

  private void _readItems(IsoBmffBox ilst, IsoBmffBox? keys, List<IsoBmffMetadataEntry> result) {
    stream.Position = ilst.DataOffset;
    var end = ilst.End;

    while (stream.Position < end) {
      if (reader.ReadBox(end) is not { } item)
        break;

      if (_readItem(item, keys) is { } entry)
        result.Add(entry);

      stream.Position = item.End;
    }
  }

  private IsoBmffMetadataEntry? _readItem(IsoBmffBox item, IsoBmffBox? keys) {
    if (item.Type == IsoBmffTypes.Keyw)
      return _readValue(item, "keyw");

    if (keys != null && item.Type <= 0xFFFF) {
      if (_readKey(keys.Value, item.Type) is not { } key)
        return null;

      return _readValue(item, key);
    }

    return null;
  }

  private IsoBmffMetadataEntry? _readValue(IsoBmffBox item, string key) {
    if (reader.FindChild(item, IsoBmffTypes.Data) is not { } data || data.DataSize < 8)
      return null;

    var length = checked((int)data.DataSize - 8);

    stream.Position = data.DataOffset + 8;

    var buffer = new byte[length];
    stream.ReadExactly(buffer);

    return new IsoBmffMetadataEntry(key, Encoding.UTF8.GetString(buffer), item, data);
  }

  private string? _readKey(IsoBmffBox keys, uint index) {
    var count = reader.ReadUInt32BigEndian(keys.DataOffset + 4);

    if (index == 0 || index > count)
      return null;

    stream.Position = keys.DataOffset + 8;

    for (uint i = 1; i <= count; i++) {
      var size = reader.ReadUInt32BigEndian();

      if (size < 8)
        throw new InvalidDataException("Invalid ISO BMFF key entry size.");

      if (i == index) {
        var namespaceType = reader.ReadUInt32BigEndian();

        var length = checked((int)size - 8);
        var buffer = new byte[length];

        stream.ReadExactly(buffer);

        return $"{IsoBmffTypes.GetTypeName(namespaceType)}:{Encoding.UTF8.GetString(buffer)}";
      }

      stream.Position += size - 4;
    }

    return null;
  }

  internal long GetChildrenOffset(IsoBmffBox box) {
    if (box.Type != IsoBmffTypes.Meta) return 0;

    stream.Position = box.DataOffset;
    if (reader.ReadBox(box.End) is { Type: IsoBmffTypes.Hdlr }) return 0;

    return 4;
  }

  private XmpMetadata _getXmp() {
    if (_xmp != null) return _xmp;

    if (!_xmpRead) {
      /*if (_readXmp() is { } entry)
        _xmp = new XmpMetadata(entry.Value);*/

      _xmpRead = true;
    }

    return _xmp ??= new XmpMetadata(null);
  }

  private IsoBmffMetadataEntry? _readXmp(IsoBmffBox uuid) {
    if (!_isXmp(uuid)) return null;

    var length = checked((int)uuid.DataSize - 16);
    var buffer = new byte[length];

    stream.ReadExactly(buffer);

    var xmp = Encoding.UTF8.GetString(buffer);

    if (!xmp.StartsWith("<?xpacket", StringComparison.Ordinal))
      return null;

    return new IsoBmffMetadataEntry("xmp", xmp, uuid, uuid);
  }

  private bool _isXmp(IsoBmffBox box) {
    if (box.Type != IsoBmffTypes.Uuid || box.DataSize <= 16)
      return false;

    Span<byte> buffer = stackalloc byte[16];

    stream.Position = box.DataOffset;
    stream.ReadExactly(buffer);

    return buffer.SequenceEqual(_xmpUuid);
  }
}