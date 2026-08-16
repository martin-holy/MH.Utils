using MH.Utils.Imaging.Jpeg;
using System.Text;

namespace MH.Utils.Tests.Imaging.Jpeg;

[TestClass]
public class JpegFileTests {
  [TestMethod]
  public void JpegFile_MissingExif_CreatesExifMetadata() {
    using var stream = new MemoryStream(_createJpeg(_createSof(640, 480)));
    using var jpeg = new JpegFile(stream);
    Assert.IsNotNull(jpeg.Exif);
  }

  [TestMethod]
  public void JpegFile_MissingXmp_CreatesXmpMetadata() {
    using var stream = new MemoryStream(_createJpeg(_createSof(640, 480)));
    using var jpeg = new JpegFile(stream);
    Assert.IsNotNull(jpeg.Xmp);
  }

  [TestMethod]
  public void JpegFile_MissingExif_ReturnsSameInstance() {
    using var stream = new MemoryStream(_createJpeg(_createSof(640, 480)));
    using var jpeg = new JpegFile(stream);
    Assert.AreSame(jpeg.Exif, jpeg.Exif);
  }

  [TestMethod]
  public void JpegFile_MissingXmp_ReturnsSameInstance() {
    using var stream = new MemoryStream(_createJpeg(_createSof(640, 480)));
    using var jpeg = new JpegFile(stream);
    Assert.AreSame(jpeg.Xmp, jpeg.Xmp);
  }

  [TestMethod]
  public void JpegFile_Exif_ReturnsSameInstance() {
    using var stream = new MemoryStream(_createJpeg(_createApp1Exif(6), _createSof(640, 480)));
    using var jpeg = new JpegFile(stream);
    var exif = jpeg.Exif;
    Assert.AreSame(exif, jpeg.Exif);
  }

  [TestMethod]
  public void JpegFile_Xmp_ReturnsSameInstance() {
    using var stream = new MemoryStream(
      _createJpeg(
        _createApp1Xmp(
          """
          <x:xmpmeta xmlns:x="adobe:ns:meta/">
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                     xmlns:dc="http://purl.org/dc/elements/1.1/">
              <rdf:Description>
                <dc:description>
                  <rdf:Alt>
                    <rdf:li xml:lang="x-default">Test</rdf:li>
                  </rdf:Alt>
                </dc:description>
              </rdf:Description>
            </rdf:RDF>
          </x:xmpmeta>
          """),
        _createSof(640, 480)));

    using var jpeg = new JpegFile(stream);
    var xmp = jpeg.Xmp;

    Assert.AreSame(xmp, jpeg.Xmp);
  }

  [TestMethod]
  public void JpegFile_ReadingExif_DoesNotNeedXmp() {
    using var stream = new MemoryStream(
      _createJpeg(
        _createApp1Exif(6),
        _createApp1Xmp(
          """
          <x:xmpmeta xmlns:x="adobe:ns:meta/">
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
            </rdf:RDF>
          </x:xmpmeta>
          """),
        _createSof(640, 480)));

    using var jpeg = new JpegFile(stream);

    Assert.AreEqual((ushort)6, jpeg.Exif.GetOrientation());
  }

  [TestMethod]
  public void JpegFile_ReadingXmp_DoesNotNeedExif() {
    using var stream = new MemoryStream(
      _createJpeg(
        _createApp1Xmp(
          """
          <x:xmpmeta xmlns:x="adobe:ns:meta/">
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                     xmlns:dc="http://purl.org/dc/elements/1.1/">
              <rdf:Description>
                <dc:description>
                  <rdf:Alt>
                    <rdf:li xml:lang="x-default">Test</rdf:li>
                  </rdf:Alt>
                </dc:description>
              </rdf:Description>
            </rdf:RDF>
          </x:xmpmeta>
          """),
        _createSof(640, 480)));

    using var jpeg = new JpegFile(stream);

    Assert.AreEqual("Test", jpeg.Xmp.GetComment());
  }

