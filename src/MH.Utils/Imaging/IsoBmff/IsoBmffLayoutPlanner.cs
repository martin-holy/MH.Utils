using System;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed record IsoBmffMove(long SourceOffset, long DestinationOffset, long Length);

internal sealed record IsoBmffWrite(long Offset, long Length);

internal sealed class IsoBmffLayoutPlan(IsoBmffLayout layout, bool requiresFullRewrite) {
  public IsoBmffLayout Layout { get; } = layout;
  public bool RequiresFullRewrite { get; } = requiresFullRewrite;
}

internal sealed class IsoBmffLayoutPlanner {
  private const long DefaultExpensiveBoxThreshold = 1024 * 1024;
  private const long FreeHeaderSize = 8;

  private readonly List<IsoBmffBoxNode> _boxes;
  private readonly Func<IsoBmffBox, long> _getBoxChildrenOffset;
  private readonly long _expensiveBoxThreshold;

  public IsoBmffLayoutPlanner(
    IEnumerable<IsoBmffBoxNode> boxes,
    Func<IsoBmffBox, long> getBoxChildrenOffset,
    long expensiveBoxThreshold = DefaultExpensiveBoxThreshold) {

    _boxes = boxes.ToList();
    _getBoxChildrenOffset = getBoxChildrenOffset;
    _expensiveBoxThreshold = expensiveBoxThreshold;
  }

  public IsoBmffLayout CreateOriginalLayout() =>
    new(_boxes
      .Where(x => x.OriginalNode is not null || x.Box.Offset >= 0)
      .Select(x => {
        var node = x.OriginalNode ?? x;
        return new IsoBmffLayoutBox(node, node.Box.Offset, node.Box.Size);
      }));

  public IsoBmffLayoutPlan CreateEditedLayout(IsoBmffLayout original, IsoBmffBoxNode editedBox) =>
    CreateEditedLayout(original, [editedBox]);

  public IsoBmffLayoutPlan CreateEditedLayout(IsoBmffLayout original, IEnumerable<IsoBmffBoxNode> editedBoxes) {
    var editedRoots = editedBoxes.Select(_getTopLevelNode).Distinct().ToList();
    if (editedRoots.Count == 0) return new IsoBmffLayoutPlan(original, false);

    var work = original.TopLevelBoxes.Select(x => new _WorkBox(x.Node, x.Offset, x.Size)).ToList();

    foreach (var root in editedRoots.OrderBy(x => original[x].Offset)) {
      if (!_applyEdit(work, original, root))
        return new IsoBmffLayoutPlan(_createFullRewriteLayout(original, editedRoots), true);
    }

    return new IsoBmffLayoutPlan(_createLayout(original, work, editedRoots), false);
  }

  private bool _applyEdit(List<_WorkBox> work, IsoBmffLayout original, IsoBmffBoxNode root) {
    if (!original.TryGetValue(root, out var originalRoot))
      throw new InvalidOperationException("Edited box does not belong to the original layout.");

    var index = work.FindIndex(x => ReferenceEquals(x.Node, root));
    if (index < 0)
      throw new InvalidOperationException("Edited box is not present in the working layout.");

    var delta = root.Box.Size - originalRoot!.Size;

    if (delta > 0) {
      if (!_grow(work, ref index, delta)) return false;
    }
    else if (delta < 0 && !_shrink(work, index, -delta)) {
      return false;
    }

    if (work[index].Size != root.Box.Size)
      throw new InvalidOperationException("Edited box size does not match the planned layout.");

    return true;
  }

  private bool _grow(List<_WorkBox> work, ref int rootIndex, long amount) {
    if (amount <= 0) return true;

    var remaining = _consumeFreeAfter(work, rootIndex, amount);
    if (remaining == 0) return true;

    remaining = _consumeFreeBefore(work, ref rootIndex, remaining);
    return remaining == 0;
  }

  private long _consumeFreeAfter(List<_WorkBox> work, int rootIndex, long amount) {
    while (amount > 0) {
      var freeIndex = _findFreeAfter(work, rootIndex);

      if (freeIndex < 0) return amount;

      var free = work[freeIndex];
      var count = Math.Min(amount, free.Size);

      work[rootIndex].Size += count;

      // Everything between the edited box and the free box has to
      // move right because the edited box grows into that space.
      for (var i = rootIndex + 1; i < freeIndex; i++)
        work[i].Offset += count;

      free.Offset += count;
      free.Size -= count;

      amount -= count;

      if (free.Size == 0)
        work.RemoveAt(freeIndex);
    }

    return amount;
  }

  private long _consumeFreeBefore(List<_WorkBox> work, ref int rootIndex, long amount) {
    while (amount > 0) {
      var freeIndex = _findFreeBefore(work, rootIndex);
      if (freeIndex < 0) return amount;

      var free = work[freeIndex];
      var count = Math.Min(amount, free.Size);

      free.Size -= count;

      for (var i = freeIndex + 1; i < rootIndex; i++)
        work[i].Offset -= count;

      work[rootIndex].Offset -= count;
      work[rootIndex].Size += count;
      amount -= count;

      if (free.Size == 0) {
        work.RemoveAt(freeIndex);
        rootIndex--;
      }
    }

    return amount;
  }

