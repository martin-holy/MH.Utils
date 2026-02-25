using System.Collections.Specialized;

namespace MH.Utils.BaseClasses;

public interface ILeafyTreeItem {
  INotifyCollectionChanged Leaves { get; }
}

public class LeafyTreeItem<T> : TreeItem, ILeafyTreeItem {
  public ExtObservableCollection<T> Leaves { get; }

  public LeafyTreeItem() {
    Leaves = new(this);
  }

  INotifyCollectionChanged ILeafyTreeItem.Leaves => Leaves;
}