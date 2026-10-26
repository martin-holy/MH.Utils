using MH.Utils.IO;
using System.Collections.Generic;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffLayoutDiff {
  public List<BinaryMove> Moves { get; } = [];
  public List<BinaryWrite> Writes { get; } = [];
}