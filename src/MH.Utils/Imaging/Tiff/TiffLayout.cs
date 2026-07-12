using System.Collections.Generic;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffLayout {
  public List<ITiffWritable> Items { get; } = [];
}