  [TestMethod]
  public void JpegFile_CanReadXmpAfterExif() {
    using var stream = new MemoryStream(
      _createJpeg(
        _createApp1Exif(6),
        _createApp(0xE2, 100),
        _createApp1Xmp(
          """
          <x:xmpmeta xmlns:x="adobe:ns:meta/">
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                     xmlns:dc="http://purl.org/dc/elements/1.1/">
              <rdf:Description>
                <dc:description>
                  <rdf:Alt>
                    <rdf:li xml:lang="x-default">Test</rdf:li>
                  </rdf:Alt>
                </dc:description>
              </rdf:Description>
            </rdf:RDF>
          </x:xmpmeta>
          """),
        _createSof(640, 480)));

    using var jpeg = new JpegFile(stream);

    Assert.AreEqual((ushort)6, jpeg.Exif.GetOrientation());
    Assert.AreEqual("Test", jpeg.Xmp.GetComment());
  }

  [TestMethod]
  public void JpegFile_CanReadExifAfterXmp() {
    using var stream = new MemoryStream(
      _createJpeg(
        _createApp1Xmp(
          """
          <x:xmpmeta xmlns:x="adobe:ns:meta/">
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                     xmlns:dc="http://purl.org/dc/elements/1.1/">
              <rdf:Description>
                <dc:description>
                  <rdf:Alt>
                    <rdf:li xml:lang="x-default">Test</rdf:li>
                  </rdf:Alt>
                </dc:description>
              </rdf:Description>
            </rdf:RDF>
          </x:xmpmeta>
          """),
        _createApp(0xE2, 100),
        _createApp1Exif(6),
        _createSof(640, 480)));

    using var jpeg = new JpegFile(stream);

    Assert.AreEqual("Test", jpeg.Xmp.GetComment());
    Assert.AreEqual((ushort)6, jpeg.Exif.GetOrientation());
  }

  [TestMethod]
  public void JpegFile_FindsXmpRegardlessOfSegmentOrder() {
    using var stream = new MemoryStream(
      _createJpeg(
        _createApp(0xE0, 100),
        _createApp1Exif(6),
        _createApp(0xE2, 500),
        _createApp1Xmp(
          """
          <x:xmpmeta xmlns:x="adobe:ns:meta/">
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                     xmlns:dc="http://purl.org/dc/elements/1.1/">
              <rdf:Description>
                <dc:description>
                  <rdf:Alt>
                    <rdf:li xml:lang="x-default">XMP comment</rdf:li>
                  </rdf:Alt>
                </dc:description>
              </rdf:Description>
            </rdf:RDF>
          </x:xmpmeta>
          """),
        _createSof(640, 480)));

    using var jpeg = new JpegFile(stream);

    Assert.AreEqual("XMP comment", jpeg.Xmp.GetComment());
  }

  [TestMethod]
  public void JpegFile_ReadsSizeWithoutExif() {
    using var stream = new MemoryStream(_createJpeg(_createSof(1234, 567)));
    using var jpeg = new JpegFile(stream);
    Assert.AreEqual(1234, jpeg.Width);
    Assert.AreEqual(567, jpeg.Height);
  }

  [TestMethod]
  public void JpegFile_ReadsSizeIndependentlyOfMetadata() {
    using var stream = new MemoryStream(
      _createJpeg(
        _createApp1Xmp("<x:xmpmeta />"),
        _createApp1Exif(6),
        _createApp(0xE2, 1000),
        _createSof(1234, 567)));

    using var jpeg = new JpegFile(stream);

    Assert.AreEqual(1234, jpeg.Width);
    Assert.AreEqual(567, jpeg.Height);
  }

  [TestMethod]
  public void JpegFile_IgnoresUnrelatedApp1Segments() {
    using var stream = new MemoryStream(
      _createJpeg(
        _createApp1([1, 2, 3, 4, 5]),
        _createApp1Exif(6),
        _createSof(640, 480)));

    using var jpeg = new JpegFile(stream);

    Assert.AreEqual((ushort)6, jpeg.Exif.GetOrientation());
  }

  [TestMethod]
  public void JpegFile_UsesFirstExifSegment() {
    using var stream = new MemoryStream(
      _createJpeg(
        _createApp1Exif(6),
        _createApp1Exif(3),
        _createSof(640, 480)));

    using var jpeg = new JpegFile(stream);

    Assert.AreEqual((ushort)6, jpeg.Exif.GetOrientation());
  }

