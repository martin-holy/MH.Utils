using System;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffLayoutPlanner {
  private const long DefaultExpensiveBoxThreshold = 1024 * 1024;
  private const long FreeHeaderSize = 8;

  private readonly List<IsoBmffNode> _originalNodes;
  private readonly List<IsoBmffNode> _nodes;
  private readonly Func<IsoBmffBox, long> _getBoxChildrenOffset;
  private readonly long _expensiveBoxThreshold;

  public IsoBmffLayoutPlanner(
    IEnumerable<IsoBmffNode> nodes,
    Func<IsoBmffBox, long> getBoxChildrenOffset,
    long expensiveBoxThreshold = DefaultExpensiveBoxThreshold) {

    _nodes = [.. nodes];
    _originalNodes = _nodes;
    _getBoxChildrenOffset = getBoxChildrenOffset;
    _expensiveBoxThreshold = expensiveBoxThreshold;
  }

  public IsoBmffLayoutPlanner(
    IEnumerable<IsoBmffNode> originalNodes,
    IEnumerable<IsoBmffNode> editedNodes,
    Func<IsoBmffBox, long> getBoxChildrenOffset,
    long expensiveBoxThreshold = DefaultExpensiveBoxThreshold) {

    _originalNodes = [.. originalNodes];
    _nodes = [.. editedNodes];
    _getBoxChildrenOffset = getBoxChildrenOffset;
    _expensiveBoxThreshold = expensiveBoxThreshold;
  }

  public IsoBmffLayout CreateOriginalLayout() =>
    new(_originalNodes
      .Where(x => x.Box.Offset >= 0)
      .Select(x => new IsoBmffLayoutBox(x, x.Box.Offset, x.Box.Size)));

  public IsoBmffLayoutPlan CreateEditedLayout(IsoBmffLayout original, IsoBmffNode editedNode) =>
    CreateEditedLayout(original, [editedNode]);

  public IsoBmffLayoutPlan CreateEditedLayout(IsoBmffLayout original, IEnumerable<IsoBmffNode> editedNodes) {
    var editedLayouts = _getEditedNodeLayouts();

    if (_nodes.Any(x => editedLayouts[x].HeaderSize != x.Box.HeaderSize))
      return new IsoBmffLayoutPlan(_createFullRewriteLayout(editedLayouts), true);

    var editedRoots = editedNodes
      .Select(_getTopLevelNode)
      .DistinctBy(x => x.Identity)
      .Where(x => !original.TryGetValue(x, out var originalBox) || x.Box.Size != originalBox!.Size)
      .ToList();

    if (editedRoots.Count == 0)
      return new IsoBmffLayoutPlan(original, false);

    var work = original.TopLevelBoxes
      .Select(x => new _workNode(x.Node, x.Offset, x.Size))
      .ToList();

    foreach (var root in editedRoots.OrderBy(x => _getOriginalOffset(original, x))) {
      var succeeded = original.TryGetValue(root, out _)
        ? _applyEdit(work, original, root)
        : _insertNewRoot(work, original, root);

      if (!succeeded)
        return new IsoBmffLayoutPlan(_createFullRewriteLayout(editedLayouts), true);
    }

    return new IsoBmffLayoutPlan(_createLayout(original, work, editedRoots, editedLayouts), false);
  }

  private Dictionary<IsoBmffNode, _nodeLayoutInfo> _getEditedNodeLayouts() {
    var result = new Dictionary<IsoBmffNode, _nodeLayoutInfo>();

    foreach (var root in _nodes.Where(x => x.Parent < 0))
      _getEditedNodesLayout(root, result);

    return result;
  }

  private _nodeLayoutInfo _getEditedNodesLayout(IsoBmffNode node, Dictionary<IsoBmffNode, _nodeLayoutInfo> layouts) {
    var size = node.Box.Size;

    foreach (var child in _getChildren(node)) {
      var childLayout = _getEditedNodesLayout(child, layouts);
      size = checked(size + childLayout.Size - child.Box.Size);
    }

    var headerSize = node.Box.HeaderSize;

    if (headerSize == 8 && size > uint.MaxValue) {
      headerSize = 16;
      size = checked(size + 8);
    }

    if (headerSize is not (8 or 16) || size < headerSize)
      throw new InvalidOperationException($"Invalid header or size for {node.Box.TypeName}.");

    var result = new _nodeLayoutInfo(size, headerSize);
    layouts.Add(node, result);

    return result;
  }

  private long _getOriginalOffset(IsoBmffLayout original, IsoBmffNode root) {
    if (original.TryGetValue(root, out var originalBox))
      return originalBox!.Offset;

    var index = _nodes.FindIndexByIdentity(root, throwIfNotFound: true);

    for (var i = index + 1; i < _nodes.Count; i++) {
      if (_nodes[i].Parent < 0 && original.TryGetValue(_nodes[i], out var nextBox))
        return nextBox!.Offset;
    }

    return original.TopLevelBoxes.LastOrDefault()?.End ?? 0;
  }

  private bool _insertNewRoot(List<_workNode> work, IsoBmffLayout original, IsoBmffNode root) {
    var editedIndex = _nodes.FindIndexByIdentity(root, throwIfNotFound: true);
    var workIndex = work.Count;

    for (var i = editedIndex + 1; i < _nodes.Count; i++) {
      if (_nodes[i].Parent >= 0 || !original.TryGetValue(_nodes[i], out _))
        continue;

      workIndex = work.FindIndex(x => x.Node.HasSameIdentityAs(_nodes[i]));

      if (workIndex < 0)
        throw new InvalidOperationException("The next original top-level node is missing.");

      break;
    }

    var offset = workIndex < work.Count
      ? work[workIndex].Offset
      : work.Count == 0
        ? 0
        : work[^1].Offset + work[^1].Size;

    work.Insert(workIndex, new _workNode(root, offset, 0));

    if (!_grow(work, ref workIndex, root.Box.Size))
      return false;

    if (!_normalizeFreeBoxes(work, ref workIndex))
      return false;

    workIndex = work.FindIndex(x => x.Node.HasSameIdentityAs(root));

    return workIndex >= 0 && work[workIndex].Size == root.Box.Size;
  }

  private bool _applyEdit(List<_workNode> work, IsoBmffLayout original, IsoBmffNode root) {
    if (!original.TryGetValue(root, out var originalRoot))
      throw new InvalidOperationException("Edited node does not belong to the original layout.");

    var index = work.FindIndex(x => x.Node.HasSameIdentityAs(root));
    if (index < 0)
      throw new InvalidOperationException("Edited node is not present in the working layout.");

    var delta = root.Box.Size - originalRoot!.Size;

    if (delta > 0) {
      if (!_grow(work, ref index, delta)) return false;
    }
    else if (delta < 0 && !_shrink(work, index, -delta)) {
      return false;
    }

    if (!_normalizeFreeBoxes(work, ref index)) return false;

    if (work[index].Size != root.Box.Size)
      throw new InvalidOperationException("Edited node size does not match the planned layout.");

    return true;
  }

  private bool _grow(List<_workNode> work, ref int rootIndex, long amount) {
    if (amount <= 0) return true;

    var remaining = _consumeFreeAfter(work, rootIndex, amount);
    if (remaining == 0) return true;

    remaining = _consumeFreeBefore(work, ref rootIndex, remaining);
    return remaining == 0;
  }

  private long _consumeFreeAfter(List<_workNode> work, int rootIndex, long amount) {
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

  private long _consumeFreeBefore(List<_workNode> work, ref int rootIndex, long amount) {
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

  private bool _shrink(List<_workNode> work, int rootIndex, long amount) {
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

    var freeNode = new IsoBmffNode(new IsoBmffBox(-1, FreeHeaderSize, amount, IsoBmffTypes.Free), -1);

    work.Insert(rootIndex + 1, new _workNode(freeNode, root.Offset + root.Size, amount));
    return true;
  }

  private int _findFreeAfter(List<_workNode> work, int rootIndex) {
    for (var i = rootIndex + 1; i < work.Count; i++) {
      var workNode = work[i];
      if (_isExpensive(workNode)) return -1;
      if (workNode.Node.Box.IsFree) return i;
    }

    return -1;
  }

  private int _findFreeBefore(List<_workNode> work, int rootIndex) {
    for (var i = rootIndex - 1; i >= 0; i--) {
      var workNode = work[i];
      if (_isExpensive(workNode)) return -1;
      if (workNode.Node.Box.IsFree) return i;
    }

    return -1;
  }

  private bool _isExpensive(_workNode workNode) =>
    !workNode.Node.Box.IsFree && workNode.Size >= _expensiveBoxThreshold;

  private bool _normalizeFreeBoxes(List<_workNode> work, ref int rootIndex) {
    for (var i = 0; i < work.Count;) {
      var free = work[i];

      if (!free.Node.Box.IsFree) {
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

  private int _findFreeAfterFragment(List<_workNode> work, int fragmentIndex) {
    for (var i = fragmentIndex + 1; i < work.Count; i++) {
      if (work[i].Node.Box.IsFree) return i;
      if (_isExpensive(work[i])) return -1;
    }

    return -1;
  }

  private int _findFreeBeforeFragment(List<_workNode> work, int fragmentIndex) {
    for (var i = fragmentIndex - 1; i >= 0; i--) {
      if (work[i].Node.Box.IsFree) return i;
      if (_isExpensive(work[i])) return -1;
    }

    return -1;
  }

  private IsoBmffLayout _createLayout(
    IsoBmffLayout original,
    List<_workNode> work,
    IReadOnlyCollection<IsoBmffNode> editedRoots,
    Dictionary<IsoBmffNode, _nodeLayoutInfo> layouts) {

    var edited = editedRoots.ToDictionary(x => x.Identity);
    var result = new List<IsoBmffLayoutBox>();

    foreach (var workNode in work) {
      if (edited.TryGetValue(workNode.Node.Identity, out var editedRoot))
        _addEditedTree(result, editedRoot, workNode.Offset, layouts);
      else if (original.TryGetValue(workNode.Node, out var originalBox))
        _addExistingTree(result, original, workNode, originalBox!);
      else
        result.Add(new(workNode.Node, workNode.Offset, workNode.Size));
    }

    return new IsoBmffLayout(result);
  }

  private void _addExistingTree(List<IsoBmffLayoutBox> result, IsoBmffLayout original, _workNode workNode, IsoBmffLayoutBox originalBox) {
    var offsetDelta = workNode.Offset - originalBox.Offset;

    result.Add(new(workNode.Node, workNode.Offset, workNode.Size));

    foreach (var child in original.Boxes) {
      if (!_isDescendantOf(child.Node, workNode.Node))
        continue;

      result.Add(new(child.Node, child.Offset + offsetDelta, child.Size));
    }
  }

  private bool _isDescendantOf(IsoBmffNode node, IsoBmffNode ancestor) {
    var index = _originalNodes.FindIndexByIdentity(node);

    if (index < 0) return false;

    while (_originalNodes[index].Parent >= 0) {
      index = _originalNodes[index].Parent;

      if (_originalNodes[index].HasSameIdentityAs(ancestor))
        return true;
    }

    return false;
  }

  private void _addEditedTree(
    List<IsoBmffLayoutBox> result,
    IsoBmffNode node,
    long offset,
    Dictionary<IsoBmffNode, _nodeLayoutInfo> layouts) {

    var nodeLayout = layouts[node];

    result.Add(new(node, offset, nodeLayout.Size, nodeLayout.HeaderSize));

    var childrenOffset = node.Data?.LongLength ?? _getBoxChildrenOffset(node.Box);
    var childOffset = checked(offset + nodeLayout.HeaderSize + childrenOffset);

    foreach (var child in _getChildren(node)) {
      _addEditedTree(result, child, childOffset, layouts);
      childOffset = checked(childOffset + layouts[child].Size);
    }
  }

  private List<IsoBmffNode> _getChildren(IsoBmffNode parent) {
    var parentIndex = _nodes.IndexOf(parent);

    return [.. _nodes.Where(x => x.Parent == parentIndex)];
  }

  private IsoBmffNode _getTopLevelNode(IsoBmffNode node) {
    var index = _nodes.FindIndexByIdentity(node, throwIfNotFound: true);

    while (_nodes[index].Parent >= 0)
      index = _nodes[index].Parent;

    return _nodes[index];
  }

  private IsoBmffLayout _createFullRewriteLayout(Dictionary<IsoBmffNode, _nodeLayoutInfo> layouts) {
    var result = new List<IsoBmffLayoutBox>();
    var offset = 0L;

    foreach (var root in _nodes.Where(x => x.Parent < 0)) {
      _addEditedTree(result, root, offset, layouts);
      offset = checked(offset + layouts[root].Size);
    }

    return new IsoBmffLayout(result);
  }

  private sealed class _workNode(IsoBmffNode node, long offset, long size) {
    public IsoBmffNode Node { get; } = node;
    public long Offset { get; set; } = offset;
    public long Size { get; set; } = size;
  }

  private readonly record struct _nodeLayoutInfo(long Size, long HeaderSize);
}