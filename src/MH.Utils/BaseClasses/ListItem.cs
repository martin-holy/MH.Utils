﻿using MH.Utils.Interfaces;
using System.Collections.Specialized;

namespace MH.Utils.BaseClasses {
  public class ListItem : ObservableObject, IListItem {
    private protected BitVector32 Bits = new(0);

    private string _icon;
    private string _name;

    public bool IsSelected { get => Bits[BitsMasks.IsSelected]; set { Bits[BitsMasks.IsSelected] = value; OnPropertyChanged(); } }
    public bool IsHidden { get => Bits[BitsMasks.IsHidden]; set { Bits[BitsMasks.IsHidden] = value; OnPropertyChanged(); } }
    public bool IsIconHidden { get => Bits[BitsMasks.IsIconHidden]; set { Bits[BitsMasks.IsIconHidden] = value; OnPropertyChanged(); } }
    public bool IsNameHidden { get => Bits[BitsMasks.IsNameHidden]; set { Bits[BitsMasks.IsNameHidden] = value; OnPropertyChanged(); } }
    public string Icon { get => _icon; set { _icon = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public object Data { get; set; }

    public ListItem() { }

    public ListItem(string icon, string name) : this() {
      Icon = icon;
      Name = name;
    }

    public ListItem(string icon, string name, object data) : this(icon, name) {
      Data = data;
    }
  }

  public class ListItem<T> : ListItem {
    private T _content;

    public T Content { get => _content; set { _content = value; OnPropertyChanged(); } }

    public ListItem(T content) {
      Content = content;
    }

    public ListItem(T content, string iconName, string name) : base(iconName, name) {
      Content = content;
    }
  }
}
