using MH.Utils.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace MH.Utils.BaseClasses;

public class ListItem(string? icon, string name) : ObservableObject, IListItem {
  protected BitVector32 _bits = new(0);

  private string? _icon = icon;
  private string _name = name;

  public bool IsSelected { get => _bits[BitsMasks.IsSelected]; set { _bits[BitsMasks.IsSelected] = value; OnPropertyChanged(); } }
  public bool IsHidden { get => _bits[BitsMasks.IsHidden]; set { _bits[BitsMasks.IsHidden] = value; OnPropertyChanged(); } }
  public bool IsIconHidden { get => _bits[BitsMasks.IsIconHidden]; set { _bits[BitsMasks.IsIconHidden] = value; OnPropertyChanged(); } }
  public bool IsNameHidden { get => _bits[BitsMasks.IsNameHidden]; set { _bits[BitsMasks.IsNameHidden] = value; OnPropertyChanged(); } }
  public string? Icon { get => _icon; set { _icon = value; OnPropertyChanged(); } }
  public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
  public object? Data { get; set; }

  public ListItem(string? icon, string name, object data) : this(icon, name) {
    Data = data;
  }
}

public class ListItem<T>(T content) : ListItem(null, string.Empty) {
  private T _content = content;

  public T Content {
    get => _content;
    set {
      if (EqualityComparer<T>.Default.Equals(_content, value)) return;
      _content = value;
      OnPropertyChanged();
    }
  }
}

public static class ListItemExtensions {
  public static T? GetByName<T>(this IEnumerable<T> items, string name, StringComparison sc = StringComparison.Ordinal)
    where T : IListItem =>
    items.SingleOrDefault(x => x.Name.Equals(name, sc));
}