  [TestMethod]
  public void JpegFile_ReadsExtendedXmp() {
    const string guid = "0123456789ABCDEF0123456789ABCDEF";
    const string comment = "Extended XMP description";

    var xml = Encoding.UTF8.GetBytes(_createExtendedXmpXml(comment));
    var mainXmp = _createApp1Xmp(_createMainXmpWithExtendedGuid(guid));
    var extendedXmp = _createApp1ExtendedXmp(guid, xml.Length, 0, xml);

    using var stream = new MemoryStream(
      _createJpeg(mainXmp, _createApp(0xE2, 100), extendedXmp, _createSof(640, 480)));

    using var jpeg = new JpegFile(stream);

    Assert.AreEqual(comment, jpeg.Xmp.GetComment());
  }

  [TestMethod]
  public void JpegFile_ReconstructsExtendedXmpFromMultipleChunks() {
    const string guid = "0123456789ABCDEF0123456789ABCDEF";
    const string comment = "This is a longer extended XMP description.";

    var xml = Encoding.UTF8.GetBytes(_createExtendedXmpXml(comment));
    var split = xml.Length / 2;
    var first = xml[..split];
    var second = xml[split..];

    using var stream = new MemoryStream(
      _createJpeg(
        _createApp1Xmp(_createMainXmpWithExtendedGuid(guid)),
        _createApp1ExtendedXmp(guid, xml.Length, 0, first),
        _createApp1ExtendedXmp(guid, xml.Length, split, second),
        _createSof(640, 480)));

    using var jpeg = new JpegFile(stream);

    Assert.AreEqual(comment, jpeg.Xmp.GetComment());
  }

  [TestMethod]
  public void JpegFile_ReconstructsExtendedXmp_WhenChunksAreOutOfOrder() {
    const string guid = "0123456789ABCDEF0123456789ABCDEF";
    const string comment = "Out of order extended XMP";

    var xml = Encoding.UTF8.GetBytes(_createExtendedXmpXml(comment));
    var split = xml.Length / 2;
    var first = xml[..split];
    var second = xml[split..];

    using var stream = new MemoryStream(
      _createJpeg(
        _createApp1Xmp(_createMainXmpWithExtendedGuid(guid)),
        _createApp1ExtendedXmp(guid, xml.Length, split, second),
        _createApp1ExtendedXmp(guid, xml.Length, 0, first),
        _createSof(640, 480)));

    using var jpeg = new JpegFile(stream);

    Assert.AreEqual(comment, jpeg.Xmp.GetComment());
  }

  [TestMethod]
  public void JpegFile_IgnoresExtendedXmpWithDifferentGuid() {
    const string guid = "0123456789ABCDEF0123456789ABCDEF";
    const string otherGuid = "FEDCBA9876543210FEDCBA9876543210";

    var extendedXml = Encoding.UTF8.GetBytes(_createExtendedXmpXml("Wrong XMP"));

    using var stream = new MemoryStream(
      _createJpeg(
        _createApp1Xmp(_createMainXmpWithExtendedGuid(guid)),
        _createApp1ExtendedXmp(otherGuid, extendedXml.Length, 0, extendedXml),
        _createSof(640, 480)));

    using var jpeg = new JpegFile(stream);

    // There is no usable extended XMP for our GUID.
    // The main packet itself only contains HasExtendedXMP.
    Assert.IsNull(jpeg.Xmp.GetComment());
  }

  [TestMethod]
  public void JpegFile_MissingExtendedXmp_FallsBackToMainXmp() {
    const string guid = "0123456789ABCDEF0123456789ABCDEF";

    using var stream = new MemoryStream(
      _createJpeg(
        _createApp1Xmp(_createMainXmpWithExtendedGuid(guid)),
        _createSof(640, 480)));

    using var jpeg = new JpegFile(stream);

    Assert.IsNull(jpeg.Xmp.GetComment());
  }

  [TestMethod]
  public void JpegFile_InvalidSignature_Throws() {
    using var stream = new MemoryStream([0x00, 0x00]);
    Assert.ThrowsException<InvalidDataException>(() => new JpegFile(stream));
  }

