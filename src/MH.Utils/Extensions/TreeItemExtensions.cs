using MH.Utils.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Extensions;

public static class TreeItemExtensions {
  public static void ExpandFromRoot(this ITreeItem self) {
    var items = self.GetThisAndParents().ToList();

    // don't expand this if Items are empty or it's just placeholder
    if (self.Items.Count == 0 || self.Items[0].Parent == null)
      items.Remove(self);

    items.Reverse();

    foreach (var item in items)
      item.IsExpanded = true;
  }

  public static void ExpandToRoot(this ITreeItem self) {
    var parent = self.Parent;

    while (parent != null) {
      parent.IsExpanded = true;
      parent = parent.Parent;
    }
  }

  public static List<T> GetBranch<T>(this T? item) where T : class, ITreeItem {
    var items = new List<T>();

    while (item != null) {
      items.Add(item);
      item = item.Parent as T ?? null;
    }

    items.Reverse();

    return items;
  }

  /// <summary>
  /// Returns index of the item in the expanded tree. Hidden items are not counted.
  /// </summary>
  public static int GetIndex(this ITreeItem item, ITreeItem parent) {
    int index = 0;
    bool found = false;
    Tree.GetIndex(item, parent, ref index, ref found);
    return found ? index : -1;
  }

  public static int GetLevel(this ITreeItem item) {
    var level = 0;
    var parent = item.Parent;

    while (parent != null) {
      level++;
      parent = parent.Parent;
    }

    return level;
  }

  public static T? GetParentOf<T>(this ITreeItem? item) where T : ITreeItem {
    var i = item;
    while (i != null) {
      if (i is T t) return t;
      i = i.Parent;
    }
    return default;
  }

  public static bool HasVisibleChildren(this ITreeItem self) {
    foreach (var child in self.Items)
      if (!child.IsHidden)
        return true;

    return false;
  }

  public static bool IsFullyExpanded(this ITreeItem self) =>
    self.IsExpanded && (self.Parent == null || IsFullyExpanded(self.Parent));

  public static bool IsVisible(this ITreeItem self) {
    if (self.IsHidden) return false;

    var parent = self.Parent;

    while (parent != null) {
      if (parent.IsHidden || !parent.IsExpanded)
        return false;

      parent = parent.Parent;
    }

    return true;
  }
}