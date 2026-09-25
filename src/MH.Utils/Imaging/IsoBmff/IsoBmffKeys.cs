using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffKeys(Stream stream, IsoBmffReader reader, IsoBmffBox keys, IsoBmffBox ilst) {
  private List<IsoBmffMetadataEntry>? _entries;

  public static IsoBmffKeys? Find(Stream stream, IsoBmffReader reader, IsoBmffBox moov, Func<IsoBmffBox, long> getBoxChildrenOffset) {
    if (reader.FindChild(moov, IsoBmffTypes.Meta) is not { } meta) return null;

    var childrenOffset = getBoxChildrenOffset(meta);

    if (reader.FindChild(meta, IsoBmffTypes.Keys, childrenOffset) is not { } keys) return null;
    if (reader.FindChild(meta, IsoBmffTypes.Ilst, childrenOffset) is not { } ilst) return null;

    return new IsoBmffKeys(stream, reader, keys, ilst);
  }

  public string? GetKeywords() =>
    _getValue("mdta:com.apple.quicktime.keywords");

  public string? GetComment() =>
    _getValue("mdta:com.apple.quicktime.comment");

  private string? _getValue(string key) {
    var entries = _getEntries();

    foreach (var entry in entries) {
      if (entry.Key == key)
        return entry.Value;
    }

    return null;
  }

  private List<IsoBmffMetadataEntry> _getEntries() {
    if (_entries is not null) return _entries;

    var keyNames = _readKeys();
    var result = new List<IsoBmffMetadataEntry>();

    stream.Position = ilst.DataOffset;

    while (stream.Position < ilst.End) {
      if (reader.ReadBox(ilst.End) is not { } item) break;

      if (item.Type <= 0xFFFF && item.Type < keyNames.Length && keyNames[item.Type] is { } key)
        _readValue(item, key, result);

      stream.Position = item.End;
    }

    return _entries = result;
  }

  private string?[] _readKeys() {
    var count = reader.ReadUInt32BigEndian(keys.DataOffset + 4);

    if (count > int.MaxValue - 1)
      throw new InvalidDataException("Too many ISO BMFF keys.");

    var result = new string[(int)count + 1];

    stream.Position = keys.DataOffset + 8;

    for (uint i = 1; i <= count; i++) {
      var size = reader.ReadUInt32BigEndian();

      if (size < 8)
        throw new InvalidDataException("Invalid ISO BMFF key entry size.");

      var namespaceType = reader.ReadUInt32BigEndian();
      var length = checked((int)size - 8);
      var buffer = new byte[length];

      stream.ReadExactly(buffer);

      result[i] = $"{IsoBmffTypes.GetTypeName(namespaceType)}:{Encoding.UTF8.GetString(buffer)}";
    }

    return result;
  }

  private void _readValue(IsoBmffBox item, string key, List<IsoBmffMetadataEntry> result) {
    if (reader.FindChild(item, IsoBmffTypes.Data) is not { } data || data.DataSize < 8)
      return;

    var length = checked((int)data.DataSize - 8);

    stream.Position = data.DataOffset + 8;

    var buffer = new byte[length];
    stream.ReadExactly(buffer);

    result.Add(new IsoBmffMetadataEntry(key, Encoding.UTF8.GetString(buffer), item, data));
  }
}