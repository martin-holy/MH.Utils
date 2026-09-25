using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffItemList(Stream stream, IsoBmffReader reader, IsoBmffBox ilst) {
  private List<IsoBmffMetadataEntry>? _entries;

  public static IsoBmffItemList? Find(Stream stream, IsoBmffReader reader, IsoBmffBox moov, Func<IsoBmffBox, long> getBoxChildrenOffset) {
    if (reader.FindChild(moov, IsoBmffTypes.Udta) is not { } udta) return null;
    if (reader.FindChild(udta, IsoBmffTypes.Meta) is not { } meta) return null;
    if (reader.FindChild(meta, IsoBmffTypes.Ilst, getBoxChildrenOffset(meta)) is not { } ilst) return null;
    return new IsoBmffItemList(stream, reader, ilst);
  }

  public string? GetKeywords() =>
    _getValue(IsoBmffTypes.Keyw);

  // TODO
  /*public string? GetComment() =>
    _getValue(IsoBmffTypes.Comment);*/

  private string? _getValue(uint type) {
    var entries = _getEntries();

    foreach (var entry in entries) {
      if (entry.Item.Type == type)
        return entry.Value;
    }

    return null;
  }

  private List<IsoBmffMetadataEntry> _getEntries() {
    if (_entries is not null) return _entries;

    var result = new List<IsoBmffMetadataEntry>();

    stream.Position = ilst.DataOffset;

    while (stream.Position < ilst.End) {
      if (reader.ReadBox(ilst.End) is not { } item)
        break;

      if (_readItem(item) is { } entry)
        result.Add(entry);

      stream.Position = item.End;
    }

    return _entries = result;
  }

  private IsoBmffMetadataEntry? _readItem(IsoBmffBox item) =>
    _readValue(item, IsoBmffTypes.GetTypeName(item.Type));

  private IsoBmffMetadataEntry? _readValue(IsoBmffBox item, string key) {
    if (reader.FindChild(item, IsoBmffTypes.Data) is not { } data || data.DataSize < 8)
      return null;

    var length = checked((int)data.DataSize - 8);

    stream.Position = data.DataOffset + 8;

    var buffer = new byte[length];
    stream.ReadExactly(buffer);

    // TODO it might now always be UTF8 string!
    return new IsoBmffMetadataEntry(key, Encoding.UTF8.GetString(buffer), item, data);
  }
}