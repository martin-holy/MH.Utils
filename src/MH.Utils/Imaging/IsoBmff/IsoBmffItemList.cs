using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffItemList(IsoBmffReader reader, IsoBmffBox ilst, bool isNew = false) {
  private List<IsoBmffMetadataEntry>? _entries = isNew ? [] : null;

  public static IsoBmffItemList? Find(IsoBmffReader reader, IsoBmffBox moov) {
    if (reader.FindChild(moov, IsoBmffTypes.Udta) is not { } udta) return null;
    if (reader.FindChild(udta, IsoBmffTypes.Meta) is not { } meta) return null;
    if (reader.FindChild(meta, IsoBmffTypes.Ilst, reader.GetBoxChildrenOffset(meta)) is not { } ilst) return null;
    return new IsoBmffItemList(reader, ilst);
  }

  public string? GetKeywords() =>
    _getValue(IsoBmffTypes.Keyw);

  public string? GetComment() =>
    _getValue(IsoBmffTypes.Comment);

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

  public void SetKeywords(string? value, List<IsoBmffBoxNode> editedBoxes) =>
    _setValue(IsoBmffTypes.Keyw, value, editedBoxes);

  public void SetComment(string? value, List<IsoBmffBoxNode> editedBoxes) =>
    _setValue(IsoBmffTypes.Comment, value, editedBoxes);

  private void _setValue(uint type, string? value, List<IsoBmffBoxNode> editedBoxes) {
    var key = IsoBmffTypes.GetTypeName(type);
    var entries = _getEntries();
    int itemIndex;

    for (var i = 0; i < entries.Count; i++) {
      var entry = entries[i];

      if (entry.Item?.Type != type)
        continue;

      if (value is null) {
        itemIndex = entry.Node is { } node
          ? editedBoxes.IndexOf(node)
          : editedBoxes.FindIndex(entry.Item);

        if (itemIndex < 0)
          throw new InvalidOperationException($"The {key} box was not found.");

        editedBoxes.RemoveBox(itemIndex);
        entries.RemoveAt(i);
      }
      else {
        _updateEntry(entry, editedBoxes, value, key);
      }

      return;
    }

    if (value is null) return;

    var payload = _createData(value);
    var item = new IsoBmffBox(-1, 8, 8, type);
    var data = new IsoBmffBox(-1, 8, 8 + payload.LongLength, IsoBmffTypes.Data);
    var ilstIndex = editedBoxes.FindIndex(ilst);

    if (ilstIndex < 0)
      throw new InvalidOperationException("The ilst box was not found.");

    itemIndex = editedBoxes.InsertBox(item, ilstIndex);
    var itemNode = editedBoxes[itemIndex];

    editedBoxes.InsertBox(data, itemIndex, payload);

    entries.Add(new IsoBmffMetadataEntry(key, value, itemNode.Box, itemNode));
  }

  private void _updateEntry(IsoBmffMetadataEntry entry, List<IsoBmffBoxNode> editedBoxes, string value, string key) {
    var itemIndex = entry.Node is { } node
      ? editedBoxes.IndexOf(node)
      : editedBoxes.FindIndex(entry.Item);

    if (itemIndex < 0)
      throw new InvalidOperationException($"The {key} box was not found.");

    var dataIndex = editedBoxes.FindIndex(
      x => x.Parent == itemIndex && x.Box.Type == IsoBmffTypes.Data);

    if (dataIndex < 0)
      throw new InvalidOperationException($"The {key} data box was not found.");

    var dataNode = editedBoxes[dataIndex];
    var prefix = _getDataPrefix(dataNode);
    var payload = _createData(value, prefix);
    var data = dataNode.Box;
    var newDataSize = data.HeaderSize + payload.LongLength;
    var delta = newDataSize - data.Size;

    dataNode.Data = payload;
    dataNode.Box = new IsoBmffBox(data.Offset, data.HeaderSize, newDataSize, data.Type);

    if (delta != 0)
      editedBoxes.UpdateParentSizes(dataNode.Parent, delta);

    entry.Node = editedBoxes[itemIndex];
    entry.Value = value;
  }

  private byte[] _getDataPrefix(IsoBmffBoxNode dataNode) {
    if (dataNode.Data is { Length: >= 8 } data)
      return data[..8];

    var prefix = new byte[8];

    reader._stream.Position = dataNode.Box.DataOffset;
    reader._stream.ReadExactly(prefix);

    return prefix;
  }

  private static byte[] _createData(string value, byte[]? prefix = null) {
    var valueBytes = Encoding.UTF8.GetBytes(value);
    var result = new byte[checked(8 + valueBytes.Length)];

    if (prefix is not null)
      prefix.CopyTo(result, 0);
    else
      BinaryPrimitives.WriteUInt32BigEndian(result, 1);

    valueBytes.CopyTo(result, 8);

    return result;
  }
}