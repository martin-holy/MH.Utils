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

      if (item.Type <= 0xFFFF
        && item.Type < keyNames.Length
        && keyNames[item.Type] is { } key
        && reader.ReadDataString(item) is { } value)
          result.Add(new IsoBmffMetadataEntry(key, value, item));

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
    var entries = _getEntries();

    var entryIndex = entries.FindIndex(x => x.Key == key);
    if (entryIndex >= 0) {
      var entry = entries[entryIndex];
      if (value == null) {
        editor.RemoveEntry(entry, key);
        entries.RemoveAt(entryIndex);
      }
      else {
        editor._updateEntry(entry, value, key);
      }

      return;
    }

    if (value == null) return;

    var keyNames = _readKeys();
    var keyIndex = Array.IndexOf(keyNames, key);
    var editedBoxes = editor._getEditedBoxes();

    if (keyIndex < 0)
      keyIndex = _addKey(key, editedBoxes);

    entries.Add(editor.AddEntry(key, value, (uint)keyIndex, ilst));
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
    var keysIndex = editedBoxes.FindIndex(keys, throwIfNotFound: true);
    var keysNode = editedBoxes[keysIndex];
    var originalData = _getKeysData(keysNode);
    var updatedData = new byte[checked(originalData.Length + entrySize)];

    originalData.CopyTo(updatedData, 0);
    BinaryPrimitives.WriteUInt32BigEndian(updatedData.AsSpan(4), (uint)index);
    BinaryPrimitives.WriteUInt32BigEndian(updatedData.AsSpan(originalData.Length), (uint)entrySize);
    BinaryPrimitives.WriteUInt32BigEndian(updatedData.AsSpan(originalData.Length + 4), 0x6D647461);
    nameBytes.CopyTo(updatedData, originalData.Length + 8);

    editedBoxes.ReplaceData(keysNode, updatedData);

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
}