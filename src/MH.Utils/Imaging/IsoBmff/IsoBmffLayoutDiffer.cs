using System.Collections.Generic;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffLayoutDiff {
  public List<IsoBmffMove> Moves { get; } = [];
  public List<IsoBmffWrite> Writes { get; } = [];
}

internal static class IsoBmffLayoutDiffer {
  public static IsoBmffLayoutDiff Diff(IsoBmffLayout original, IsoBmffLayout edited) {
    var result = new IsoBmffLayoutDiff();

    foreach (var box in edited.TopLevelBoxes) {
      if (!original.TryGetValue(box.Node, out var old))
        continue;

      if (old!.Offset != box.Offset && box.Node.Box.Type != IsoBmffTypes.Free)
        result.Moves.Add(new IsoBmffMove(old.Offset, box.Offset, old.Size));
    }

    foreach (var box in edited.Boxes) {
      if (!original.TryGetValue(box.Node, out var old)) {
        result.Writes.Add(new IsoBmffWrite(box.Offset, box.Size));
        continue;
      }

      if (old!.Size != box.Size || old.Offset != box.Offset && box.Node.Box.Type == IsoBmffTypes.Free)
        result.Writes.Add(new IsoBmffWrite(box.Offset, box.Size));
    }

    return result;
  }
}