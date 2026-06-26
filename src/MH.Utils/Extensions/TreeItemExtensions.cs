using MH.Utils.Interfaces;

namespace MH.Utils.Extensions;

public static class TreeItemExtensions {
  public static bool HasVisibleChildren(this ITreeItem self) {
    foreach (var child in self.Items)
      if (!child.IsHidden)
        return true;

    return false;
  }
}