  [TestMethod]
  public void JpegFile_InvalidSegmentLength_Throws() {
    using var stream = new MemoryStream([
      0xFF, 0xD8,       // SOI
      0xFF, 0xE0,       // APP0
      0x00, 0x01,       // invalid length
      0xFF, 0xDA        // SOS
      ]);

    using var jpeg = new JpegFile(stream);

    Assert.ThrowsException<InvalidDataException>(() => jpeg.Exif);
  }

  [TestMethod]
  public void JpegFile_TruncatedSegment_Throws() {
    using var stream = new MemoryStream([
      0xFF, 0xD8,       // SOI
      0xFF, 0xE0,       // APP0
      0x00, 0x20,       // says 32 bytes
      0x01, 0x02, 0x03  // but only 3 bytes follow
      ]);

    using var jpeg = new JpegFile(stream);

    Assert.ThrowsException<InvalidDataException>(() => jpeg.Exif);
  }

  [TestMethod]
  public void JpegFile_InvalidSof_Throws() {
    var sof = _createSegment(
      0xC0,
      [
        8,              // precision
        0, 10,          // height
        0,              // truncated width
      ]);

    using var stream = new MemoryStream(_createJpeg(sof));
    using var jpeg = new JpegFile(stream);

    Assert.ThrowsException<InvalidDataException>(() => jpeg.Width);
  }

  [TestMethod]
  public void JpegFile_ReadingExif_StopsAfterExif() {
    var exif = _createApp1Exif(6);
    var xmp = _createApp1Xmp(
      """
      <x:xmpmeta xmlns:x="adobe:ns:meta/">
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
        </rdf:RDF>
      </x:xmpmeta>
      """);

    var jpegData = _createJpeg(
      _createApp(0xE0, 100),
      exif,
      _createApp(0xE2, 50000),
      xmp,
      _createSof(640, 480));

    using var stream = new TrackingStream(jpegData);
    using var jpeg = new JpegFile(stream);

    Assert.AreEqual((ushort)6, jpeg.Exif.GetOrientation());

    // The scanner should not have reached the large segment,
    // XMP or SOF.
    long exifEnd =
      2 +                 // SOI
      (4 + 100) +         // APP0
      exif.Length;        // complete EXIF segment

    Assert.AreEqual(exifEnd, stream.MaxPosition);
  }

  [TestMethod]
  public void JpegFile_ReadingXmp_StopsAfterXmp() {
    var exif = _createApp1Exif(6);

    var xmp = _createApp1Xmp(
      """
      <x:xmpmeta xmlns:x="adobe:ns:meta/">
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                 xmlns:dc="http://purl.org/dc/elements/1.1/">
          <rdf:Description>
            <dc:description>
              <rdf:Alt>
                <rdf:li xml:lang="x-default">Test</rdf:li>
              </rdf:Alt>
            </dc:description>
          </rdf:Description>
        </rdf:RDF>
      </x:xmpmeta>
      """);

    var jpegData = _createJpeg(
      _createApp(0xE0, 100),
      exif,
      _createApp(0xE2, 50000),
      xmp,
      _createSof(640, 480));

    using var stream = new TrackingStream(jpegData);
    using var jpeg = new JpegFile(stream);

    Assert.AreEqual("Test", jpeg.Xmp.GetComment());

    long xmpEnd =
      2 +                 // SOI
      (4 + 100) +         // APP0
      exif.Length +       // complete EXIF segment
      (4 + 50000) +       // large APP2
      xmp.Length;         // complete XMP segment

    Assert.AreEqual(xmpEnd, stream.MaxPosition);
  }

  [TestMethod]
  public void JpegMetadataWriter_NoMetadata_PreservesJpeg() {
    var source = _createJpegWithMetadata(
      _createApp1Exif(6),
      _createApp1Xmp(_createExtendedXmpXml("Original")),
      true);

    var result = _writeJpeg(source);

    CollectionAssert.AreEqual(source, result);
  }

