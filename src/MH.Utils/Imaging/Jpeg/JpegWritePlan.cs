using MH.Utils.IO;
using System.Collections.Generic;
using System.IO;

namespace MH.Utils.Imaging.Jpeg;

internal sealed class JpegWritePlan(
  JpegMetadataWriter writer,
  bool requiresFullRewrite,
  IReadOnlyList<BinaryWrite>? writes = null) : IBinaryWritePlan {

  public bool RequiresFullRewrite { get; } = requiresFullRewrite;
  public IReadOnlyList<BinaryMove> Moves => [];
  public IReadOnlyList<BinaryWrite> Writes { get; } = writes ?? [];

  public void WriteFullRewrite(Stream source, Stream destination) {
    if (source.CanSeek)
      source.Position = 0;

    writer.Write(source, destination);
  }
}