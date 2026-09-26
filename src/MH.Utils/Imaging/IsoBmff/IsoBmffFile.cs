using MH.Utils.Imaging.Xmp;
using System;
using System.IO;

namespace MH.Utils.Imaging.IsoBmff;

public sealed class IsoBmffFile {
  private readonly Stream _stream;
  internal readonly IsoBmffReader _reader;

  private bool _xmpRead;
  private IsoBmffMetadataEntry? _xmpEntry;
  private XmpMetadata? _xmp;
  internal readonly IsoBmffMetadata _metadata;

  public int Width => _metadata.Width;
  public int Height => _metadata.Height;
  public int Orientation => _metadata.Orientation;
  public double? FrameRate => _metadata.FrameRate;
  public TimeSpan? Duration => _metadata.Duration;
  public XmpMetadata Xmp => _getXmp();

  public IsoBmffFile(Stream stream) {
    _stream = stream;
    _reader = new IsoBmffReader(_stream);
    _metadata = new(_stream, _reader);
  }

  internal XmpMetadata _getXmp() {
    if (_xmp != null) return _xmp;
    if (!_xmpRead) {
      _xmpEntry = IsoBmffXmp.Find(_stream, _reader);
      
      if (_xmpEntry != null)
        _xmp = new XmpMetadata(_xmpEntry.Value);

      _xmpRead = true;
    }

    return _xmp ?? new XmpMetadata(null);
  }
}