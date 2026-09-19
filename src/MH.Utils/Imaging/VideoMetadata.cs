using MH.Utils.Imaging.IsoBmff;
using System;
using System.IO;

namespace MH.Utils.Imaging;

public sealed class VideoMetadata {
  public int Width { get; }
  public int Height { get; }
  public int Orientation { get; }
  public double? FrameRate { get; }
  public TimeSpan? Duration { get; }

  public VideoMetadata(Stream stream) {
    var file = new IsoBmffFile(stream);

    Width = file.Width;
    Height = file.Height;
    Orientation = file.Orientation;
    FrameRate = file.FrameRate;
    Duration = file.Duration;
  }
}