  private bool _shrink(List<_WorkBox> work, int rootIndex, long amount) {
    if (amount <= 0) return true;

    var freeIndex = _findFreeAfter(work, rootIndex);
    if (freeIndex >= 0) {
      var free = work[freeIndex];

      work[rootIndex].Size -= amount;

      for (var i = rootIndex + 1; i < freeIndex; i++)
        work[i].Offset -= amount;

      free.Offset -= amount;
      free.Size += amount;

      return true;
    }

    if (amount < FreeHeaderSize) return false;

    var root = work[rootIndex];
    root.Size -= amount;

    var freeNode = new IsoBmffBoxNode(new IsoBmffBox(-1, FreeHeaderSize, amount, IsoBmffTypes.Free), -1);

    work.Insert(rootIndex + 1, new _WorkBox(freeNode, root.Offset + root.Size, amount));
    return true;
  }

  private int _findFreeAfter(List<_WorkBox> work, int rootIndex) {
    for (var i = rootIndex + 1; i < work.Count; i++) {
      var box = work[i];

      if (_isExpensive(box))
        return -1;

      if (box.Node.Box.Type == IsoBmffTypes.Free)
        return i;
    }

    return -1;
  }

  private int _findFreeBefore(List<_WorkBox> work, int rootIndex) {
    for (var i = rootIndex - 1; i >= 0; i--) {
      var box = work[i];

      if (_isExpensive(box))
        return -1;

      if (box.Node.Box.Type == IsoBmffTypes.Free)
        return i;
    }

    return -1;
  }

  private bool _isExpensive(_WorkBox box) =>
    box.Size >= _expensiveBoxThreshold;

  private IsoBmffLayout _createLayout(IsoBmffLayout original, List<_WorkBox> work, IReadOnlyCollection<IsoBmffBoxNode> editedRoots) {
    var edited = editedRoots.ToHashSet();
    var result = new List<IsoBmffLayoutBox>();

    foreach (var box in work) {
      if (edited.Contains(box.Node))
        _addEditedTree(result, box.Node, box.Offset);
      else if (original.TryGetValue(box.Node, out var originalBox))
        _addExistingTree(result, original, box, originalBox!);
      else
        result.Add(new IsoBmffLayoutBox(box.Node, box.Offset, box.Size));
    }

    return new IsoBmffLayout(result);
  }

  private void _addExistingTree(List<IsoBmffLayoutBox> result, IsoBmffLayout original, _WorkBox workBox, IsoBmffLayoutBox originalBox) {
    var offsetDelta = workBox.Offset - originalBox.Offset;

    result.Add(new IsoBmffLayoutBox(workBox.Node, workBox.Offset, workBox.Size));

    foreach (var child in original.Boxes) {
      if (!_isDescendantOf(child.Node.OriginalNode ?? child.Node, workBox.Node)) // TODO not sure about this change
        continue;

      result.Add(new IsoBmffLayoutBox(child.Node, child.Offset + offsetDelta, child.Size));
    }
  }

  private bool _isDescendantOf(IsoBmffBoxNode node, IsoBmffBoxNode ancestor) {
    var index = _boxes.IndexOf(node);

    if (index < 0)
      return false;

    while (_boxes[index].Parent >= 0) {
      index = _boxes[index].Parent;

      if (ReferenceEquals(_boxes[index], ancestor))
        return true;
    }

    return false;
  }

  private void _addEditedTree(List<IsoBmffLayoutBox> result, IsoBmffBoxNode node, long offset) {
    result.Add(new IsoBmffLayoutBox(node, offset, node.Box.Size));

    var childOffset = offset + node.Box.HeaderSize + _getBoxChildrenOffset(node.Box);

    foreach (var child in _getChildren(node)) {
      _addEditedTree(result, child, childOffset);

      childOffset += child.Box.Size;
    }
  }

  private List<IsoBmffBoxNode> _getChildren(IsoBmffBoxNode parent) {
    var parentIndex = _boxes.IndexOf(parent);

    return _boxes
      .Where(x => x.Parent == parentIndex)
      .ToList();
  }

  private IsoBmffBoxNode _getTopLevelNode(IsoBmffBoxNode node) {
    var index = _boxes.IndexOf(node);

    if (index < 0)
      throw new InvalidOperationException("Box does not belong to this planner.");

    while (_boxes[index].Parent >= 0)
      index = _boxes[index].Parent;

    return _boxes[index];
  }

  private IsoBmffLayout _createFullRewriteLayout(IsoBmffLayout original, IReadOnlyCollection<IsoBmffBoxNode> editedRoots) {
    var work = new List<_WorkBox>();
    var offset = 0L;

    foreach (var node in _boxes.Where(x => x.Parent < 0).OrderBy(x => x.Box.Offset)) {
      var size = editedRoots.Contains(node) || !original.TryGetValue(node, out var old)
        ? node.Box.Size
        : old!.Size;

      work.Add(new _WorkBox(node, offset, size));
      offset += size;
    }

    return _createLayout(original, work, editedRoots);
  }

  private sealed class _WorkBox(IsoBmffBoxNode node, long offset, long size) {
    public IsoBmffBoxNode Node { get; } = node;
    public long Offset { get; set; } = offset;
    public long Size { get; set; } = size;
  }
}