using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffKeys(IsoBmffReader reader, IsoBmffBox keys, IsoBmffBox ilst, bool isNew = false) {
  private List<IsoBmffMetadataEntry>? _entries = isNew ? [] : null;
  private string?[]? _keyNames = isNew ? [null] : null;

  public static IsoBmffKeys? Find(IsoBmffReader reader, IsoBmffBox moov) {
    if (reader.FindChild(moov, IsoBmffTypes.Meta) is not { } meta) return null;

    var childrenOffset = reader.GetBoxChildrenOffset(meta);

    if (reader.FindChild(meta, IsoBmffTypes.Keys, childrenOffset) is not { } keys) return null;
    if (reader.FindChild(meta, IsoBmffTypes.Ilst, childrenOffset) is not { } ilst) return null;

    return new IsoBmffKeys(reader, keys, ilst);
  }

  public string? GetKeywords() =>
    _getValue("mdta:com.apple.quicktime.keywords");

  public string? GetComment() =>
    _getValue("mdta:com.apple.quicktime.comment");

  public void SetKeywords(string? value, IsoBmffEditor editor) =>
    _setValue("mdta:com.apple.quicktime.keywords", value, editor);

  public void SetComment(string? value, IsoBmffEditor editor) =>
    _setValue("mdta:com.apple.quicktime.comment", value, editor);

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

    reader._stream.Position = ilst.DataOffset;

    while (reader._stream.Position < ilst.End) {
      if (reader.ReadBox(ilst.End) is not { } item) break;

      if (item.Type <= 0xFFFF && item.Type < keyNames.Length && keyNames[item.Type] is { } key)
        _readValue(item, key, result);

      reader._stream.Position = item.End;
    }

    return _entries = result;
  }

  private string?[] _readKeys() {
    if (_keyNames is not null) return _keyNames;

    var count = reader.ReadUInt32BigEndian(keys.DataOffset + 4);

    if (count > int.MaxValue - 1)
      throw new InvalidDataException("Too many ISO BMFF keys.");

    var result = new string[(int)count + 1];

    reader._stream.Position = keys.DataOffset + 8;

    for (uint i = 1; i <= count; i++) {
      var size = reader.ReadUInt32BigEndian();

      if (size < 8)
        throw new InvalidDataException("Invalid ISO BMFF key entry size.");

      var namespaceType = reader.ReadUInt32BigEndian();
      var length = checked((int)size - 8);
      var buffer = new byte[length];

      reader._stream.ReadExactly(buffer);

      result[i] = $"{IsoBmffTypes.GetTypeName(namespaceType)}:{Encoding.UTF8.GetString(buffer)}";
    }

    return _keyNames = result;
  }

  private void _setValue(string key, string? value, IsoBmffEditor editor) {
    var keyNames = _readKeys();
    var keyIndex = Array.IndexOf(keyNames, key);
    var editedBoxes = editor._getEditedBoxes();
    var entries = _getEntries();

    foreach (var entry in entries) {
      if (entry.Key != key)
        continue;

      if (value is null) {
        var itemIndex = entry.Node is { } node
          ? editedBoxes.IndexOf(node)
          : editedBoxes.FindIndex(entry.Item!.Value);

        if (itemIndex < 0)
          throw new InvalidOperationException($"The {key} box was not found.");

        editedBoxes.RemoveBox(itemIndex);
        entries.Remove(entry);
      }
      else {
        editor._updateEntry(entry, value, key);
      }

      return;
    }

    if (value is null) return;

    if (keyIndex < 0)
      keyIndex = _addKey(key, editedBoxes);

    var payload = IsoBmffEditor._createData(value);
    var item = new IsoBmffBox(-1, 8, 8, (uint)keyIndex);
    var data = new IsoBmffBox(-1, 8, 8 + payload.LongLength, IsoBmffTypes.Data);
    var ilstIndex = editedBoxes.FindIndex(ilst);

    if (ilstIndex < 0)
      throw new InvalidOperationException("The ilst box was not found.");

    var itemIndexToInsert = editedBoxes.InsertBox(item, ilstIndex);
    var itemNode = editedBoxes[itemIndexToInsert];

    editedBoxes.InsertBox(data, itemIndexToInsert, payload);

    entries.Add(new IsoBmffMetadataEntry(key, value, itemNode.Box, itemNode));
  }

  private int _addKey(string key, List<IsoBmffBoxNode> editedBoxes) {
    var keyNames = _readKeys();

    if (keyNames.Length > ushort.MaxValue)
      throw new InvalidDataException("Too many ISO BMFF keys.");

    var separatorIndex = key.IndexOf(':');

    if (separatorIndex != 4)
      throw new InvalidDataException("Invalid ISO BMFF key namespace.");

    var nameBytes = Encoding.UTF8.GetBytes(key[(separatorIndex + 1)..]);
    var entrySize = checked(8 + nameBytes.Length);
    var index = keyNames.Length;

    var keysIndex = editedBoxes.FindIndex(keys);

    if (keysIndex < 0)
      throw new InvalidOperationException("The keys box was not found.");

    var keysNode = editedBoxes[keysIndex];
    var originalData = _getKeysData(keysNode);
    var updatedData = new byte[checked(originalData.Length + entrySize)];

    originalData.CopyTo(updatedData, 0);
    BinaryPrimitives.WriteUInt32BigEndian(updatedData.AsSpan(4), (uint)index);
    BinaryPrimitives.WriteUInt32BigEndian(updatedData.AsSpan(originalData.Length), (uint)entrySize);
    BinaryPrimitives.WriteUInt32BigEndian(updatedData.AsSpan(originalData.Length + 4), 0x6D647461);
    nameBytes.CopyTo(updatedData, originalData.Length + 8);

    var updatedSize = checked(keysNode.Box.HeaderSize + updatedData.LongLength);
    var delta = updatedSize - keysNode.Box.Size;

    keysNode.Data = updatedData;
    keysNode.Box = new IsoBmffBox(keysNode.Box.Offset, keysNode.Box.HeaderSize, updatedSize, keysNode.Box.Type);

    if (delta != 0)
      editedBoxes.UpdateParentSizes(keysNode.Parent, delta);

    Array.Resize(ref keyNames, keyNames.Length + 1);
    keyNames[index] = key;
    _keyNames = keyNames;

    return index;
  }

  private byte[] _getKeysData(IsoBmffBoxNode keysNode) {
    if (keysNode.Data is { } data)
      return data;

    var length = checked((int)keysNode.Box.DataSize);
    var result = new byte[length];

    reader._stream.Position = keysNode.Box.DataOffset;
    reader._stream.ReadExactly(result);

    return result;
  }

  private void _readValue(IsoBmffBox item, string key, List<IsoBmffMetadataEntry> result) {
    if (reader.FindChild(item, IsoBmffTypes.Data) is not { } data || data.DataSize < 8)
      return;

    var length = checked((int)data.DataSize - 8);

    reader._stream.Position = data.DataOffset + 8;

    var buffer = new byte[length];
    reader._stream.ReadExactly(buffer);

    result.Add(new IsoBmffMetadataEntry(key, Encoding.UTF8.GetString(buffer), item));
  }
}