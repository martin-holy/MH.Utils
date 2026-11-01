using System;

namespace MH.Utils.Imaging.Tiff;

public static class TiffLayoutPlanner {
  private const int _minimumPaddingSize = 5;

  public static void Plan(TiffLayout layout) {
    long position = 8;
    TiffObject? previous = null;

    foreach (var item in layout.Items) {
      long originalOffset = item.OriginalOffset ?? position;

      // Preserve the original offset whenever possible.
      long offset = Math.Max(position, originalOffset);

      // Every IFD and out-of-line value must be word-aligned.
      if ((offset & 1) != 0) offset++;

      // The gap belongs to the preceding object.
      if (previous != null)
        _setHole(previous, checked((int)(offset - position)));

      // Reserved padding can absorb displacement caused by growth.
      if (item is PaddingValue padding && item.OriginalOffset is uint original) {
        long displacement = offset - original;

        if (displacement > 0) {
          int available = Math.Max(0, padding.Data.Length - _minimumPaddingSize);
          int consumed = (int)Math.Min(displacement, available);

          padding.Consume(consumed);
        }
      }

      position = offset + item.CurrentSize;
      previous = item;

      if (position > uint.MaxValue)
        throw new InvalidOperationException("TIFF file exceeds the supported size.");
    }
  }

  private static void _setHole(TiffObject item, int size) {
    if (size == 0) {
      item.HoleAfter = null;
      return;
    }

    if (item.HoleAfter == null) {
      item.HoleAfter = new TiffLayoutHole(new byte[size]);
      return;
    }

    item.HoleAfter.Resize(size);
  }
}