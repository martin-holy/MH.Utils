namespace MH.Utils.Imaging.Tiff;

public static class TiffLayoutPlanner {
  public static void Plan(TiffLayout layout) {
    int pendingGrowth = 0;
    int pendingShrink = 0;

    foreach (var item in layout.Items) {
      int delta = item.CurrentSize - item.OriginalSize;

      if (delta > 0)
        pendingGrowth += delta;
      else if (delta < 0)
        pendingShrink += -delta;

      if (pendingGrowth > 0 && item.HoleAfter != null)
        pendingGrowth -= item.HoleAfter.Consume(pendingGrowth);

      if (item is PaddingValue padding) {
        if (pendingGrowth > 0)
          pendingGrowth -= padding.Consume(pendingGrowth);

        if (pendingShrink > 0) {
          padding.Extend(pendingShrink);
          pendingShrink = 0;
        }
      }
    }
  }
}