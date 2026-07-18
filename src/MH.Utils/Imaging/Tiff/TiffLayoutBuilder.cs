using System.Collections.Generic;

namespace MH.Utils.Imaging.Tiff;

public static class TiffLayoutBuilder {
  public static TiffLayout Build(TiffFile file, TiffReader? reader) {
    var layout = new TiffLayout();

    _collectIfd(layout, file.Ifd0);

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
    var items = new List<TiffObject>(layout.Items.Count);

    foreach (var item in layout.Items)
      if (item.OriginalOffset != null)
        items.Add((TiffObject)item);

    items.Sort(static (a, b) =>
      a.OriginalOffset!.Value.CompareTo(b.OriginalOffset!.Value));

    for (int i = 0; i < items.Count - 1; i++) {
      var current = items[i];
      var next = items[i + 1];
      uint end = current.OriginalOffset!.Value + (uint)current.OriginalSize;

      if (next.OriginalOffset!.Value <= end)
        continue;

      int length = checked((int)(next.OriginalOffset!.Value - end));

      layout.Holes.Add(new TiffLayoutHole(end, reader.GetSpan(end, length).ToArray()));
    }
  }
}