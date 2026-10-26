using System.Collections.Generic;
using System.IO;

namespace MH.Utils.IO;

public interface IBinaryWritePlan {
  bool RequiresFullRewrite { get; }
  internal IReadOnlyList<BinaryMove> Moves { get; }
  internal IReadOnlyList<BinaryWrite> Writes { get; }
  internal void WriteFullRewrite(Stream source, Stream destination);
}