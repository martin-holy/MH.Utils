using System.Collections.Generic;

namespace MH.Utils.Imaging.Tiff;

public static class TiffLayoutBuilder {
  public static TiffLayout Build(TiffFile file, TiffReader? reader) {
    var layout = new TiffLayout();
    var padding = new List<TiffObject>();

    // Match TiffSerializer's output order: all IFD tables, then values.
    _collectIfdTables(layout, file.Ifd0);
    _collectOutOfLineValues(layout, file.Ifd0, padding);

    // TiffLayoutPlanner adjusts padding after planning the other objects.
    layout.Items.AddRange(padding);

    if (reader != null)
      _findHoles(layout, reader);

    return layout;
  }

  private static void _collectIfdTables(TiffLayout layout, TiffIfd ifd) {
    layout.Items.Add(ifd);

    foreach (var entry in ifd.Entries) {
      if (entry.SubIfd != null)
        _collectIfdTables(layout, entry.SubIfd);
    }

    if (ifd.NextIfd != null)
      _collectIfdTables(layout, ifd.NextIfd);
  }

  private static void _collectOutOfLineValues(TiffLayout layout, TiffIfd ifd, List<TiffObject> padding) {
    // Match the serializer's descendant-before-parent value order.
    foreach (var entry in ifd.Entries) {
      if (entry.SubIfd != null)
        _collectOutOfLineValues(layout, entry.SubIfd, padding);
    }

    if (ifd.NextIfd != null)
      _collectOutOfLineValues(layout, ifd.NextIfd, padding);

    foreach (var entry in ifd.Entries) {
      if (entry.SubIfd != null
        || entry.Value is not { } value
        || entry.IsInline)
        continue;

      if (value is PaddingValue)
        padding.Add(value);
      else
        layout.Items.Add(value);
    }
  }

  private static void _findHoles(TiffLayout layout, TiffReader reader) {
    var items = new List<TiffObject>(layout.Items.Count);

    foreach (var item in layout.Items) {
      if (item.OriginalOffset != null) {
        item.HoleAfter = null;
        items.Add(item);
      }
    }

    items.Sort(static (a, b) => a.OriginalOffset!.Value.CompareTo(b.OriginalOffset!.Value));

    for (int i = 0; i < items.Count - 1; i++) {
      var current = items[i];
      var next = items[i + 1];
      uint end = current.OriginalOffset!.Value + (uint)current.OriginalSize;

      if (next.OriginalOffset!.Value <= end) continue;

      int length = checked((int)(next.OriginalOffset!.Value - end));

      current.HoleAfter = new TiffLayoutHole(reader.GetSpan(end, length).ToArray());
    }
  }
}