  [TestMethod]
  public void JpegMetadataWriter_ReplacesExif() {
    var source = _createJpegWithMetadata(_createApp1Exif(6), null, true);
    var result = _writeJpeg(source, _createExifTiff(3));

    using var stream = new MemoryStream(result);
    using var jpeg = new JpegFile(stream);

    Assert.AreEqual((ushort)3, jpeg.Exif.GetOrientation());
  }

  [TestMethod]
  public void JpegMetadataWriter_ReplacingExif_PreservesOtherSegments() {
    var xmp = _createApp1Xmp(
      """
      <x:xmpmeta xmlns:x="adobe:ns:meta/">
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                 xmlns:dc="http://purl.org/dc/elements/1.1/">
          <rdf:Description>
            <dc:description>
              <rdf:Alt>
                <rdf:li xml:lang="x-default">Original</rdf:li>
              </rdf:Alt>
            </dc:description>
          </rdf:Description>
        </rdf:RDF>
      </x:xmpmeta>
      """);

    var source = _createJpegWithMetadata(_createApp1Exif(6), xmp, true);
    var result = _writeJpeg(source, _createExifTiff(3));

    using var stream = new MemoryStream(result);
    using var jpeg = new JpegFile(stream);

    Assert.AreEqual((ushort)3, jpeg.Exif.GetOrientation());
    Assert.AreEqual("Original", jpeg.Xmp.GetComment());
  }

  [TestMethod]
  public void JpegMetadataWriter_ReplacesXmp() {
    var source = _createJpegWithMetadata(
      null,
      _createApp1Xmp(
        """
      <x:xmpmeta xmlns:x="adobe:ns:meta/">
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                 xmlns:dc="http://purl.org/dc/elements/1.1/">
          <rdf:Description>
            <dc:description>
              <rdf:Alt>
                <rdf:li xml:lang="x-default">Old</rdf:li>
              </rdf:Alt>
            </dc:description>
          </rdf:Description>
        </rdf:RDF>
      </x:xmpmeta>
      """));

    var newXmp = _createApp1Xmp(
      """
      <x:xmpmeta xmlns:x="adobe:ns:meta/">
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                 xmlns:dc="http://purl.org/dc/elements/1.1/">
          <rdf:Description>
            <dc:description>
              <rdf:Alt>
                <rdf:li xml:lang="x-default">New</rdf:li>
              </rdf:Alt>
            </dc:description>
          </rdf:Description>
        </rdf:RDF>
      </x:xmpmeta>
      """);

    var result = _writeJpeg(source, xmp: newXmp);

    using var stream = new MemoryStream(result);
    using var jpeg = new JpegFile(stream);

    Assert.AreEqual("New", jpeg.Xmp.GetComment());
  }

  [TestMethod]
  public void JpegMetadataWriter_AddsMissingExif() {
    var source = _createJpegWithMetadata();
    var result = _writeJpeg(source, _createExifTiff(6));

    using var stream = new MemoryStream(result);
    using var jpeg = new JpegFile(stream);

    Assert.AreEqual((ushort)6, jpeg.Exif.GetOrientation());
  }

  [TestMethod]
  public void JpegMetadataWriter_AddsMissingXmp() {
    var source = _createJpegWithMetadata();

    var xmp = _createApp1Xmp(
      """
      <x:xmpmeta xmlns:x="adobe:ns:meta/">
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                 xmlns:dc="http://purl.org/dc/elements/1.1/">
          <rdf:Description>
            <dc:description>
              <rdf:Alt>
                <rdf:li xml:lang="x-default">Added</rdf:li>
              </rdf:Alt>
            </dc:description>
          </rdf:Description>
        </rdf:RDF>
      </x:xmpmeta>
      """);

    var result = _writeJpeg(source, xmp: xmp);

    using var stream = new MemoryStream(result);
    using var jpeg = new JpegFile(stream);

    Assert.AreEqual("Added", jpeg.Xmp.GetComment());
  }

