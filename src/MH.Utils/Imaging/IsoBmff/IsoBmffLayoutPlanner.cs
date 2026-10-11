using System;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed record IsoBmffMove(long SourceOffset, long DestinationOffset, long Length);

internal sealed record IsoBmffWrite(long Offset, byte[] Data);

internal sealed class IsoBmffLayoutPlan(IsoBmffLayout layout, bool requiresFullRewrite) {
  public IsoBmffLayout Layout { get; } = layout;
  public bool RequiresFullRewrite { get; } = requiresFullRewrite;
}

internal sealed class IsoBmffLayoutPlanner {
  private const long DefaultExpensiveBoxThreshold = 1024 * 1024;
  private const long FreeHeaderSize = 8;

  private readonly List<IsoBmffBoxNode> _originalBoxes;
  private readonly List<IsoBmffBoxNode> _boxes;
  private readonly Func<IsoBmffBox, long> _getBoxChildrenOffset;
  private readonly long _expensiveBoxThreshold;

  public IsoBmffLayoutPlanner(
    IEnumerable<IsoBmffBoxNode> boxes,
    Func<IsoBmffBox, long> getBoxChildrenOffset,
    long expensiveBoxThreshold = DefaultExpensiveBoxThreshold) {

    _boxes = [.. boxes];
    _originalBoxes = _boxes;
    _getBoxChildrenOffset = getBoxChildrenOffset;
    _expensiveBoxThreshold = expensiveBoxThreshold;
  }

  public IsoBmffLayoutPlanner(
    IEnumerable<IsoBmffBoxNode> originalBoxes,
    IEnumerable<IsoBmffBoxNode> editedBoxes,
    Func<IsoBmffBox, long> getBoxChildrenOffset,
    long expensiveBoxThreshold = DefaultExpensiveBoxThreshold) {

    _originalBoxes = [.. originalBoxes];
    _boxes = [.. editedBoxes];
    _getBoxChildrenOffset = getBoxChildrenOffset;
    _expensiveBoxThreshold = expensiveBoxThreshold;
  }

  public IsoBmffLayout CreateOriginalLayout() =>
    new(_originalBoxes
      .Where(x => x.Box.Offset >= 0)
      .Select(x => new IsoBmffLayoutBox(x, x.Box.Offset, x.Box.Size)));

  public IsoBmffLayoutPlan CreateEditedLayout(IsoBmffLayout original, IsoBmffBoxNode editedBox) =>
    CreateEditedLayout(original, [editedBox]);

  public IsoBmffLayoutPlan CreateEditedLayout(IsoBmffLayout original, IEnumerable<IsoBmffBoxNode> editedBoxes) {
    var editedRoots = editedBoxes
      .Select(_getTopLevelNode)
      .DistinctBy(x => x.Identity)
      .Where(x => !original.TryGetValue(x, out var originalBox) || x.Box.Size != originalBox!.Size)
      .ToList();

    if (editedRoots.Count == 0)
      return new IsoBmffLayoutPlan(original, false);

    var work = original.TopLevelBoxes
      .Select(x => new _WorkBox(x.Node, x.Offset, x.Size))
      .ToList();

    foreach (var root in editedRoots.OrderBy(x => original[x].Offset)) {
      if (!original.TryGetValue(root, out _) || !_applyEdit(work, original, root))
        return new IsoBmffLayoutPlan(_createFullRewriteLayout(), true);
    }

    return new IsoBmffLayoutPlan(_createLayout(original, work, editedRoots), false);
  }

