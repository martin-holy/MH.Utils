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

      if (i == layout.Items.Count - 1) continue;
      
      // TODO 2 byte zero gap
      var next = layout.Items[i + 1];
      var expected = item.OriginalOffset + item.CurrentSize;

      if (next.OriginalOffset > expected) {
        var hole = checked((int)(next.OriginalOffset - expected));
        writer.WriteZeros(hole);
      }
    }

    writer.FlushDeferred();

    return ms.ToArray();
  }
}