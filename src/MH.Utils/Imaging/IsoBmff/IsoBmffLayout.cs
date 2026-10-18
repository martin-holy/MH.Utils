using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffLayout {
  private readonly Dictionary<IsoBmffNode, IsoBmffLayoutBox> _boxes;

  public IsoBmffLayout(IEnumerable<IsoBmffLayoutBox> boxes) {
    _boxes = boxes.ToDictionary(x => x.Node.Identity);
  }

  public IsoBmffLayoutBox this[IsoBmffNode node] =>
    _boxes[node.Identity];

  public IEnumerable<IsoBmffLayoutBox> Boxes =>
    _boxes.Values.OrderBy(x => x.Offset);

  public bool TryGetValue(IsoBmffNode node, out IsoBmffLayoutBox? box) =>
    _boxes.TryGetValue(node.Identity, out box);

  public IEnumerable<IsoBmffLayoutBox> TopLevelBoxes =>
    _boxes.Values
      .Where(x => x.Node.Parent < 0)
      .OrderBy(x => x.Offset);
}