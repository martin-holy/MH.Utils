using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed record IsoBmffLayoutBox(IsoBmffBoxNode Node, long Offset, long Size) {
  public long End => Offset + Size;
}

internal sealed class IsoBmffLayout {
  private readonly Dictionary<IsoBmffBoxNode, IsoBmffLayoutBox> _boxes;

  public IsoBmffLayout(IEnumerable<IsoBmffLayoutBox> boxes) {
    _boxes = boxes.ToDictionary(x => x.Node);
  }

  public IsoBmffLayoutBox this[IsoBmffBoxNode node] =>
    _boxes[node];

  public IEnumerable<IsoBmffLayoutBox> Boxes =>
    _boxes.Values.OrderBy(x => x.Offset);

  public bool TryGetValue(IsoBmffBoxNode node, out IsoBmffLayoutBox? box) =>
    _boxes.TryGetValue(node, out box);
}