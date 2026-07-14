using System;

namespace MH.Utils.Imaging.Tiff;

public static class TiffLayoutPlanner {
  public static void Plan(TiffLayout layout) {
    var pending = 0;
    var holeIndex = 0;

    foreach (var item in layout.Items) {
      var delta = item.CurrentSize - item.OriginalSize;

      if (delta > 0)
        pending += delta;

      while (holeIndex < layout.Holes.Count) {
        var hole = layout.Holes[holeIndex];

        if (hole.OriginalOffset != item.OriginalOffset + item.OriginalSize)
          break;

        var consume = Math.Min(hole.Size, pending);
        hole.Consume(consume);
        pending -= consume;
        holeIndex++;
      }

      if (pending == 0)
        continue;

      if (item is PaddingValue padding) {
        var consume = Math.Min(padding.Data.Length, pending);
        padding.Consume(consume);
        pending -= consume;
      }
    }
  }
}