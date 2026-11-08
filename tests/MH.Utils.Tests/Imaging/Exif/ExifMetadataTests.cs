using MH.Utils.Imaging.Exif;
using MH.Utils.Imaging.Tiff;

namespace MH.Utils.Tests.Imaging.Exif;

[TestClass]
public class ExifMetadataTests {
  [TestMethod]
  public void ToTiff_AddsPaddingAfterSizeChangeAndPreservesItForLaterEdits() {
    var original = _createTiff();
    var metadata = new ExifMetadata(new TiffReader(original));
    metadata.SetComment("test");

    var padded = metadata.ToTiff();

    Assert.IsTrue(padded.Length > original.Length);

    var reloaded = new ExifMetadata(new TiffReader(padded));
    Assert.IsTrue(reloaded.TiffFile.ExifIfd?.FindEntry(ExifTag.Padding)?.Value is PaddingValue { Data.Length: > 4 });
    Assert.AreEqual("test", reloaded.GetComment());

    reloaded.SetRating(4);
    var updated = reloaded.ToTiff();

    Assert.AreEqual(padded.Length, updated.Length);
  }

  private static byte[] _createTiff() {
    var file = TiffFile.CreateEmpty();
    file.Ifd0.SetEntry(ExifTag.Orientation, TiffType.Short, [1, 0]);

    return TiffSerializer.Serialize(file, true);
  }
}
