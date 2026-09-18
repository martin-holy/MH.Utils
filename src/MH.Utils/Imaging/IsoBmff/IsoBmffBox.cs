namespace MH.Utils.Imaging.IsoBmff;

internal readonly struct IsoBmffBox(long offset, long headerSize, long size, uint type) {
  public long Offset { get; } = offset;
  public long HeaderSize { get; } = headerSize;
  public long Size { get; } = size;
  public uint Type { get; } = type;

  public long DataOffset => Offset + HeaderSize;
  public long DataSize => Size - HeaderSize;
}