namespace MH.Utils.IO;

public sealed record BinaryMove(long SourceOffset, long DestinationOffset, long Length) {
  public bool IsEmpty => Length <= 0 || SourceOffset == DestinationOffset;
}