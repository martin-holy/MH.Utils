using MH.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Tree;

public static class TreeItemExtensions {
  public static IEnumerable<TItem> AsTree<TItem, TGroup, TSort>(this IEnumerable<TItem> items, Func<TGroup, TSort> orderBy)
    where TItem : class, ITreeItem where TGroup : class, ITreeItem {
    var dic = items.ToDictionary(x => (TGroup)x.Data!, x => x);

    foreach (var item in dic.OrderBy(x => orderBy(x.Key))) {
      if (item.Key.Parent is not TGroup parent) {
        yield return item.Value;
        continue;
      }

      item.Value.Parent = dic[parent];
      item.Value.Parent.Items.Add(item.Value);
    }
  }

  public static void DoForAll(this IEnumerable<ITreeItem> items, Action<ITreeItem> action) {
    foreach (var item in items) {
      action(item);
      items.DoForAll(action);
    }
  }

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

  public static IEnumerable<T> Flatten<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>?> elementSelector) where T : ITreeItem {
    var stack = new Stack<IEnumerator<T>>();
    var e = source.GetEnumerator();
    try {
      while (true) {
        while (e.MoveNext()) {
          var item = e.Current;
          if (item == null) continue;
          yield return item;
          var elements = elementSelector(item);
          if (elements == null) continue;
          stack.Push(e);
          e = elements.GetEnumerator();
        }

        if (stack.Count == 0) break;
        e.Dispose();
        e = stack.Pop();
      }
    }
    finally {
      e.Dispose();
      while (stack.Count != 0)
        stack.Pop().Dispose();
    }
  }

  public static IEnumerable<T> Flatten<T>(this IEnumerable<T> items) where T : ITreeItem =>
    items.Flatten(x => x.Items.Cast<T>());

  public static IEnumerable<T> Flatten<T>(this T item) where T : ITreeItem =>
    new[] { item }.Concat(item.Items.Cast<T>().Flatten());

  public static List<T> GetBranch<T>(this T? item) where T : class, ITreeItem {
    var items = new List<T>();

    while (item != null) {
      items.Add(item);
      item = item.Parent as T ?? null;
    }

    items.Reverse();

    return items;
  }

  public static T GetBranchEndOfType<T>(this T item) where T : class, ITreeItem {
    while (item.Items.OfType<T>().Any())
      item = item.Items.OfType<T>().First();

    return item;
  }

  public static T? GetNextBranchEndOfType<T>(this T? current) where T : class, ITreeItem {
    if (current == null) return default;

    if (current.Items.OfType<T>().Any())
      return current.GetBranchEndOfType();

    var parent = current.Parent;
    while (parent != null) {
      var index = parent.Items.IndexOf(current!);
      if (parent.Items.Skip(index + 1).OfType<T>().FirstOrDefault()?.GetBranchEndOfType() is { } next)
        return next;

      current = parent as T;
      parent = current?.Parent;
    }

    return default;
  }

  public static string GetFullName<T>(this T self, string separator, Func<T, string?> nameSelector) where T : class, ITreeItem {
    var list = self.GetThisAndParents().ToList();
    list.Reverse();
    return string.Join(separator, list.Select(nameSelector));
  }

  /// <summary>
  /// Returns index of the item in the expanded tree. Hidden items are not counted.
  /// </summary>
  public static int GetIndex(this ITreeItem item, ITreeItem parent) {
    var index = 0;
    var found = false;
    TreeU.GetIndex(item, parent, ref index, ref found);
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

  public static ITreeItem? GetPreviousSibling(this ITreeItem item) {
    if (item.Parent == null) return null;
    var idx = item.Parent.Items.IndexOf(item);
    return idx < 1 ? null : item.Parent.Items[idx - 1];
  }

  public static ITreeItem? GetRoot(ITreeItem item) {
    var i = item;
    while (i != null) {
      if (i.Parent == null) return i;
      i = i.Parent;
    }
    return default;
  }

  public static IEnumerable<T> GetThisAndItems<T>(this ITreeItem root) where T : class {
    if (root is T rootItem)
      yield return rootItem;

    foreach (var item in root.Items)
      foreach (var subItem in item.GetThisAndItems<T>())
        yield return subItem;
  }

  public static IEnumerable<T> GetThisAndParents<T>(this T? item) where T : class, ITreeItem {
    while (item != null) {
      yield return item;
      item = item.Parent as T;
    }
  }

  public static bool HasThisParent(this ITreeItem self, ITreeItem parent) {
    var p = self.Parent;
    while (p != null) {
      if (ReferenceEquals(p, parent))
        return true;
      p = p.Parent;
    }

    return false;
  }

  public static bool HasVisibleChildren(this ITreeItem self) {
    foreach (var child in self.Items)
      if (!child.IsHidden)
        return true;

    return false;
  }

  public static bool IsFullyExpanded(this ITreeItem self) =>
    self.IsExpanded && (self.Parent == null || self.Parent.IsFullyExpanded());

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

  public static void SetExpanded<T>(this ITreeItem self, bool value) where T : ITreeItem {
    if (self.IsExpanded != value)
      self.IsExpanded = value;
    foreach (var item in self.Items.OfType<T>())
      item.SetExpanded<T>(value);
  }

  public static bool Move(this ITreeItem item, ITreeItem dest, bool aboveDest) {
    var relative = item.GetType() == dest.GetType();
    var oldParent = item.Parent;
    var newParent = relative
      ? dest.Parent
      : dest;

    if (newParent == null || oldParent == null) return false;

    if (!ReferenceEquals(oldParent, newParent)) {
      oldParent.Items.Remove(item);
      if (oldParent.Items.Count == 0)
        oldParent.IsExpanded = false;

      item.Parent = newParent;
    }

    if (relative)
      newParent.Items.SetRelativeTo(item, dest, aboveDest);
    else
      TreeU.SetInOrder(newParent.Items, item, x => x.Name);

    return true;
  }

  public static List<FlatTreeItem> ToFlatTreeItems(this IEnumerable<ITreeItem> roots, int startLevel = 0) {
    var flatItems = new List<FlatTreeItem>();
    var stack = new Stack<(ITreeItem Node, int Level)>();

    foreach (var root in roots.Reverse())
      stack.Push((root, startLevel));

    while (stack.Count > 0) {
      var (node, level) = stack.Pop();
      if (node.IsHidden) continue;
      flatItems.Add(new(node, level));
      if (!node.IsExpanded) continue;
      for (var i = node.Items.Count - 1; i >= 0; i--)
        stack.Push((node.Items[i], level + 1));
    }

    return flatItems;
  }

  public static List<T>? Toggle<T>(this List<T>? list, T item) where T : class, ITreeItem {
    list ??= [];

    if (list.SelectMany(x => x.GetThisAndParents()).Any(x => ReferenceEquals(x, item))) {
      list.Remove(item);
      return list.Count == 0 ? null : list;
    }

    // remove possible redundant items
    // example: if new item is "Weather/Sunny" item "Weather" is redundant
    foreach (var newItem in item.GetThisAndParents())
      list.Remove(newItem);

    list.Add(item);

    return list;
  }

  public static IEnumerable<string> ToStrings<T>(this IEnumerable<T> items, Func<T, string> nameSelector)
    where T : class, ITreeItem =>
    items
      .EmptyIfNull()
      .SelectMany(x => x.GetThisAndParents())
      .Distinct()
      .OrderBy(x => x.GetFullName(".", nameSelector))
      .Select(nameSelector);
}