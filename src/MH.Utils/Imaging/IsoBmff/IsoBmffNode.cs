using System;
using System.Collections.Generic;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffNode(IsoBmffBox box, int parent, IsoBmffNode? originalNode = null, byte[]? data = null) {
  public IsoBmffBox Box { get; set; } = box;
  public int Parent { get; set; } = parent;
  public IsoBmffNode? OriginalNode { get; } = originalNode;

  // Leaf payload bytes, or container prefix bytes before its children.
  public byte[]? Data { get; set; } = data;

  public IsoBmffNode Identity => OriginalNode?.Identity ?? this;

  public bool HasSameIdentityAs(IsoBmffNode other) =>
    ReferenceEquals(Identity, other.Identity);
}

internal static class IsoBmffNodeExtensions {
  public static int FindIndexByIdentity(this IList<IsoBmffNode> nodes, IsoBmffNode node, bool throwIfNotFound = false) {
    for (var i = 0; i < nodes.Count; i++)
      if (nodes[i].HasSameIdentityAs(node))
        return i;

    if (throwIfNotFound)
      throw new InvalidOperationException("A node with the same identity was not found.");

    return -1;
  }

  public static IsoBmffNode? FindByIdentity(this IList<IsoBmffNode> nodes, IsoBmffNode node) {
    var index = nodes.FindIndexByIdentity(node);

    return index < 0 ? null : nodes[index];
  }

  public static int FindIndex(this IList<IsoBmffNode> nodes, uint type, int parent = -1, bool throwIfNotFound = false) {
    for (var i = 0; i < nodes.Count; i++) {
      var node = nodes[i];

      if (node.Parent == parent && node.Box.Type == type)
        return i;
    }

    if (throwIfNotFound)
      throw new InvalidOperationException($"The {IsoBmffTypes.GetTypeName(type)} node was not found.");

    return -1;
  }

  public static int FindIndex(this IList<IsoBmffNode> nodes, IsoBmffBox? box, bool throwIfNotFound = false) {
    if (box is not { } boxValue) {
      if (throwIfNotFound)
        throw new InvalidOperationException("The node was not found because it is null.");

      return -1;
    }

    for (var i = 0; i < nodes.Count; i++) {
      var node = nodes[i];

      if (node.Box.Offset == boxValue.Offset && node.Box.Type == boxValue.Type)
        return i;
    }

    if (throwIfNotFound)
      throw new InvalidOperationException($"The {IsoBmffTypes.GetTypeName(boxValue.Type)} node was not found.");

    return -1;
  }

  public static int FindOrInsert(this List<IsoBmffNode> nodes, uint type, long size, int parentIndex, byte[]? data = null) {
    var index = nodes.FindIndex(type, parentIndex);
    if (index < 0)
      index = nodes.InsertNode(type, size, parentIndex, data);

    return index;
  }

  public static void UpdateParentSizes(this IList<IsoBmffNode> nodes, int parentIndex, long delta) {
    while (parentIndex >= 0) {
      var parent = nodes[parentIndex];

      parent.Box = new IsoBmffBox(
        parent.Box.Offset,
        parent.Box.HeaderSize,
        parent.Box.Size + delta,
        parent.Box.Type);

      parentIndex = parent.Parent;
    }
  }

  public static int InsertNode(this List<IsoBmffNode> nodes, IsoBmffBox box, int parentIndex, byte[]? data = null) {
    var index = parentIndex + 1;

    while (index < nodes.Count && _isDescendantOf(nodes[index], parentIndex, nodes))
      index++;

    nodes._insertNode(index, new IsoBmffNode(box, parentIndex, data: data));
    nodes.UpdateParentSizes(parentIndex, box.Size);

    return index;
  }

  public static int InsertNode(this List<IsoBmffNode> nodes, uint type, long size, int parentIndex, byte[]? data = null) =>
    nodes.InsertNode(new IsoBmffBox(-1, 8, size, type), parentIndex, data);

  public static IsoBmffNode InsertTopLevelNode(this List<IsoBmffNode> nodes, IsoBmffNode node) {
    var index = nodes.FindIndex(x => x.Parent < 0 && x.Box.Type == IsoBmffTypes.Mdat);

    if (index < 0)
      index = nodes.Count;

    nodes._insertNode(index, node);

    return node;
  }

  private static void _insertNode(this List<IsoBmffNode> nodes, int index, IsoBmffNode node) {
    for (var i = 0; i < nodes.Count; i++)
      if (nodes[i].Parent >= index)
        nodes[i].Parent++;

    nodes.Insert(index, node);
  }

  private static bool _isDescendantOf(IsoBmffNode node, int ancestorIndex, List<IsoBmffNode> nodes) {
    var parent = node.Parent;

    while (parent >= 0) {
      if (parent == ancestorIndex)
        return true;

      parent = nodes[parent].Parent;
    }

    return false;
  }

  public static void RemoveNode(this List<IsoBmffNode> nodes, int index) {
    var parentIndex = nodes[index].Parent;
    var size = nodes[index].Box.Size;

    var end = index + 1;

    while (end < nodes.Count && nodes[end].Parent >= index) end++;

    var count = end - index;

    nodes.RemoveRange(index, count);

    for (var i = 0; i < nodes.Count; i++)
      if (nodes[i].Parent >= end)
        nodes[i].Parent -= count;

    nodes.UpdateParentSizes(parentIndex, -size);
  }

  public static void ReplaceData(this IList<IsoBmffNode> nodes, IsoBmffNode node, byte[] data) {
    var box = node.Box;
    var newSize = checked(box.HeaderSize + data.LongLength);
    var delta = newSize - box.Size;

    node.Data = data;
    node.Box = new IsoBmffBox(box.Offset, box.HeaderSize, newSize, box.Type);

    if (delta != 0)
      nodes.UpdateParentSizes(node.Parent, delta);
  }
}