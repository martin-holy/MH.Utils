using MH.Utils.BaseClasses;
using System.Collections.Specialized;

namespace MH.Utils.Tree;

public interface ILeafyTreeItem {
  INotifyCollectionChanged Leaves { get; }
}

public interface ILeafyTreeItem<T> : ITreeItem {
  public ExtObservableCollection<T> Leaves { get; set; }
}

public class LeafyTreeItem<T> : TreeItem, ILeafyTreeItem {
  public ExtObservableCollection<T> Leaves { get; }

  public LeafyTreeItem() {
    Leaves = new(this);
  }

  INotifyCollectionChanged ILeafyTreeItem.Leaves => Leaves;
}