using MH.Utils.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.BaseClasses;

public class TreeItem : ListItem, ITreeItem {
  private ITreeItem? _parent;

  public ITreeItem? Parent { get => _parent; set { _parent = value; OnPropertyChanged(); } }
  public ExtObservableCollection<ITreeItem> Items { get; }
  public bool IsExpanded {
    get => _bits[BitsMasks.IsExpanded];
    set {
      if (_bits[BitsMasks.IsExpanded] == value) return;
      _bits[BitsMasks.IsExpanded] = value;
      _onIsExpandedChanged(value);
      OnPropertyChanged();
    }
  }
    
  public TreeItem() : base(null, string.Empty) { Items = new(this); }

  public TreeItem(string? icon, string name) : base(icon, name) { Items = new(this); }

  public TreeItem(object data) : base(null, string.Empty, data) { Items = new(this); }

  protected virtual void _onIsExpandedChanged(bool value) { }

  public void AddItems(IEnumerable<ITreeItem> items) =>
    Items.AddItems(items.ToList(), item => item.Parent = this);
}