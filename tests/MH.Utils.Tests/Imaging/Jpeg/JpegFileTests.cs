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

  private static ReadOnlySpan<byte> _exifHeader => "Exif\0\0"u8;
  private static ReadOnlySpan<byte> _xmpHeader => "http://ns.adobe.com/xap/1.0/\0"u8;

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

    _exifHeader.CopyTo(payload);
    tiff.CopyTo(payload, 6);

    return _createApp1(payload);
  }

  private static byte[] _createApp1Xmp(string xml) {
    var xmlBytes = Encoding.UTF8.GetBytes(xml);
    var payload = new byte[29 + xmlBytes.Length];

    _xmpHeader.CopyTo(payload);
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
}