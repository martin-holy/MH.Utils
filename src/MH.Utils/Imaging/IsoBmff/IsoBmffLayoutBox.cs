namespace MH.Utils.Imaging.IsoBmff;

internal sealed record IsoBmffLayoutBox(IsoBmffNode Node, long Offset, long Size, long HeaderSize) {
  public long End => Offset + Size;

  public IsoBmffLayoutBox(IsoBmffNode node, long offset, long size)
    : this(node, offset, size, node.Box.HeaderSize) {
  }
}