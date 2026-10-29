using MH.Utils.Imaging.Exif;
using MH.Utils.Imaging.Tiff;

namespace MH.Utils.Tests.Imaging.Exif;

[TestClass]
public class ExifMetadataTests {
  [TestMethod]
  public void ToTiff_DoesNotAddPaddingWhenSizeIsUnchanged() {
    var reader = new TiffReader(_createTiff());
    var metadata = new ExifMetadata(reader);
    metadata.SetOrientation(2);

    var tiff = metadata.ToTiff(addPadding: true);

    Assert.AreEqual(reader.Buffer.Length, tiff.Length);
  }

  [TestMethod]
  public void ToTiff_AddsPaddingAfterSizeChangeAndPreservesItForLaterEdits() {
    var original = _createTiff();
    var metadata = new ExifMetadata(new TiffReader(original));
    metadata.SetComment("test");

    var padded = metadata.ToTiff(addPadding: true);

    Assert.IsTrue(padded.Length > original.Length);
    Assert.AreEqual(0, padded.Length % 1024);

    var reloaded = new ExifMetadata(new TiffReader(padded));
    Assert.IsTrue(reloaded.TiffFile.Ifd0.FindEntry(ExifTag.Padding)?.Value is PaddingValue { Data.Length: > 4 });
    Assert.AreEqual("test", reloaded.GetComment());

    reloaded.SetRating(4);
    var updated = reloaded.ToTiff(addPadding: true);

    Assert.AreEqual(padded.Length, updated.Length);
  }

  private static byte[] _createTiff() {
    var file = TiffFile.CreateEmpty();
    file.Ifd0.SetEntry(ExifTag.Orientation, TiffType.Short, [1, 0]);

    return TiffSerializer.Serialize(file, true);
  }
}
