using MH.Utils.Imaging;
using MH.Utils.Imaging.Exif;
using MH.Utils.Imaging.Jpeg;
using MH.Utils.Imaging.Xmp;
using System.Diagnostics;
using System.Xml.Linq;

namespace MH.Utils.Tests.Imaging;

[TestClass]
public class ImageMetadataTests {
  [TestMethod]
  public void RealFile_RemoveMetadata() {
    JpegFile.RemoveMetadata(@"d:\Dev\.NET\MH.Utils\tests\test_image.jpg");
  }

  [TestMethod]
  public void RealFile_Write() {
    var filePath = @"d:\Dev\.NET\MH.Utils\tests\test_image.jpg";

    var metadata = new ImageMetadata(filePath) {
      Orientation = ExifOrientation.Normal,
      Rating = 5,
      Comment = "Ocellated lizard",
      Keywords = ["Animal/Lizard", "Outdoor"]
    };

    var people = metadata.Jpeg.Xmp.EnsurePeople();
    if (!people.Any(x => x.PersonDisplayName == "Alejandro")) {
      var person = people.Add("Alejandro", "0.66875, 0.341667, 0.17125, 0.228333");
      person.Element.SetXmpArray(XmpNs.MpReg + "RectangleKeywords", ["Head"]);
    }

    XNamespace customNs = "customNamespace";
    XNamespace customNs2 = "customNamespace2";
    metadata.Jpeg.Xmp.EnsureDoc().SetArray(customNs + "myCustomArrayProperty", ["value1", "value2"]);
    metadata.Jpeg.Xmp.EnsureDoc().SetProperty(customNs2 + "myCustomProperty", "custom value", XmpValueStyle.Element);

    // TODO custom exif property
    
    metadata.Write(filePath);
  }

  [TestMethod]
  public void RealFile_Read() {
    var filePath = @"d:\Dev\.NET\MH.Utils\tests\test_image.jpg";

    var metadata = new ImageMetadata(filePath, JpegMetadataLoad.All);
    var gps = metadata.GpsCoordinate;

    Debug.WriteLine($"Orientation: {metadata.Orientation}");
    Debug.WriteLine($"Comment: {metadata.Comment}");
    Debug.WriteLine($"Lat: {gps?.Latitude} Lng:{gps?.Longitude}");
    Debug.WriteLine($"Rating: {metadata.Rating}");
    Debug.WriteLine($"Width: {metadata.Width}");
    Debug.WriteLine($"Height: {metadata.Height}");
    Debug.WriteLine($"Keywords: {string.Join(", ", metadata.Keywords ?? [])}");

    if (metadata.People is { } people)
      foreach (var person in people) {
        var keywords = person.Element
          .GetXmpArray(XmpNs.MpReg + "RectangleKeywords")?
          .Select(e => e.Value.Trim())
          .Where(v => v.Length > 0)
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .ToArray();

        Debug.WriteLine($"Person: {person.PersonDisplayName}" +
          $", rect: {person.Rectangle}" +
          $", keywords: {string.Join(", ", keywords ?? [])}");
      }

    if (metadata.Jpeg.Xmp.Doc is not { } doc) return;
    XNamespace customNs = "customNamespace";
    XNamespace customNs2 = "customNamespace2";
    Debug.WriteLine($"Custom XMP array: {string.Join(", ", doc.GetArray(customNs + "myCustomArrayProperty") ?? [])}");
    Debug.WriteLine($"Custom XMP property: {doc.GetProperty(customNs2 + "myCustomProperty")}");
  }
}