  [TestMethod]
  public void JpegMetadataWriter_ReplacesExifAndXmp() {
    var source = _createJpegWithMetadata(
      _createApp1Exif(6),
      _createApp1Xmp(
        """
        <x:xmpmeta xmlns:x="adobe:ns:meta/">
          <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                   xmlns:dc="http://purl.org/dc/elements/1.1/">
            <rdf:Description>
              <dc:description>
                <rdf:Alt>
                  <rdf:li xml:lang="x-default">Old</rdf:li>
                </rdf:Alt>
              </dc:description>
            </rdf:Description>
          </rdf:RDF>
        </x:xmpmeta>
        """));

    var newXmp = _createApp1Xmp(
      """
      <x:xmpmeta xmlns:x="adobe:ns:meta/">
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                 xmlns:dc="http://purl.org/dc/elements/1.1/">
          <rdf:Description>
            <dc:description>
              <rdf:Alt>
                <rdf:li xml:lang="x-default">New</rdf:li>
              </rdf:Alt>
            </dc:description>
          </rdf:Description>
        </rdf:RDF>
      </x:xmpmeta>
      """);

    var result = _writeJpeg(source, _createExifTiff(3), newXmp);

    using var stream = new MemoryStream(result);
    using var jpeg = new JpegFile(stream);

    Assert.AreEqual((ushort)3, jpeg.Exif.GetOrientation());
    Assert.AreEqual("New", jpeg.Xmp.GetComment());
  }

  [TestMethod]
  public void JpegMetadataWriter_PreservesSegmentOrder() {
    var exif = _createApp1Exif(6);
    var xmp = _createApp1Xmp("<x:xmpmeta />");
    var app0 = _createApp(0xE0, 10);
    var app2 = _createApp(0xE2, 20);
    var comment = _createComment("test"u8.ToArray());
    var sof = _createSof(640, 480);

    var source = _createJpeg(app0, exif, app2, xmp, comment, sof);
    var result = _writeJpeg(source, _createApp1Exif(3), _createApp1Xmp("<x:xmpmeta />"));
    var segments = _readSegments(result);

    CollectionAssert.AreEqual(
      new[] { (byte)0xE0, (byte)0xE1, (byte)0xE2, (byte)0xE1, (byte)0xFE, (byte)0xC0 },
      segments.Select(x => x.Marker).ToArray());
  }

  [TestMethod]
  public void JpegMetadataWriter_PreservesUnrelatedSegments() {
    var app0 = _createApp(0xE0, 100);
    var app2 = _createApp(0xE2, 200);
    var comment = _createComment("Keep me"u8.ToArray());

    var source = _createJpeg(app0, _createApp1Exif(6), app2, comment, _createSof(640, 480));
    var result = _writeJpeg(source, _createApp1Exif(3));
    var sourceSegments = _readSegments(source);
    var resultSegments = _readSegments(result);

    CollectionAssert.AreEqual(sourceSegments[0].Payload, resultSegments[0].Payload);
    CollectionAssert.AreEqual(sourceSegments[2].Payload, resultSegments[2].Payload);
    CollectionAssert.AreEqual(sourceSegments[3].Payload, resultSegments[3].Payload);
  }

  [TestMethod]
  public void JpegMetadataWriter_PreservesImageData() {
    var source = _createJpeg([_createApp1Exif(6), _createSof(640, 480)]);
    var result = _writeJpeg(source, _createApp1Exif(3));

    CollectionAssert.AreEqual(_getAfterSos(source), _getAfterSos(result));
  }

  [TestMethod]
  public void TestGeneratedJpeg_CanBeRead() {
    var jpegData = _createJpeg(
      _createApp(0xE0, 100),
      _createApp1Exif(6),
      _createApp(0xE2, 200),
      _createSof(640, 480));

    using var stream = new MemoryStream(jpegData);
    var jpeg = new JpegFile(stream);

    Assert.AreEqual(640, jpeg.Width);
    Assert.AreEqual(480, jpeg.Height);
    Assert.AreEqual((ushort)6, jpeg.Exif.GetOrientation());
  }

  private static byte[] _getAfterSos(byte[] jpeg) {
    var index = 0;

    for (int i = 0; i < jpeg.Length - 1; i++) {
      if (jpeg[i] == 0xFF && jpeg[i + 1] == 0xDA) {
        index = i;
        break;
      }
    }

    Assert.AreNotEqual(0, index);

    return jpeg[index..];
  }

