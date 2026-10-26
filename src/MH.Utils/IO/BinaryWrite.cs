namespace MH.Utils.IO;

public sealed record BinaryWrite(long Offset, byte[] Data) {
  public bool IsEmpty => Data is null || Data.Length == 0;
}