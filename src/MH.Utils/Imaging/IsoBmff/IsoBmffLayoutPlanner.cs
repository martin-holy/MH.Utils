using System;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed record IsoBmffMove(long SourceOffset, long DestinationOffset, long Length);

internal sealed record IsoBmffWrite(long Offset, long Length);

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
    new(_boxes.Select(x => new IsoBmffLayoutBox(x, x.Box.Offset, x.Box.Size)));

  public IsoBmffLayout CreateEditedLayout(IsoBmffLayout original, IsoBmffBoxNode editedBox) {
    var root = _getTopLevelNode(editedBox);

    if (!original.TryGetValue(root, out var originalRoot))
      throw new InvalidOperationException("Edited box does not belong to the original layout.");

    var rootIndex = _getTopLevelIndex(root);

    var work = original.TopLevelBoxes
      .Select(x => new _WorkBox(x.Node, x.Offset, x.Size))
      .ToList();

    var workRoot = work.Single(x => ReferenceEquals(x.Node, root));

    var delta = root.Box.Size - originalRoot!.Size;

    if (delta > 0)
      _grow(work, rootIndex, delta);
    else if (delta < 0)
      _shrink(work, rootIndex, -delta);

    return _createLayout(original, work, [root]);
  }

  public IsoBmffLayout CreateEditedLayout(IsoBmffLayout original, IEnumerable<IsoBmffBoxNode> editedBoxes) {
    var editedRoots = editedBoxes
      .Select(_getTopLevelNode)
      .Distinct()
      .ToList();

    if (editedRoots.Count == 0)
      return original;

    var work = original.TopLevelBoxes
      .Select(x => new _WorkBox(x.Node, x.Offset, x.Size))
      .ToList();

    // Always process edits in their original physical order.
    // This makes the result deterministic and prevents a later edit
    // from unexpectedly consuming space intended by an earlier one.
    var orderedRoots = editedRoots
      .OrderBy(x => original[x].Offset)
      .ToList();

    foreach (var root in orderedRoots)
      _applyEdit(work, original, root);

    return _createLayout(original, work, editedRoots);
  }

  private void _applyEdit(List<_WorkBox> work, IsoBmffLayout original, IsoBmffBoxNode root) {
    if (!original.TryGetValue(root, out var originalRoot))
      throw new InvalidOperationException(
        "Edited box does not belong to the original layout.");

    var index = work.FindIndex(x => ReferenceEquals(x.Node, root));

    if (index < 0)
      throw new InvalidOperationException(
        "Edited box is not present in the working layout.");

    var delta = root.Box.Size - originalRoot!.Size;

    if (delta > 0)
      _grow(work, index, delta);
    else if (delta < 0)
      _shrink(work, index, -delta);

    if (work[index].Size != root.Box.Size)
      throw new InvalidOperationException(
        "Edited box size does not match the planned layout.");
  }

  private void _grow(List<_WorkBox> work, int rootIndex, long amount) {
    if (amount <= 0) return;

    var remaining = amount;

    // Prefer free space after the edited box. This keeps the edited
    // box at the same offset whenever possible.
    remaining = _consumeFreeAfter(work, rootIndex, remaining);

    if (remaining == 0) return;

    // Then use free space before the edited box. This moves the edited
    // box to the left but keeps everything after it anchored.
    remaining = _consumeFreeBefore(work, rootIndex, remaining);

    if (remaining != 0)
      throw new InvalidOperationException(
        "The edited layout cannot be created without moving an expensive box or rewriting the file.");
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

  private long _consumeFreeBefore(List<_WorkBox> work, int rootIndex, long amount) {
    while (amount > 0) {
      var freeIndex = _findFreeBefore(work, rootIndex);

      if (freeIndex < 0) return amount;

      var free = work[freeIndex];
      var count = Math.Min(amount, free.Size);

      free.Size -= count;

      // Everything between the free box and edited box moves left.
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

  private void _shrink(List<_WorkBox> work, int rootIndex, long amount) {
    if (amount <= 0) return;

    var freeIndex = _findFreeAfter(work, rootIndex);

    if (freeIndex >= 0) {
      var free = work[freeIndex];

      work[rootIndex].Size -= amount;

      // Compact everything between the edited box and the existing
      // free box to the left.
      for (var i = rootIndex + 1; i < freeIndex; i++)
        work[i].Offset -= amount;

      free.Offset -= amount;
      free.Size += amount;

      return;
    }

    // No existing free space is available before the next expensive
    // anchor. The released bytes can become a new free box.
    if (amount < FreeHeaderSize)
      throw new InvalidOperationException(
        "The edited layout leaves less than a free-box header and requires a rewrite.");

    var root = work[rootIndex];

    root.Size -= amount;

    var freeNode = new IsoBmffBoxNode(new IsoBmffBox(-1, FreeHeaderSize, amount, IsoBmffTypes.Free), -1);

    work.Insert(rootIndex + 1, new _WorkBox(freeNode, root.Offset + root.Size, amount));
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
      if (!_isDescendantOf(child.Node, workBox.Node))
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

  private int _getTopLevelIndex(IsoBmffBoxNode node) {
    var index = _boxes.IndexOf(node);

    if (index < 0)
      throw new InvalidOperationException("Box does not belong to this planner.");

    while (_boxes[index].Parent >= 0)
      index = _boxes[index].Parent;

    var topLevelNode = _boxes[index];

    return _boxes
      .Select((x, i) => (x, i))
      .Where(x => x.x.Parent < 0)
      .OrderBy(x => x.x.Box.Offset)
      .Select(x => x.x)
      .ToList()
      .IndexOf(topLevelNode);
  }

  private sealed class _WorkBox(IsoBmffBoxNode node, long offset, long size) {
    public IsoBmffBoxNode Node { get; } = node;
    public long Offset { get; set; } = offset;
    public long Size { get; set; } = size;
  }
}