using System;
using System.Collections.Generic;
using System.Text;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffItemList(IsoBmffReader reader, IsoBmffBox ilst) {
  private List<IsoBmffMetadataEntry>? _entries;

  public static IsoBmffItemList? Find(IsoBmffReader reader, IsoBmffBox moov) {
    if (reader.FindChild(moov, IsoBmffTypes.Udta) is not { } udta) return null;
    if (reader.FindChild(udta, IsoBmffTypes.Meta) is not { } meta) return null;
    if (reader.FindChild(meta, IsoBmffTypes.Ilst, reader.GetBoxChildrenOffset(meta)) is not { } ilst) return null;
    return new IsoBmffItemList(reader, ilst);
  }

  public string? GetKeywords() =>
    _getValue(IsoBmffTypes.Keyw);

  // TODO
  /*public string? GetComment() =>
    _getValue(IsoBmffTypes.Comment);*/

  private string? _getValue(uint type) {
    var entries = _getEntries();

    foreach (var entry in entries) {
      if (entry.Item?.Type == type)
        return entry.Value;
    }

    return null;
  }

  private List<IsoBmffMetadataEntry> _getEntries() {
    if (_entries is not null) return _entries;

    var result = new List<IsoBmffMetadataEntry>();

    reader._stream.Position = ilst.DataOffset;

    while (reader._stream.Position < ilst.End) {
      if (reader.ReadBox(ilst.End) is not { } item)
        break;

      if (_readItem(item) is { } entry)
        result.Add(entry);

      reader._stream.Position = item.End;
    }

    return _entries = result;
  }

  private IsoBmffMetadataEntry? _readItem(IsoBmffBox item) =>
    _readValue(item, IsoBmffTypes.GetTypeName(item.Type));

  private IsoBmffMetadataEntry? _readValue(IsoBmffBox item, string key) {
    if (reader.FindChild(item, IsoBmffTypes.Data) is not { } data || data.DataSize < 8)
      return null;

    var length = checked((int)data.DataSize - 8);

    reader._stream.Position = data.DataOffset + 8;

    var buffer = new byte[length];
    reader._stream.ReadExactly(buffer);

    // TODO it might not always be UTF8 string!
    return new IsoBmffMetadataEntry(key, Encoding.UTF8.GetString(buffer), item);
  }

  public void SetKeywords(string? value, List<IsoBmffBoxNode> editedBoxes) {
    var entries = _getEntries();

    for (var i = 0; i < entries.Count; i++) {
      var entry = entries[i];

      if (entry.Item?.Type != IsoBmffTypes.Keyw)
        continue;

      if (value is null) {
        editedBoxes.RemoveBox(editedBoxes.FindIndex(entry.Item));
        entries.RemoveAt(i);
      }
      else {
        _updateEntry(entry, editedBoxes, value);
      }

      return;
    }

    if (value is null) return;

    var valueSize = Encoding.UTF8.GetByteCount(value);
    var item = new IsoBmffBox(-1, 8, 8, IsoBmffTypes.Keyw);
    var data = new IsoBmffBox(-1, 8, 16 + valueSize, IsoBmffTypes.Data);
    var ilstIndex = editedBoxes.FindIndex(ilst);

    if (ilstIndex < 0)
      throw new InvalidOperationException("The ilst box was not found.");

    var itemIndex = editedBoxes.InsertBox(item, ilstIndex);

    editedBoxes.InsertBox(data, itemIndex);

    entries.Add(new IsoBmffMetadataEntry("keyw", value, item));
  }

  private void _updateEntry(IsoBmffMetadataEntry entry, List<IsoBmffBoxNode> editedBoxes, string value) {
    var valueSize = Encoding.UTF8.GetByteCount(value);
    var itemIndex = editedBoxes.FindIndex(entry.Item);

    if (itemIndex < 0)
      throw new InvalidOperationException("The keyw box was not found.");

    var dataIndex = editedBoxes.FindIndex(
      x => x.Parent == itemIndex && x.Box.Type == IsoBmffTypes.Data);

    if (dataIndex < 0)
      throw new InvalidOperationException("The keyw data box was not found.");

    var data = editedBoxes[dataIndex].Box;
    var item = editedBoxes[itemIndex].Box;

    var newDataSize = 16 + valueSize;
    var delta = newDataSize - data.Size;

    editedBoxes[dataIndex].Box = new IsoBmffBox(data.Offset, data.HeaderSize, newDataSize, data.Type);
    editedBoxes[itemIndex].Box = new IsoBmffBox(item.Offset, item.HeaderSize, item.Size + delta, item.Type);

    if (delta != 0)
      editedBoxes.UpdateParentSizes(editedBoxes[itemIndex].Parent, delta);

    entry.Value = value;
  }
}