namespace MH.Utils.Imaging.Tiff;

public static class TiffLayoutBuilder {
  public static TiffLayout Build(TiffFile file, TiffReader? reader) {
    var layout = new TiffLayout();

    _collectIfd(layout, file.Ifd0);

    layout.Items.Sort(static (a, b) => {
      if (a.OriginalOffset is uint oa) {
        if (b.OriginalOffset is uint ob)
          return oa.CompareTo(ob);

        return -1;
      }

      return b.OriginalOffset is null ? 0 : 1;
    });

    if (reader != null)
      _findHoles(layout, reader);

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

  private static void _findHoles(TiffLayout layout, TiffReader reader) {
    for (int i = 0; i < layout.Items.Count - 1; i++) {
      var current = layout.Items[i];

      if (current.OriginalOffset == null) continue;

      var next = layout.Items[i + 1];
      uint end = (uint)current.OriginalOffset + (uint)current.OriginalSize;

      if (next.OriginalOffset > end) {
        var length = checked((int)(next.OriginalOffset - end));
        layout.Holes.Add(new TiffLayoutHole(end, reader.GetSpan(end, length).ToArray()));
      }
    }
  }
}