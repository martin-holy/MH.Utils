using System.Collections.Generic;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffLayout {
  public List<ITiffWritable> Items { get; } = [];
  public List<TiffLayoutHole> Holes { get; } = [];

  public TiffLayoutHole? FindHoleAfter(ITiffWritable item) {
    uint end = item.OriginalOffset + (uint)item.OriginalSize;

    foreach (var hole in Holes)
      if (hole.OriginalOffset == end)
        return hole;

    return null;
  }
}