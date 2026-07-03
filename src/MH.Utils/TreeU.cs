using MH.Utils.Extensions;
using MH.Utils.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MH.Utils;

public static class TreeU {
  public static T? FindItem<T>(IEnumerable<T> items, Func<T, bool> equals) where T : class, ITreeItem {
    foreach (var item in items) {
      if (equals(item))
        return item;

      var res = FindItem(item.Items.OfType<T>(), equals);
      if (res != null) return res;
    }

    return default;
  }

  /// <summary>
  /// Returns index of the item in the expanded tree. Hidden items are not counted.
  /// </summary>
  public static void GetIndex(ITreeItem item, ITreeItem parent, ref int index, ref bool found) {
    if (ReferenceEquals(item, parent)) {
      found = true;
      return;
    }

    foreach (var pItem in parent.Items) {
      if (pItem.IsHidden) continue;
      index++;
      if (ReferenceEquals(item, pItem)) {
        found = true;
        break;
      }
      if (!pItem.IsExpanded) continue;
      GetIndex(item, pItem, ref index, ref found);
      if (found) break;
    }
  }

  public static int SetInOrder<T>(ObservableCollection<T> collection, T item, Func<T, string?> keySelector) {
    if (item == null) return -1;

    int newIdx;
    var strB = keySelector(item);
    var itemIsGroup = item is ITreeGroup;

    for (newIdx = 0; newIdx < collection.Count; newIdx++) {
      var compareItem = collection[newIdx];
      var compareItemIsGroup = compareItem is ITreeGroup;

      if (itemIsGroup && !compareItemIsGroup)
        break;

      if (!itemIsGroup && compareItemIsGroup)
        continue;

      var strA = keySelector(compareItem);
      var cRes = string.Compare(strA, strB, StringComparison.CurrentCultureIgnoreCase);
      if (item.Equals(collection[newIdx]) || cRes < 0) continue;

      break;
    }

    var oldIdx = collection.IndexOf(item);
    if (oldIdx < 0)
      collection.Insert(newIdx, item);
    else if (oldIdx != newIdx) {
      if (newIdx > oldIdx) newIdx--;
      collection.Move(oldIdx, newIdx);
    }

    return newIdx;
  }

  /// <summary>
  /// 
  /// </summary>
  /// <param name="root"></param>
  /// <param name="path">full or partial path with no separator on the end</param>
  /// <param name="separator"></param>
  /// <param name="comparison"></param>
  /// <returns></returns>
  public static ITreeItem? GetByPath(ITreeItem root, string path, char separator, StringComparison comparison = StringComparison.CurrentCultureIgnoreCase) {
    if (string.IsNullOrEmpty(path)) return null;

    var rootFullPath = root.GetFullName(separator.ToString(), x => x.Name);
    if (rootFullPath.Equals(path, comparison)) return root;

    var parts = (path.StartsWith(rootFullPath, comparison)
        ? path[(rootFullPath.Length + 1)..]
        : path)
      .Split(separator);

    foreach (var part in parts) {
      var item = root.Items.SingleOrDefault(x => part.Equals(x.Name, comparison));
      if (item == null) return null;
      root = item;
    }

    return root;
  }

  public static T? FindChild<T>(IEnumerable<ITreeItem> items, Func<T, bool> equals) {
    foreach (var item in items) {
      if (equals((T)item))
        return (T)item;

      var res = FindChild(item.Items, equals);
      if (res != null) return res;
    }

    return default;
  }
}