  private bool _applyEdit(List<_WorkBox> work, IsoBmffLayout original, IsoBmffBoxNode root) {
    if (!original.TryGetValue(root, out var originalRoot))
      throw new InvalidOperationException("Edited box does not belong to the original layout.");

    var index = work.FindIndex(x => ReferenceEquals(x.Node.Identity, root.Identity));
    if (index < 0)
      throw new InvalidOperationException("Edited box is not present in the working layout.");

    var delta = root.Box.Size - originalRoot!.Size;

    if (delta > 0) {
      if (!_grow(work, ref index, delta)) return false;
    }
    else if (delta < 0 && !_shrink(work, index, -delta)) {
      return false;
    }

    if (!_normalizeFreeBoxes(work, ref index)) return false;

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
    box.Node.Box.Type != IsoBmffTypes.Free &&
    box.Size >= _expensiveBoxThreshold;

  private bool _normalizeFreeBoxes(List<_WorkBox> work, ref int rootIndex) {
    for (var i = 0; i < work.Count;) {
      var free = work[i];

      if (free.Node.Box.Type != IsoBmffTypes.Free) {
        i++;
        continue;
      }

      if (free.Size < 0) return false;

      if (free.Size == 0) {
        work.RemoveAt(i);
        if (i < rootIndex) rootIndex--;
        continue;
      }

      if (free.Size >= FreeHeaderSize) {
        i++;
        continue;
      }

      var destination = _findFreeAfterFragment(work, i);

      if (destination >= 0) {
        var size = free.Size;

        for (var j = i + 1; j < destination; j++)
          work[j].Offset -= size;

        work[destination].Offset -= size;
        work[destination].Size += size;

        work.RemoveAt(i);
        if (i < rootIndex) rootIndex--;

        continue;
      }

      destination = _findFreeBeforeFragment(work, i);
      if (destination < 0) return false;

      var fragmentSize = free.Size;

      for (var j = destination + 1; j < i; j++)
        work[j].Offset += fragmentSize;

      work[destination].Size += fragmentSize;

      work.RemoveAt(i);
      if (i < rootIndex) rootIndex--;
    }

    return true;
  }

  private int _findFreeAfterFragment(List<_WorkBox> work, int fragmentIndex) {
    for (var i = fragmentIndex + 1; i < work.Count; i++) {
      if (work[i].Node.Box.Type == IsoBmffTypes.Free) return i;
      if (_isExpensive(work[i])) return -1;
    }

    return -1;
  }

  private int _findFreeBeforeFragment(List<_WorkBox> work, int fragmentIndex) {
    for (var i = fragmentIndex - 1; i >= 0; i--) {
      if (work[i].Node.Box.Type == IsoBmffTypes.Free) return i;
      if (_isExpensive(work[i])) return -1;
    }

    return -1;
  }

  private IsoBmffLayout _createLayout(IsoBmffLayout original, List<_WorkBox> work, IReadOnlyCollection<IsoBmffBoxNode> editedRoots) {
    var edited = editedRoots.ToDictionary(x => x.Identity);
    var result = new List<IsoBmffLayoutBox>();

    foreach (var box in work) {
      if (edited.TryGetValue(box.Node.Identity, out var editedRoot))
        _addEditedTree(result, editedRoot, box.Offset);
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
      if (!_isDescendantOf(child.Node, workBox.Node))
        continue;

      result.Add(new IsoBmffLayoutBox(child.Node, child.Offset + offsetDelta, child.Size));
    }
  }

  private bool _isDescendantOf(IsoBmffBoxNode node, IsoBmffBoxNode ancestor) {
    var index = _originalBoxes.FindIndex(x => ReferenceEquals(x.Identity, node.Identity));

    if (index < 0)
      return false;

    while (_originalBoxes[index].Parent >= 0) {
      index = _originalBoxes[index].Parent;

      if (ReferenceEquals(_originalBoxes[index].Identity, ancestor.Identity))
        return true;
    }

    return false;
  }

  private void _addEditedTree(List<IsoBmffLayoutBox> result, IsoBmffBoxNode node, long offset) {
    result.Add(new IsoBmffLayoutBox(node, offset, node.Box.Size));

    var childrenOffset = node.Data?.LongLength ?? _getBoxChildrenOffset(node.Box);
    var childOffset = offset + node.Box.HeaderSize + childrenOffset;

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
    var index = _boxes.FindIndex(x => ReferenceEquals(x.Identity, node.Identity));

    if (index < 0)
      throw new InvalidOperationException("Box does not belong to this planner.");

    while (_boxes[index].Parent >= 0)
      index = _boxes[index].Parent;

    return _boxes[index];
  }

  private IsoBmffLayout _createFullRewriteLayout() {
    var result = new List<IsoBmffLayoutBox>();
    var offset = 0L;

    foreach (var root in _boxes.Where(x => x.Parent < 0)) {
      _addEditedTree(result, root, offset);
      offset += root.Box.Size;
    }

    return new IsoBmffLayout(result);
  }

  private sealed class _WorkBox(IsoBmffBoxNode node, long offset, long size) {
    public IsoBmffBoxNode Node { get; } = node;
    public long Offset { get; set; } = offset;
    public long Size { get; set; } = size;
  }
}