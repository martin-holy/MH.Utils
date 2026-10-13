using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal static class IsoBmffLayoutDiffer {
  public static IsoBmffLayoutDiff Diff(IsoBmffLayout original, IsoBmffLayout edited, IEnumerable<IsoBmffBoxNode>? boxesToWrite = null) {
    var result = new IsoBmffLayoutDiff();

    var movedBoxes = new List<(IsoBmffLayoutBox Edited, IsoBmffLayoutBox Original)>();

    foreach (var box in edited.Boxes) {
      if (original.TryGetValue(box.Node, out var old) && old!.Offset != box.Offset)
        movedBoxes.Add((box, old));
    }

    foreach (var (box, old) in movedBoxes) {
      if (box.Node.Box.Type == IsoBmffTypes.Free)
        continue;

      var offsetDelta = box.Offset - old.Offset;
      var coveredByMovedAncestor = movedBoxes.Any(candidate =>
        !ReferenceEquals(candidate.Edited.Node.Identity, box.Node.Identity) &&
        candidate.Edited.Offset - candidate.Original.Offset == offsetDelta &&
        candidate.Original.Offset <= old.Offset &&
        candidate.Original.End >= old.End &&
        candidate.Edited.Offset <= box.Offset &&
        candidate.Edited.End >= box.End);

      if (!coveredByMovedAncestor)
        result.Moves.Add(new IsoBmffMove(old.Offset, box.Offset, old.Size));
    }

    foreach (var box in edited.Boxes) {
      var isNew = !original.TryGetValue(box.Node, out var old);
      var headerChanged = isNew ||
        old!.Size != box.Size ||
        (box.Node.Box.Type == IsoBmffTypes.Free && old.Offset != box.Offset);

      if (headerChanged)
        result.Writes.Add(new IsoBmffWrite(box.Offset, _createHeader(box)));

      // New nodes are present in the planned layout. Explicit nodes also cover
      // same-size edits when the planner can reuse the original layout.
      if (box.Node.Data is { Length: > 0 } data)
        result.Writes.Add(new IsoBmffWrite(box.Offset + box.HeaderSize, data));
    }

    if (boxesToWrite is not null) {
      foreach (var node in boxesToWrite.DistinctBy(x => x.Identity)) {
        if (node.Data is not { Length: > 0 } data)
          continue;

        if (!edited.TryGetValue(node, out var box))
          continue;

        if (edited.Boxes.Any(x => ReferenceEquals(x.Node, node)))
          continue;

        result.Writes.Add(new IsoBmffWrite(
          box!.Offset + node.Box.HeaderSize,
          data));
      }
    }

    _orderMoves(result.Moves);
    result.Writes.Sort((x, y) => x.Offset.CompareTo(y.Offset));

    return result;
  }

  internal static byte[] _createHeader(IsoBmffLayoutBox box) {
    var size = box.Size;
    var headerSize = box.HeaderSize;

    if (size < headerSize)
      throw new InvalidOperationException("Box size is smaller than its header.");

    if (headerSize is not (8 or 16))
      throw new InvalidOperationException($"Unsupported ISO BMFF header size: {headerSize}");

    var header = new byte[checked((int)headerSize)];

    if (headerSize == 8) {
      if (size > uint.MaxValue)
        throw new InvalidOperationException("Box requires an extended-size header.");

      BinaryPrimitives.WriteUInt32BigEndian(header, checked((uint)size));
    }
    else {
      BinaryPrimitives.WriteUInt32BigEndian(header, 1);
      BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(8), checked((ulong)size));
    }

    BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), box.Node.Box.Type);

    return header;
  }

  private static void _orderMoves(List<IsoBmffMove> moves) {
    // Temporary ordering for monotonic shifts:
    // rightward moves from right to left, then leftward moves from left to right.
    var ordered = moves
      .Where(x => x.DestinationOffset > x.SourceOffset)
      .OrderByDescending(x => x.SourceOffset)
      .Concat(moves
        .Where(x => x.DestinationOffset < x.SourceOffset)
        .OrderBy(x => x.SourceOffset))
      .ToList();

    moves.Clear();
    moves.AddRange(ordered);
  }
}