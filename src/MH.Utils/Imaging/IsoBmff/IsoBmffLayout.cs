using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed record IsoBmffLayoutBox(IsoBmffBoxNode Node, long Offset, long Size) {
  public long End => Offset + Size;
}

internal sealed class IsoBmffLayout {
  private readonly Dictionary<IsoBmffBoxNode, IsoBmffLayoutBox> _boxes;

  public IsoBmffLayout(IEnumerable<IsoBmffLayoutBox> boxes) {
    _boxes = boxes.ToDictionary(x => x.Node.Identity);
  }

  public IsoBmffLayoutBox this[IsoBmffBoxNode node] =>
    _boxes[node.Identity];

  public IEnumerable<IsoBmffLayoutBox> Boxes =>
    _boxes.Values.OrderBy(x => x.Offset);

  public bool TryGetValue(IsoBmffBoxNode node, out IsoBmffLayoutBox? box) =>
    _boxes.TryGetValue(node.Identity, out box);

  public IEnumerable<IsoBmffLayoutBox> TopLevelBoxes =>
    _boxes.Values
      .Where(x => x.Node.Parent < 0)
      .OrderBy(x => x.Offset);
}