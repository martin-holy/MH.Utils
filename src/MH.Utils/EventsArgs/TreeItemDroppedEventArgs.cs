using MH.Utils.Interfaces;
using System;

namespace MH.Utils.EventsArgs;

public class TreeItemDroppedEventArgs(object data, ITreeItem dest, bool aboveDest, bool copy) : EventArgs {
  public object Data { get; } = data;
  public ITreeItem Dest { get; } = dest;
  public bool AboveDest { get; } = aboveDest;
  public bool Copy { get; } = copy;
}