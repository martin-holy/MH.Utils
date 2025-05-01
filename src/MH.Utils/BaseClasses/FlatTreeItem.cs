using MH.Utils.Interfaces;
using System;

namespace MH.Utils.BaseClasses;

public class FlatTreeItem(ITreeItem treeItem, int level) : IEquatable<FlatTreeItem> {
  public ITreeItem TreeItem { get; } = treeItem;
  public int Level { get; } = level;

  public bool Equals(FlatTreeItem? other) => other is not null && ReferenceEquals(TreeItem, other.TreeItem);
  public override bool Equals(object? obj) => obj?.GetType() == GetType() && ReferenceEquals(TreeItem, ((FlatTreeItem)obj).TreeItem);
  public override int GetHashCode() => TreeItem.GetHashCode();
}