using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffEditor(IsoBmffReader reader) {
  private List<IsoBmffBoxNode>? _originalBoxes;
  private List<IsoBmffBoxNode>? _editedBoxes;

  internal List<IsoBmffBoxNode> _getEditedBoxes() {
    if (_editedBoxes is not null) return _editedBoxes;

    _originalBoxes = reader._readBoxes();
    _editedBoxes = [.. _originalBoxes.Select(x => new IsoBmffBoxNode(x.Box, x.Parent, x))];

    return _editedBoxes;
  }

  internal List<IsoBmffBoxNode> _getOriginalBoxes() {
    _getEditedBoxes();

    return _originalBoxes!;
  }

  internal void _updateEntry(IsoBmffMetadataEntry entry, string value, string key) {
    var editedBoxes = _getEditedBoxes();
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

  internal static byte[] _createData(string value, byte[]? prefix = null) {
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

  public void RemoveEntry(IsoBmffMetadataEntry entry, string key) {
    var editedBoxes = _getEditedBoxes();
    var itemIndex = entry.Node is { } node
      ? editedBoxes.IndexOf(node)
      : editedBoxes.FindIndex(entry.Item);

    if (itemIndex < 0)
      throw new InvalidOperationException($"The {key} box was not found.");

    editedBoxes.RemoveBox(itemIndex);
  }
}