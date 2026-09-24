using System.Collections.Generic;

namespace MH.Utils.Imaging.IsoBmff;

internal readonly struct IsoBmffBoxNode(IsoBmffBox box, int parent) {
  public IsoBmffBox Box { get; } = box;
  public int Parent { get; } = parent;
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
}