namespace MH.Utils.Imaging.Tiff;

public static class TiffLayoutPlanner {
  public static void Plan(TiffLayout layout) {
    int pending = 0;

    foreach (var item in layout.Items) {
      pending += item.CurrentSize - item.OriginalSize;

      if (pending <= 0) continue;

      if (item.HoleAfter != null)
        pending -= item.HoleAfter.Consume(pending);

      if (pending <= 0) continue;

      if (item is PaddingValue padding)
        pending -= padding.Consume(pending);
    }
  }
}