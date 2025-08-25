using System.Collections.Specialized;

namespace MH.Utils.BaseClasses;

public interface ILeafyTreeItem {
  INotifyCollectionChanged Leaves { get; }
}

public class LeafyTreeItem<T> : TreeItem, ILeafyTreeItem {
  public ExtObservableCollection<T> Leaves { get; set; } = [];
  INotifyCollectionChanged ILeafyTreeItem.Leaves => Leaves;
}