using System;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed record IsoBmffMove(long SourceOffset, long DestinationOffset, long Length);

internal sealed record IsoBmffWrite(long Offset, long Length);

internal sealed class IsoBmffBoxEdit(IsoBmffBoxNode boxNode, long newSize) {
  public IsoBmffBoxNode BoxNode { get; } = boxNode;
  public long NewSize { get; } = newSize;
  public long SizeDelta => NewSize - BoxNode.Box.Size;
}

internal sealed class IsoBmffLayoutPlan {
  public List<IsoBmffMove> Moves { get; } = [];
  public List<IsoBmffWrite> Writes { get; } = [];
  public bool RequiresRewrite { get; internal set; }
}

internal sealed record IsoBmffRegion(long Offset, long Size) {
  public long End => Offset + Size;
}

internal sealed class IsoBmffLayoutPlanner(List<IsoBmffBoxNode> boxes, Func<IsoBmffBox, long> getBoxChildrenOffset) {
  private readonly List<IsoBmffBoxNode> _boxes = boxes;
  private readonly Func<IsoBmffBox, long> _getBoxChildrenOffset = getBoxChildrenOffset;

  public IsoBmffLayout PlanLayout(IsoBmffBoxEdit edit) {
    var deltas = _getSizeDeltas(edit);
    var root = _getRoot(edit.BoxNode);
    var layout = new List<IsoBmffLayoutBox>();

    foreach (var box in _boxes) {
      if (box.Parent >= 0)
        continue;

      _addLayout(layout, box, box.Box.Offset, deltas);
    }

    return new IsoBmffLayout(layout);
  }

  private Dictionary<IsoBmffBoxNode, long> _getSizeDeltas(IsoBmffBoxEdit edit) {
    var deltas = new Dictionary<IsoBmffBoxNode, long>();
    var delta = edit.SizeDelta;
    var node = edit.BoxNode;

    while (true) {
      deltas[node] = delta;

      if (node.Parent < 0)
        break;

      node = _boxes[node.Parent];
    }

    return deltas;
  }

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

  internal IsoBmffLayout CreateEditedLayout(IsoBmffLayout original, IsoBmffBoxEdit edit) {
    var deltas = _getSizeDeltas(edit);
    var root = _getRoot(edit.BoxNode);
    var originalRoot = original[root];

    var before = _getPreviousTopLevel(root);
    var after = _getNextTopLevel(root);

    var beforeLayout = before is not null ? original[before] : null;
    var afterLayout = after is not null ? original[after] : null;

    var beforeFree = _isFree(before) ? beforeLayout : null;
    var afterFree = _isFree(after) ? afterLayout : null;

    var newSize = originalRoot.Size + edit.SizeDelta;
    var newOffset = originalRoot.Offset;

    if (edit.SizeDelta > 0) {
      var growth = edit.SizeDelta;
      var fromAfter = Math.Min(growth, afterFree?.Size ?? 0);
      var remaining = growth - fromAfter;
      var fromBefore = Math.Min(remaining, beforeFree?.Size ?? 0);

      remaining -= fromBefore;

      if (remaining != 0)
        throw new InvalidOperationException("Not enough adjacent free space.");

      newOffset -= fromBefore;
    }

    var result = new List<IsoBmffLayoutBox>();

    foreach (var topLevel in _getTopLevelBoxes()) {
      if (topLevel == root) {
        _addEditedTree(result, topLevel, newOffset, deltas);

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

  private void _addEditedTree(List<IsoBmffLayoutBox> result, IsoBmffBoxNode node, long offset, Dictionary<IsoBmffBoxNode, long> deltas) {
    var size = node.Box.Size + (deltas.TryGetValue(node, out var delta) ? delta : 0);

    result.Add(new IsoBmffLayoutBox(node, offset, size));

    var children = _getChildren(node);

    if (children.Count == 0) return;

    var childOffset = offset + node.Box.HeaderSize + _getBoxChildrenOffset(node.Box);

    foreach (var child in children) {
      var childSize = child.Box.Size + (deltas.TryGetValue(child, out var childDelta) ? childDelta : 0);
      _addEditedTree(result, child, childOffset, deltas);
      childOffset += childSize;
    }
  }

  private static bool _isFree(IsoBmffBoxNode? node) =>
    node?.Box.Type == IsoBmffTypes.Free;
}