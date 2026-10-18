using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffEditor(IsoBmffReader reader) {
  private List<IsoBmffBoxNode>? _originalBoxes;
  private List<IsoBmffBoxNode>? _editedBoxes;

  public List<IsoBmffBoxNode> GetEditedBoxes() {
    if (_editedBoxes != null) return _editedBoxes;

    _originalBoxes = reader._readBoxes();
    _editedBoxes = [.. _originalBoxes.Select(x => new IsoBmffBoxNode(x.Box, x.Parent, x))];

    return _editedBoxes;
  }

  internal List<IsoBmffBoxNode> _getOriginalBoxes() {
    GetEditedBoxes();

    return _originalBoxes!;
  }

  public void UpdateEntry(IsoBmffMetadataEntry entry, string value, string key) {
    var editedBoxes = GetEditedBoxes();
    var itemIndex = _findEntryIndex(entry, key);

    var dataIndex = editedBoxes.FindIndex(
      x => x.Parent == itemIndex && x.Box.Type == IsoBmffTypes.Data);

    if (dataIndex < 0)
      throw new InvalidOperationException($"The {key} data box was not found.");

    var dataNode = editedBoxes[dataIndex];
    var prefix = _getDataPrefix(dataNode);
    var payload = _createData(value, prefix);

    editedBoxes.ReplaceData(dataNode, payload);

    entry.Node = editedBoxes[itemIndex];
    entry.Value = value;
  }

  private int _findEntryIndex(IsoBmffMetadataEntry entry, string key) {
    var editedBoxes = GetEditedBoxes();
    var itemIndex = entry.Node is { } node
      ? editedBoxes.IndexOf(node)
      : editedBoxes.FindIndex(entry.Item);

    if (itemIndex < 0)
      throw new InvalidOperationException($"The {key} box was not found.");

    return itemIndex;
  }

  private static byte[] _createData(string value, byte[]? prefix = null) {
    var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
    var result = new byte[checked(8 + valueBytes.Length)];

    if (prefix is not null)
      prefix.CopyTo(result, 0);
    else
      BinaryPrimitives.WriteUInt32BigEndian(result, 1);

    valueBytes.CopyTo(result, 8);

    return result;
  }

  private byte[] _getDataPrefix(IsoBmffBoxNode dataNode) {
    if (dataNode.Data is { Length: >= 8 } data)
      return data[..8];

    var prefix = new byte[8];

    reader._stream.Position = dataNode.Box.DataOffset;
    reader._stream.ReadExactly(prefix);

    return prefix;
  }

  public IsoBmffMetadataEntry AddEntry(string key, string value, uint itemType, IsoBmffBox ilst) {
    var editedBoxes = GetEditedBoxes();
    var payload = _createData(value);
    var ilstIndex = editedBoxes.FindIndex(ilst, throwIfNotFound: true);
    var itemIndex = editedBoxes.InsertBox(itemType, 8, ilstIndex);
    var itemNode = editedBoxes[itemIndex];

    editedBoxes.InsertBox(IsoBmffTypes.Data, 8 + payload.LongLength, itemIndex, payload);

    return new IsoBmffMetadataEntry(key, value, itemNode.Box, itemNode);
  }

  public void RemoveEntry(IsoBmffMetadataEntry entry, string key) {
    var itemIndex = _findEntryIndex(entry, key);
    GetEditedBoxes().RemoveBox(itemIndex);
  }
}