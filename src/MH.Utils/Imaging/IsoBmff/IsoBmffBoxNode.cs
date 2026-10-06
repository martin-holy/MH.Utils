using System.Collections.Generic;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffBoxNode(IsoBmffBox box, int parent) {
  public IsoBmffBox Box { get; set; } = box;
  public int Parent { get; set; } = parent;
}

internal static class IsoBmffBoxNodeExtensions {
  public static int FindIndex(this IList<IsoBmffBoxNode> boxes, uint type, int parent = -1) {
    for (var i = 0; i < boxes.Count; i++) {
      var node = boxes[i];

      if (node.Parent == parent && node.Box.Type == type)
        return i;
    }

    return -1;
  }

  public static int FindIndex(this IList<IsoBmffBoxNode> boxes, IsoBmffBox? box) {
    if (box is not { } boxValue) return -1;

    for (var i = 0; i < boxes.Count; i++) {
      var node = boxes[i];

      if (node.Box.Offset == boxValue.Offset && node.Box.Type == boxValue.Type)
        return i;
    }

    return -1;
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

  public static int InsertBox(this List<IsoBmffBoxNode> boxes, IsoBmffBox box, int parentIndex) {
    var index = parentIndex + 1;

    while (index < boxes.Count && _isDescendantOf(boxes[index], parentIndex, boxes))
      index++;

    for (var i = 0; i < boxes.Count; i++)
      if (boxes[i].Parent >= index)
        boxes[i].Parent++;

    boxes.Insert(index, new IsoBmffBoxNode(box, parentIndex));
    boxes.UpdateParentSizes(parentIndex, box.Size);

    return index;
  }

  public static int InsertBox(this List<IsoBmffBoxNode> boxes, uint type, long size, int parentIndex) =>
    boxes.InsertBox(new IsoBmffBox(-1, 8, size, type), parentIndex);

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
}