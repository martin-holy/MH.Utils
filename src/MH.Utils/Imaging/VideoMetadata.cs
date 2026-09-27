using MH.Utils.Imaging.IsoBmff;
using System;
using System.IO;

namespace MH.Utils.Imaging;

public sealed class VideoMetadata {
  private readonly IsoBmffFile _isoBmff;

  public int Width => _isoBmff.Width;
  public int Height => _isoBmff.Height;
  public int Orientation => _isoBmff.Orientation;
  public double? FrameRate => _isoBmff.FrameRate;
  public TimeSpan? Duration => _isoBmff.Duration;
  public string[]? Keywords { get => _getKeywords(); }

  public VideoMetadata(Stream stream) {
    _isoBmff = new IsoBmffFile(stream);
  }

  private string[]? _getKeywords() =>
    _isoBmff.Xmp.GetKeywords() ?? _isoBmff._metadata.GetKeywords();
}