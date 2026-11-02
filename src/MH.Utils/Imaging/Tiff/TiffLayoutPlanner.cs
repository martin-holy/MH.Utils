namespace MH.Utils.Imaging.Tiff;

public static class TiffLayoutPlanner {
  public static void Plan(TiffLayout layout, int paddingChunk = 0) {
    PaddingValue? padding = null;
    int pendingGrowth = 0;
    int pendingShrink = 0;
    int totalSize = 8;

    foreach (var item in layout.Items) {
      if (item is PaddingValue value) {
        padding = value;
        continue;
      }

      int delta = item.CurrentSize - item.OriginalSize;

      if (delta > 0)
        pendingGrowth += delta;
      else if (delta < 0)
        pendingShrink -= delta;

      if (pendingGrowth > 0 && item.HoleAfter != null)
        pendingGrowth -= item.HoleAfter.Consume(pendingGrowth);

      totalSize = checked(totalSize + item.CurrentSize);

      if (item.HoleAfter != null)
        totalSize = checked(totalSize + item.HoleAfter.Size);
    }

    if (padding == null) return;

    int paddingSize = padding.CurrentSize + pendingShrink - pendingGrowth;

    if (paddingChunk > 0) {
      totalSize = checked(totalSize + paddingSize);

      paddingSize = paddingChunk - totalSize % paddingChunk;

      if (paddingSize <= 4)
        paddingSize += paddingChunk;
    }

    int difference = paddingSize - padding.CurrentSize;

    if (difference > 0)
      padding.Extend(difference);
    else if (difference < 0)
      padding.Consume(-difference);
  }
}