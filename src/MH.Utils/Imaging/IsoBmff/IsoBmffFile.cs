using System;
using System.IO;

namespace MH.Utils.Imaging.IsoBmff;

public sealed class IsoBmffFile {
  public int Width { get; }
  public int Height { get; }
  public int Orientation { get; }
  public double? FrameRate { get; }
  public TimeSpan? Duration { get; }

  public IsoBmffFile(Stream stream) {
    var reader = new IsoBmffReader(stream);
    var moov = reader.FindMoov() ?? throw new InvalidDataException("ISO BMFF moov box not found.");
    var trak = reader.FindVideoTrack(moov) ?? throw new InvalidDataException("Video track not found.");
    var tkhd = reader.FindChild(trak, IsoBmffTypes.Tkhd) ?? throw new InvalidDataException("Video track has no tkhd box.");

    var (width, height) = reader.ReadDimensions(tkhd);
    Width = width;
    Height = height;

    Orientation = reader.ReadOrientation(tkhd);

    if (reader.FindChild(trak, IsoBmffTypes.Mdia) is not { } mdia) return;
    if (reader.FindChild(mdia, IsoBmffTypes.Mdhd) is not { } mdhd) return;
    if (reader.FindChild(mdia, IsoBmffTypes.Minf) is not { } minf) return;
    if (reader.FindChild(minf, IsoBmffTypes.Stbl) is not { } stbl) return;
    if (reader.FindChild(stbl, IsoBmffTypes.Stts) is not { } stts) return;

    var (timescale, duration) = reader.ReadTiming(mdhd);

    Duration = duration;
    FrameRate = reader.ReadFrameRate(stts, timescale);
  }
}