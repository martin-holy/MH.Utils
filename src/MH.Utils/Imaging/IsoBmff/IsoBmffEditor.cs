using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffEditor(IsoBmffReader reader) {
  private List<IsoBmffNode>? _originalNodes;
  private List<IsoBmffNode>? _editedNodes;

  public List<IsoBmffNode> GetEditedNodes() {
    if (_editedNodes != null) return _editedNodes;

    _originalNodes = reader._readNodes();
    _editedNodes = [.. _originalNodes.Select(x => new IsoBmffNode(x.Box, x.Parent, x))];

    return _editedNodes;
  }

  internal List<IsoBmffNode> _getOriginalNodes() {
    GetEditedNodes();

    return _originalNodes!;
  }

  public void UpdateEntry(IsoBmffMetadataEntry entry, string value, string key) {
    var editedNodes = GetEditedNodes();
    var itemIndex = _findEntryIndex(entry, key);

    var dataIndex = editedNodes.FindIndex(
      x => x.Parent == itemIndex && x.Box.Type == IsoBmffTypes.Data);

    if (dataIndex < 0)
      throw new InvalidOperationException($"The {key} data node was not found.");

    var dataNode = editedNodes[dataIndex];
    var prefix = _getDataPrefix(dataNode);
    var payload = _createData(value, prefix);

    editedNodes.ReplaceData(dataNode, payload);

    entry.Node = editedNodes[itemIndex];
    entry.Value = value;
  }

  private int _findEntryIndex(IsoBmffMetadataEntry entry, string key) {
    var editedNodes = GetEditedNodes();
    var itemIndex = entry.Node is { } node
      ? editedNodes.IndexOf(node)
      : editedNodes.FindIndex(entry.Item);

    if (itemIndex < 0)
      throw new InvalidOperationException($"The {key} node was not found.");

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

  private byte[] _getDataPrefix(IsoBmffNode dataNode) {
    if (dataNode.Data is { Length: >= 8 } data)
      return data[..8];

    var prefix = new byte[8];

    reader._stream.Position = dataNode.Box.DataOffset;
    reader._stream.ReadExactly(prefix);

    return prefix;
  }

  public IsoBmffMetadataEntry AddEntry(string key, string value, uint itemType, IsoBmffBox ilst) {
    var editedNodes = GetEditedNodes();
    var payload = _createData(value);
    var ilstIndex = editedNodes.FindIndex(ilst, throwIfNotFound: true);
    var itemIndex = editedNodes.InsertNode(itemType, 8, ilstIndex);
    var itemNode = editedNodes[itemIndex];

    editedNodes.InsertNode(IsoBmffTypes.Data, 8 + payload.LongLength, itemIndex, payload);

    return new IsoBmffMetadataEntry(key, value, itemNode.Box, itemNode);
  }

  public void RemoveEntry(IsoBmffMetadataEntry entry, string key) {
    var itemIndex = _findEntryIndex(entry, key);
    GetEditedNodes().RemoveNode(itemIndex);
  }
}