using System;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed record IsoBmffMove(long SourceOffset, long DestinationOffset, long Length);

internal sealed record IsoBmffWrite(long Offset, long Length);

// TODO not used
internal sealed class IsoBmffLayoutPlan {
  public List<IsoBmffMove> Moves { get; } = [];
  public List<IsoBmffWrite> Writes { get; } = [];
  public bool RequiresRewrite { get; internal set; }
}

// TODO not used
internal sealed record IsoBmffRegion(long Offset, long Size) {
  public long End => Offset + Size;
}

internal sealed class IsoBmffLayoutPlanner(List<IsoBmffBoxNode> boxes, Func<IsoBmffBox, long> getBoxChildrenOffset) {
  private readonly List<IsoBmffBoxNode> _boxes = boxes;
  private readonly Func<IsoBmffBox, long> _getBoxChildrenOffset = getBoxChildrenOffset;

  private IsoBmffBoxNode _getRoot(IsoBmffBoxNode node) {
    while (node.Parent >= 0)
      node = _boxes[node.Parent];

    return node;
  }

  private void _addLayout(List<IsoBmffLayoutBox> layout, IsoBmffBoxNode node, long offset, Dictionary<IsoBmffBoxNode, long> deltas) {
    var size = node.Box.Size;

    if (deltas.TryGetValue(node, out var delta))
      size += delta;

    layout.Add(new IsoBmffLayoutBox(node, offset, size));

    var children = _getChildren(node);
    var childOffset = offset + node.Box.HeaderSize + _getBoxChildrenOffset(node.Box);

    foreach (var child in children) {
      var childDelta = deltas.TryGetValue(child, out var d) ? d : 0;
      var childSize = child.Box.Size + childDelta;

      _addLayout(layout, child, childOffset, deltas);
      childOffset += childSize;
    }
  }

  private List<IsoBmffBoxNode> _getChildren(IsoBmffBoxNode parent) {
    var parentIndex = _boxes.IndexOf(parent);

    return [.. _boxes.Where(x => x.Parent == parentIndex)];
  }

  internal IsoBmffLayout CreateOriginalLayout() {
    var result = new List<IsoBmffLayoutBox>();

    foreach (var box in _boxes)
      result.Add(new(box, box.Box.Offset, box.Box.Size));

    return new IsoBmffLayout(result);
  }

  internal IsoBmffLayout CreateEditedLayout(IsoBmffLayout original, IsoBmffBoxNode editedBox) {
    var root = _getRoot(editedBox);
    var originalRoot = original[root];

    var delta = editedBox.Box.Size - original[editedBox].Size;

    var before = _getPreviousTopLevel(root);
    var after = _getNextTopLevel(root);

    var beforeFree = _isFree(before) ? original[before] : null;
    var afterFree = _isFree(after) ? original[after] : null;

    var newSize = originalRoot.Size + delta;
    var newOffset = originalRoot.Offset;

    if (delta > 0) {
      var fromAfter = Math.Min(delta, afterFree?.Size ?? 0);
      var remaining = delta - fromAfter;
      var fromBefore = Math.Min(remaining, beforeFree?.Size ?? 0);

      remaining -= fromBefore;

      if (remaining != 0)
        throw new InvalidOperationException("Not enough adjacent free space.");

      newOffset -= fromBefore;
    }

    var result = new List<IsoBmffLayoutBox>();

    foreach (var topLevel in _getTopLevelBoxes()) {
      if (topLevel == root) {
        _addEditedTree(result, topLevel, newOffset);
        continue;
      }

      if (topLevel == beforeFree?.Node) {
        var consumed = originalRoot.Offset - newOffset;
        var size = beforeFree.Size - consumed;

        if (size > 0)
          result.Add(new(topLevel, beforeFree.Offset, size));

        continue;
      }

      if (topLevel == afterFree?.Node) {
        var consumed = newOffset + newSize - originalRoot.End;
        var size = afterFree.Size - consumed;

        if (size > 0)
          result.Add(new(topLevel, afterFree.Offset + consumed, size));

        continue;
      }

      result.Add(original[topLevel]);
    }

    return new IsoBmffLayout(result);
  }

  private IsoBmffBoxNode? _getPreviousTopLevel(IsoBmffBoxNode node) {
    var topLevel = _getTopLevelBoxes();
    var index = topLevel.IndexOf(node);

    return index > 0
      ? topLevel[index - 1]
      : null;
  }

  private IsoBmffBoxNode? _getNextTopLevel(IsoBmffBoxNode node) {
    var topLevel = _getTopLevelBoxes();
    var index = topLevel.IndexOf(node);

    return index >= 0 && index + 1 < topLevel.Count
      ? topLevel[index + 1]
      : null;
  }

  private List<IsoBmffBoxNode> _getTopLevelBoxes() =>
    [.. _boxes.Where(x => x.Parent < 0)];

  private void _addEditedTree(List<IsoBmffLayoutBox> result, IsoBmffBoxNode node, long offset) {
    result.Add(new(node, offset, node.Box.Size));

    var children = _getChildren(node);

    if (children.Count == 0) return;

    var childOffset = offset + node.Box.HeaderSize + _getBoxChildrenOffset(node.Box);

    foreach (var child in children) {
      _addEditedTree(result, child, childOffset);
      childOffset += child.Box.Size;
    }
  }

  private static bool _isFree(IsoBmffBoxNode? node) =>
    node?.Box.Type == IsoBmffTypes.Free;
}