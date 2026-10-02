using System.Collections.Generic;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffLayoutDiff {
  public List<IsoBmffMove> Moves { get; } = [];
  public List<IsoBmffWrite> Writes { get; } = [];
}

internal static class IsoBmffLayoutDiffer {
  public static IsoBmffLayoutDiff Diff(IsoBmffLayout original, IsoBmffLayout edited, IsoBmffBoxEdit edit) {
    var result = new IsoBmffLayoutDiff();

    foreach (var box in edited.Boxes) {
      var old = original[box.Node];

      if (old.Offset != box.Offset) {
        var direction = box.Offset > old.Offset ? IsoBmffMoveDirection.Down : IsoBmffMoveDirection.Up;
        result.Moves.Add(new IsoBmffMove(old.Offset, box.Offset, old.Size, direction));

        continue;
      }

      if (box.Node == edit.BoxNode) {
        result.Writes.Add(new IsoBmffWrite(box.Offset, box.Size));

        continue;
      }

      if (old.Size != box.Size)
        result.Writes.Add(new IsoBmffWrite(box.Offset, box.Size));
    }

    return result;
  }
}