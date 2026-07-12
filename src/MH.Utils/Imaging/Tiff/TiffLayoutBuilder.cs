namespace MH.Utils.Imaging.Tiff;

public static class TiffLayoutBuilder {
  public static TiffLayout Build(TiffFile file) {
    var layout = new TiffLayout();

    _collectIfd(layout, file.Ifd0);

    layout.Items.Sort(static (a, b) => a.OriginalOffset.CompareTo(b.OriginalOffset));

    return layout;
  }

  private static void _collectIfd(TiffLayout layout, TiffIfd ifd) {
    layout.Items.Add(ifd);

    foreach (var entry in ifd.Entries) {
      if (entry.SubIfd != null) {
        _collectIfd(layout, entry.SubIfd);
        continue;
      }

      if (entry.Value is InlineValue)
        continue;

      layout.Items.Add(entry.Value!);
    }

    if (ifd.NextIfd != null)
      _collectIfd(layout, ifd.NextIfd);
  }
}