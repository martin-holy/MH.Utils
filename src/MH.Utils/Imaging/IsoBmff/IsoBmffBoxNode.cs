using System.Collections.Generic;

namespace MH.Utils.Imaging.IsoBmff;

internal class IsoBmffBoxNode(IsoBmffBox box, int parent) {
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

  public static IsoBmffBoxNode? FindNode(this IList<IsoBmffBoxNode> boxes, uint type, int parent = -1) {
    var index = FindIndex(boxes, type, parent);
    return index == -1 ? null : boxes[index];
  }

  public static IsoBmffBox? FindBox(this IList<IsoBmffBoxNode> boxes, uint type, int parent = -1) {
    var index = FindIndex(boxes, type, parent);
    return index == -1 ? null : boxes[index].Box;
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

    return index;
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
}