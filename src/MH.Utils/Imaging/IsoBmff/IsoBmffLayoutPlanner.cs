using System;
using System.Collections.Generic;
using System.IO;

namespace MH.Utils.Imaging.IsoBmff;

internal enum IsoBmffMoveDirection { Up, Down }

// TODO make it struct if possible
internal sealed record IsoBmffMove(long SourceOffset, long DestinationOffset, long Length, IsoBmffMoveDirection Direction);

// TODO make it struct if possible
internal sealed record IsoBmffWrite(long Offset, byte[] Data);

internal sealed class IsoBmffBoxEdit(IsoBmffBoxNode box, long newSize) {
  public IsoBmffBoxNode Box { get; } = box;
  public long NewSize { get; } = newSize;
  public long SizeDelta => NewSize - Box.Box.Size;
}

internal sealed class IsoBmffLayoutPlan {
  public List<IsoBmffMove> Moves { get; } = [];
  public List<IsoBmffWrite> Writes { get; } = [];
  public bool RequiresRewrite { get; internal set; }
}

internal sealed class IsoBmffLayoutPlanner(List<IsoBmffBoxNode> boxes) {
  private readonly List<IsoBmffBoxNode> _boxes = boxes;

  public IsoBmffLayoutPlan Plan(IsoBmffBoxEdit edit) {
    var plan = new IsoBmffLayoutPlan();
    var deltas = _getSizeDeltas(edit);
    var root = _getAffectedRoot(edit.Box, deltas);
    var delta = deltas[root];

    if (delta == 0) return plan;

    var previous = _getPreviousSibling(root);
    var next = _getNextSibling(root);

    if (delta > 0)
      _planGrowth(plan, root, delta, previous, next);
    else
      _planShrink(plan, root, -delta, next);

    return plan;
  }

  private Dictionary<IsoBmffBoxNode, long> _getSizeDeltas(IsoBmffBoxEdit edit) {
    var deltas = new Dictionary<IsoBmffBoxNode, long>();
    var node = edit.Box;

    while (node.Parent >= 0) {
      deltas[node] = edit.NewSize - edit.Box.Box.Size;
      node = _boxes[node.Parent];
    }

    deltas[node] = edit.NewSize - edit.Box.Box.Size;

    return deltas;
  }

  // TODO deltas are not used
  private IsoBmffBoxNode _getAffectedRoot(IsoBmffBoxNode edit, Dictionary<IsoBmffBoxNode, long> deltas) {
    var node = edit;

    while (node.Parent >= 0)
      node = _boxes[node.Parent];

    return node;
  }

  private void _planGrowth(IsoBmffLayoutPlan plan, IsoBmffBoxNode root, long growth, IsoBmffBoxNode? previous, IsoBmffBoxNode? next) {
    var afterFree = _isFree(next) ? next : null;
    var beforeFree = _isFree(previous) ? previous : null;

    var after = Math.Min(growth, afterFree?.Box.Size ?? 0);
    var remaining = growth - after;

    var before = Math.Min(remaining, beforeFree?.Box.Size ?? 0);
    remaining -= before;

    if (remaining != 0) {
      plan.RequiresRewrite = true;
      return;
    }

    // First consume the free space after the root.
    if (afterFree is not null && after != 0)
      _shrinkFreeAfter(plan, afterFree.Value, after);

    // Then consume free space before the root.
    if (beforeFree is not null && before != 0)
      _shrinkFreeBefore(plan, beforeFree.Value, before);

    // Everything between the old root and the new root position has to be moved.
    if (before != 0)
      _moveRegionUp(plan, beforeFree!.Value.Box.End, root.Box.End, before);

    // The actual changed root will subsequently be written at:
    var newOffset = root.Box.Offset - before;

    plan.Writes.Add(new IsoBmffWrite(
      newOffset,
      new byte[checked((int)(root.Box.Size + growth))]));
  }

  private void _planShrink(IsoBmffLayoutPlan plan, IsoBmffBoxNode root, long shrink, IsoBmffBoxNode? next) {
    // For now create/extend free space after the root.
    //
    // This is deliberately only planning. The writer will generate
    // the actual free box bytes.

    if (next is not null && _isFree(next)) {
      plan.Writes.Add(new IsoBmffWrite(
        root.Box.End - shrink,
        new byte[checked((int)(next.Value.Box.Size + shrink))]));

      return;
    }

    plan.Writes.Add(new IsoBmffWrite(
      root.Box.End - shrink,
      new byte[checked((int)shrink)]));
  }

  private void _shrinkFreeAfter(IsoBmffLayoutPlan plan, IsoBmffBoxNode free, long amount) {
    var newSize = free.Box.Size - amount;

    if (newSize == 0) return;

    plan.Writes.Add(new IsoBmffWrite(
      free.Box.Offset + amount,
      new byte[checked((int)newSize)]));
  }

  private void _shrinkFreeBefore(IsoBmffLayoutPlan plan, IsoBmffBoxNode free, long amount) {
    var newSize = free.Box.Size - amount;

    if (newSize == 0) return;

    plan.Writes.Add(new IsoBmffWrite(
      free.Box.Offset,
      new byte[checked((int)newSize)]));
  }

  private void _moveRegionUp(IsoBmffLayoutPlan plan, long sourceStart, long sourceEnd, long amount) {
    var length = sourceEnd - sourceStart;

    if (length <= 0) return;

    plan.Moves.Add(new IsoBmffMove(
      sourceStart,
      sourceStart - amount,
      length,
      IsoBmffMoveDirection.Up));
  }

  private IsoBmffBoxNode? _getPreviousSibling(IsoBmffBoxNode node) {
    var index = _boxes.IndexOf(node);

    for (var i = index - 1; i >= 0; i--) {
      if (_boxes[i].Parent == node.Parent)
        return _boxes[i];

      if (_boxes[i].Parent < node.Parent)
        break;
    }

    return null;
  }

  private IsoBmffBoxNode? _getNextSibling(IsoBmffBoxNode node) {
    var index = _boxes.IndexOf(node);

    for (var i = index + 1; i < _boxes.Count; i++) {
      if (_boxes[i].Parent == node.Parent)
        return _boxes[i];

      if (_boxes[i].Parent < node.Parent)
        break;
    }

    return null;
  }

  private static bool _isFree(IsoBmffBoxNode? node) =>
    node?.Box.Type == IsoBmffTypes.Free;

  private static void _move(Stream stream, long source, long destination, long length) {
    if (length <= 0 || source == destination)
      return;

    const int bufferSize = 64 * 1024;
    var buffer = new byte[Math.Min(bufferSize, checked((int)length))];

    if (destination < source) {
      // Moving up: source -> destination.
      // Start at the beginning.
      var position = 0L;

      while (position < length) {
        var count = (int)Math.Min(buffer.Length, length - position);

        stream.Position = source + position;
        stream.ReadExactly(buffer, 0, count);

        stream.Position = destination + position;
        stream.Write(buffer, 0, count);

        position += count;
      }
    }
    else {
      // Moving down: source -> destination.
      // Start at the end.
      var position = length;

      while (position > 0) {
        var count = (int)Math.Min(buffer.Length, position);

        position -= count;

        stream.Position = source + position;
        stream.ReadExactly(buffer, 0, count);

        stream.Position = destination + position;
        stream.Write(buffer, 0, count);
      }
    }
  }
}