using System;
using System.Collections.Generic;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffBoxNode(IsoBmffBox box, int parent, IsoBmffBoxNode? originalNode = null, byte[]? data = null) {
  public IsoBmffBox Box { get; set; } = box;
  public int Parent { get; set; } = parent;
  public IsoBmffBoxNode? OriginalNode { get; } = originalNode;

  // Leaf payload bytes, or container prefix bytes before its children.
  public byte[]? Data { get; set; } = data;

  public IsoBmffBoxNode Identity => OriginalNode?.Identity ?? this;

  public bool HasSameIdentityAs(IsoBmffBoxNode other) =>
    ReferenceEquals(Identity, other.Identity);
}

internal static class IsoBmffBoxNodeExtensions {
  public static int FindIndexByIdentity(this IList<IsoBmffBoxNode> boxes, IsoBmffBoxNode node, bool throwIfNotFound = false) {
    for (var i = 0; i < boxes.Count; i++)
      if (boxes[i].HasSameIdentityAs(node))
        return i;

    if (throwIfNotFound)
      throw new InvalidOperationException("A box with the same identity was not found.");

    return -1;
  }

  public static IsoBmffBoxNode? FindByIdentity(this IList<IsoBmffBoxNode> boxes, IsoBmffBoxNode node) {
    var index = boxes.FindIndexByIdentity(node);

    return index < 0 ? null : boxes[index];
  }

  public static int FindIndex(this IList<IsoBmffBoxNode> boxes, uint type, int parent = -1, bool throwIfNotFound = false) {
    for (var i = 0; i < boxes.Count; i++) {
      var node = boxes[i];

      if (node.Parent == parent && node.Box.Type == type)
        return i;
    }

    if (throwIfNotFound)
      throw new InvalidOperationException($"The {IsoBmffTypes.GetTypeName(type)} box was not found.");

    return -1;
  }

  public static int FindIndex(this IList<IsoBmffBoxNode> boxes, IsoBmffBox? box, bool throwIfNotFound = false) {
    if (box is not { } boxValue) {
      if (throwIfNotFound)
        throw new InvalidOperationException("The box was not found because it is null.");

      return -1;
    }

    for (var i = 0; i < boxes.Count; i++) {
      var node = boxes[i];

      if (node.Box.Offset == boxValue.Offset && node.Box.Type == boxValue.Type)
        return i;
    }

    if (throwIfNotFound)
      throw new InvalidOperationException($"The {IsoBmffTypes.GetTypeName(boxValue.Type)} box was not found.");

    return -1;
  }

  public static int FindOrInsert(this List<IsoBmffBoxNode> boxes, uint type, long size, int parentIndex, byte[]? data = null) {
    var index = boxes.FindIndex(type, parentIndex);
    if (index < 0)
      index = boxes.InsertBox(type, size, parentIndex, data);

    return index;
  }

  public static void UpdateParentSizes(this IList<IsoBmffBoxNode> boxes, int parentIndex, long delta) {
    while (parentIndex >= 0) {
      var parent = boxes[parentIndex];

      parent.Box = new IsoBmffBox(
        parent.Box.Offset,
        parent.Box.HeaderSize,
        parent.Box.Size + delta,
        parent.Box.Type);

      parentIndex = parent.Parent;
    }
  }

  public static int InsertBox(this List<IsoBmffBoxNode> boxes, IsoBmffBox box, int parentIndex, byte[]? data = null) {
    var index = parentIndex + 1;

    while (index < boxes.Count && _isDescendantOf(boxes[index], parentIndex, boxes))
      index++;

    for (var i = 0; i < boxes.Count; i++)
      if (boxes[i].Parent >= index)
        boxes[i].Parent++;

    boxes.Insert(index, new IsoBmffBoxNode(box, parentIndex, data: data));
    boxes.UpdateParentSizes(parentIndex, box.Size);

    return index;
  }

  public static int InsertBox(this List<IsoBmffBoxNode> boxes, uint type, long size, int parentIndex, byte[]? data = null) =>
    boxes.InsertBox(new IsoBmffBox(-1, 8, size, type), parentIndex, data);

  public static IsoBmffBoxNode InsertTopLevelBox(this List<IsoBmffBoxNode> boxes, IsoBmffBoxNode node) {
    var index = boxes.FindIndex(x => x.Parent < 0 && x.Box.Type == IsoBmffTypes.Mdat);

    if (index < 0)
      index = boxes.Count;

    for (var i = 0; i < boxes.Count; i++)
      if (boxes[i].Parent >= index)
        boxes[i].Parent++;

    boxes.Insert(index, node);

    return node;
  }

  private static bool _isDescendantOf(IsoBmffBoxNode node, int ancestorIndex, List<IsoBmffBoxNode> boxes) {
    var parent = node.Parent;

    while (parent >= 0) {
      if (parent == ancestorIndex)
        return true;

      parent = boxes[parent].Parent;
    }

    return false;
  }

  public static void RemoveBox(this List<IsoBmffBoxNode> boxes, int index) {
    var parentIndex = boxes[index].Parent;
    var size = boxes[index].Box.Size;

    var end = index + 1;

    while (end < boxes.Count && boxes[end].Parent >= index) end++;

    var count = end - index;

    boxes.RemoveRange(index, count);

    for (var i = 0; i < boxes.Count; i++)
      if (boxes[i].Parent >= end)
        boxes[i].Parent -= count;

    boxes.UpdateParentSizes(parentIndex, -size);
  }

  public static void ReplaceData(this IList<IsoBmffBoxNode> boxes, IsoBmffBoxNode node, byte[] data) {
    var box = node.Box;
    var newSize = checked(box.HeaderSize + data.LongLength);
    var delta = newSize - box.Size;

    node.Data = data;
    node.Box = new IsoBmffBox(box.Offset, box.HeaderSize, newSize, box.Type);

    if (delta != 0)
      boxes.UpdateParentSizes(node.Parent, delta);
  }
}