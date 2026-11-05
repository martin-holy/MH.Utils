using System;

namespace MH.Utils.Imaging.Tiff;

public static class TiffLayoutPlanner {
  public static void Plan(TiffLayout layout, int paddingChunk = 1024) {
    PaddingValue? padding = null;
    int pendingDelta = 0;
    int totalSize = 8;

    foreach (var item in layout.Items) {
      if (item is PaddingValue value) {
        padding = value;
        continue;
      }

      int delta = item.CurrentSize - item.OriginalSize;
      pendingDelta = checked(pendingDelta + delta);
      totalSize = checked(totalSize + item.CurrentSize);

      int originalHoleSize = item.HoleAfter?.Size ?? 0;
      int holeSize = originalHoleSize;

      if (pendingDelta > 0)
        holeSize -= Math.Min(pendingDelta, holeSize);

      if (((totalSize + holeSize) & 1) != 0)
        holeSize++;

      if (item.HoleAfter != null)
        item.HoleAfter.Resize(holeSize);
      else if (holeSize > 0)
        item.HoleAfter = new TiffLayoutHole(new byte[holeSize]);

      pendingDelta = checked(pendingDelta + holeSize - originalHoleSize);
      totalSize = checked(totalSize + holeSize);
    }

    if (padding == null) return;

    PaddingValue.ValidatePaddingChunk(paddingChunk);

    int paddingSize = Math.Clamp(
      checked(padding.CurrentSize - pendingDelta),
      PaddingValue.HeaderLength,
      paddingChunk);

    int difference = paddingSize - padding.CurrentSize;

    if ((paddingSize & 1) != 0)
      difference += paddingSize == paddingChunk ? -1 : 1;

    if (difference > 0)
      padding.Extend(difference);
    else if (difference < 0)
      padding.Consume(-difference);
  }
}