  private static byte[] _createJpeg(params byte[][] segments) {
    using var stream = new MemoryStream();

    // SOI
    stream.WriteByte(0xFF);
    stream.WriteByte(0xD8);

    foreach (var segment in segments)
      stream.Write(segment);

    // SOS
    stream.WriteByte(0xFF);
    stream.WriteByte(0xDA);

    // SOS length = 8
    stream.WriteByte(0x00);
    stream.WriteByte(0x08);

    // One component
    stream.WriteByte(0x01);
    stream.WriteByte(0x01);
    stream.WriteByte(0x00);

    // Spectral selection
    stream.WriteByte(0x00);
    stream.WriteByte(0x3F);

    // Successive approximation
    stream.WriteByte(0x00);

    // Fake image data
    stream.Write([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);

    // EOI
    stream.WriteByte(0xFF);
    stream.WriteByte(0xD9);

    return stream.ToArray();
  }

  private static byte[] _createSegment(byte marker, byte[] payload) {
    if (payload.Length > ushort.MaxValue - 2)
      throw new ArgumentOutOfRangeException(nameof(payload));

    ushort length = (ushort)(payload.Length + 2);

    var segment = new byte[payload.Length + 4];

    segment[0] = 0xFF;
    segment[1] = marker;
    segment[2] = (byte)(length >> 8);
    segment[3] = (byte)length;

    Buffer.BlockCopy(payload, 0, segment, 4, payload.Length);

    return segment;
  }

  private static byte[] _createApp1(byte[] payload) =>
    _createSegment(0xE1, payload);

  private static byte[] _createApp(byte marker, int length) =>
    _createSegment(marker, new byte[length]);

  private static byte[] _createApp1Exif(ushort orientation) {
    var tiff = _createExifTiff(orientation);
    var payload = new byte[6 + tiff.Length];

    "Exif\0\0"u8.CopyTo(payload);
    tiff.CopyTo(payload, 6);

    return _createApp1(payload);
  }

  private static byte[] _createApp1Xmp(string xml) {
    var header = "http://ns.adobe.com/xap/1.0/\0"u8;
    var xmlBytes = Encoding.UTF8.GetBytes(xml);
    var payload = new byte[header.Length + xmlBytes.Length];

    header.CopyTo(payload);
    xmlBytes.CopyTo(payload, header.Length);

    return _createApp1(payload);
  }

  private static byte[] _createExifTiff(ushort orientation) {
    return [
      // TIFF header
      (byte)'I', (byte)'I',
      42, 0,

      // IFD0 offset = 8
      8, 0, 0, 0,

      // One IFD entry
      1, 0,

      // Orientation
      0x12, 0x01,

      // SHORT
      3, 0,

      // Count = 1
      1, 0, 0, 0,

      // Value
      (byte)orientation,
      (byte)(orientation >> 8),
      0, 0,

      // Next IFD = none
      0, 0, 0, 0
    ];
  }

  private static byte[] _createSof(ushort width, ushort height, byte marker = 0xC0) {
    var payload = new byte[] {
      8,
      (byte)(height >> 8),
      (byte)height,
      (byte)(width >> 8),
      (byte)width,
      3,

      1, 0x22, 0,
      2, 0x11, 1,
      3, 0x11, 1
    };

    return _createSegment(marker, payload);
  }

  private static byte[] _createApp1ExtendedXmp(string guid, int fullLength, int offset, byte[] data) {
    const int fixedPayloadLength =
      35 + // XMP extension header
      32 + // GUID
      4 +  // full length
      4;   // offset

    if (data.Length > ushort.MaxValue - 2 - fixedPayloadLength)
      throw new ArgumentOutOfRangeException(nameof(data));

    var header = "http://ns.adobe.com/xmp/extension/\0"u8;
    var guidBytes = Encoding.ASCII.GetBytes(guid);

    const int guidLength = 32;
    const int fullLengthSize = 4;
    const int offsetSize = 4;

    int payloadLength =
      header.Length +
      guidLength +
      fullLengthSize +
      offsetSize +
      data.Length;

    var payload = new byte[payloadLength];

    int p = 0;

    header.CopyTo(payload);
    p += header.Length;

    guidBytes.CopyTo(payload, p);
    p += guidLength;

    payload[p++] = (byte)(fullLength >> 24);
    payload[p++] = (byte)(fullLength >> 16);
    payload[p++] = (byte)(fullLength >> 8);
    payload[p++] = (byte)fullLength;

    payload[p++] = (byte)(offset >> 24);
    payload[p++] = (byte)(offset >> 16);
    payload[p++] = (byte)(offset >> 8);
    payload[p++] = (byte)offset;

    data.CopyTo(payload, p);

    return _createApp1(payload);
  }

  private static string _createExtendedXmpXml(string comment) =>
    $"""
      <x:xmpmeta xmlns:x="adobe:ns:meta/">
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                 xmlns:dc="http://purl.org/dc/elements/1.1/">
          <rdf:Description>
            <dc:description>
              <rdf:Alt>
                <rdf:li xml:lang="x-default">{comment}</rdf:li>
              </rdf:Alt>
            </dc:description>
          </rdf:Description>
        </rdf:RDF>
      </x:xmpmeta>
      """;

  private static string _createMainXmpWithExtendedGuid(string guid) =>
    $"""
      <x:xmpmeta xmlns:x="adobe:ns:meta/">
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                 xmlns:xmpNote="http://ns.adobe.com/xmp/note/">
          <rdf:Description xmpNote:HasExtendedXMP="{guid}" />
        </rdf:RDF>
      </x:xmpmeta>
      """;

  private sealed class TrackingStream(byte[] buffer) : MemoryStream(buffer) {
    public long MaxPosition { get; private set; }

    public override long Position {
      get => base.Position;
      set {
        base.Position = value;
        _updateMaxPosition();
      }
    }

    public override long Seek(long offset, SeekOrigin origin) {
      var position = base.Seek(offset, origin);
      _updateMaxPosition();
      return position;
    }

    public override int Read(byte[] buffer, int offset, int count) {
      var result = base.Read(buffer, offset, count);
      _updateMaxPosition();
      return result;
    }

    public override int Read(Span<byte> buffer) {
      var result = base.Read(buffer);
      _updateMaxPosition();
      return result;
    }

    public override int ReadByte() {
      var result = base.ReadByte();
      _updateMaxPosition();
      return result;
    }

    private void _updateMaxPosition() {
      if (Position > MaxPosition)
        MaxPosition = Position;
    }
  }

  private static byte[] _writeJpeg(byte[] source, byte[]? exif = null, byte[]? xmp = null) {
    using var input = new MemoryStream(source);
    using var output = new MemoryStream();

    var writer = new JpegMetadataWriter {
      Exif = exif,
      Xmp = xmp
    };

    writer.Write(input, output);

    return output.ToArray();
  }

  private static byte[] _createComment(byte[] data) =>
    _createSegment(0xFE, data);

  private static byte[] _createJpegWithMetadata(byte[]? exif = null, byte[]? xmp = null, bool includeComment = false) {
    var segments = new List<byte[]> {
      _createApp(0xE0, 100)
    };

    if (includeComment)
      segments.Add(_createComment("JPEG comment"u8.ToArray()));

    if (exif != null)
      segments.Add(exif);

    segments.Add(_createApp(0xE2, 200));

    if (xmp != null)
      segments.Add(xmp);

    segments.Add(_createSof(640, 480));

    return _createJpeg([.. segments]);
  }

  private static List<(byte Marker, byte[] Payload)> _readSegments(byte[] jpeg) {
    using var stream = new MemoryStream(jpeg);
    using var reader = new BinaryReader(stream);

    Assert.AreEqual(0xFF, reader.ReadByte());
    Assert.AreEqual(0xD8, reader.ReadByte());

    var result = new List<(byte, byte[])>();

    while (stream.Position < stream.Length) {
      Assert.AreEqual(0xFF, reader.ReadByte());

      var marker = reader.ReadByte();

      while (marker == 0xFF)
        marker = reader.ReadByte();

      if (marker == 0xDA)
        break;

      var length = (reader.ReadByte() << 8) | reader.ReadByte();

      Assert.IsTrue(length >= 2);

      var payload = reader.ReadBytes(length - 2);

      Assert.AreEqual(length - 2, payload.Length);

      result.Add((marker, payload));
    }

    return result;
  }
}