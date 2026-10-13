using System.Collections.Generic;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffLayoutDiff {
  public List<IsoBmffMove> Moves { get; } = [];
  public List<IsoBmffWrite> Writes { get; } = [];
}