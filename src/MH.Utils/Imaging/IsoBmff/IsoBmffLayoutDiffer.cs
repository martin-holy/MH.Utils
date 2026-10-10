using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffLayoutDiff {
  public List<IsoBmffMove> Moves { get; } = [];
  public List<IsoBmffWrite> Writes { get; } = [];
}

internal static class IsoBmffLayoutDiffer {
  public static IsoBmffLayoutDiff Diff(IsoBmffLayout original, IsoBmffLayout edited, IEnumerable<IsoBmffBoxNode> boxesToWrite) {
    var result = new IsoBmffLayoutDiff();
    var writeSet = boxesToWrite.Select(x => x.Identity).ToHashSet();

    foreach (var box in edited.TopLevelBoxes) {
      if (box.Node.Box.Type == IsoBmffTypes.Free)
        continue;

      if (!original.TryGetValue(box.Node, out var old))
        continue;

      if (old!.Offset != box.Offset)
        result.Moves.Add(new IsoBmffMove(old.Offset, box.Offset, old.Size));
    }

    foreach (var box in edited.TopLevelBoxes) {
      if (box.Node.Box.Type == IsoBmffTypes.Free) {
        if (!original.TryGetValue(box.Node, out var old) ||
            old!.Offset != box.Offset ||
            old.Size != box.Size) {
          result.Writes.Add(new IsoBmffWrite(box.Offset, box.Size));
        }

        continue;
      }

      if (writeSet.Contains(box.Node.Identity))
        result.Writes.Add(new IsoBmffWrite(box.Offset, box.Size));
    }

    _orderMoves(result.Moves);

    return result;
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