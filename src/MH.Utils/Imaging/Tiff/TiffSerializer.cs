using System.IO;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffSerializer {
  public static byte[] Serialize(TiffLayout layout, bool littleEndian) {
    using var ms = new MemoryStream();

    var writer = new TiffWriter(ms, littleEndian);
    writer.WriteHeader();

    for (var i = 0; i < layout.Items.Count; i++) {
      var item = layout.Items[i];

      item.Write(writer);

      if (layout.FindHoleAfter(item) is { } hole)
        writer.WriteZeros(hole.Size);
    }

    writer.FlushDeferred();

    return ms.ToArray();
  }
}