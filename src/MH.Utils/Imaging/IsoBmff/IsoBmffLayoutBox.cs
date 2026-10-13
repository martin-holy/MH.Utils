namespace MH.Utils.Imaging.IsoBmff;

internal sealed record IsoBmffLayoutBox(IsoBmffBoxNode Node, long Offset, long Size, long HeaderSize) {
  public long End => Offset + Size;

  public IsoBmffLayoutBox(IsoBmffBoxNode node, long offset, long size)
    : this(node, offset, size, node.Box.HeaderSize) {
  }
}