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

  private static byte[] _createJpeg(params byte[][] segments) {
    using var stream = new MemoryStream();

    stream.WriteByte(0xFF);
    stream.WriteByte(0xD8);

    foreach (var segment in segments)
      stream.Write(segment);

    stream.WriteByte(0xFF);
    stream.WriteByte(0xDA);

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
    var xmlBytes = Encoding.UTF8.GetBytes(xml);
    var payload = new byte[29 + xmlBytes.Length];

    "http://ns.adobe.com/xap/1.0/\0"u8.CopyTo(payload);
    xmlBytes.CopyTo(payload, 29);

    return _createApp1(payload);
  }

  private static byte[] _createExifTiff(ushort orientation) {
    var tiff = new byte[26];

    // TIFF header, little endian.
    tiff[0] = 0x49;
    tiff[1] = 0x49;
    tiff[2] = 0x2A;
    tiff[3] = 0x00;

    // IFD0 offset.
    tiff[4] = 0x08;

    // One entry.
    tiff[8] = 0x01;

    // Orientation tag 0x0112.
    tiff[10] = 0x12;
    tiff[11] = 0x01;

    // SHORT type = 3.
    tiff[12] = 0x03;

    // Count = 1.
    tiff[14] = 0x01;

    // Value.
    tiff[18] = (byte)orientation;
    tiff[19] = (byte)(orientation >> 8);

    // Next IFD = 0.
    // Already zero.

    return tiff;
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
    if (guid.Length != 32)
      throw new ArgumentException("Extended XMP GUID must contain 32 characters.", nameof(guid));

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
}