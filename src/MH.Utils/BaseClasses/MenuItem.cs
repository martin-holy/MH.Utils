using MH.Utils.Extensions;
using MH.Utils.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace MH.Utils.BaseClasses;

public class MenuItem : TreeItem {
  public ICommand? Command { get; }
  public object? CommandParameter { get; set; }
  public string? InputGestureText { get; set; }
  public new string Icon => !string.IsNullOrEmpty(base.Icon) ? base.Icon : (Command as RelayCommandBase)?.Icon ?? string.Empty;
  public string Text => !string.IsNullOrEmpty(Name) ? Name : (Command as RelayCommandBase)?.Text ?? string.Empty;

  public MenuItem(string? icon, string name) : base(icon, name) { }

  public MenuItem(string? icon, string name, IEnumerable<ITreeItem> items) : this(icon, name) {
    foreach (var item in items) Add(item);
  }

  public MenuItem(ICommand command, object? commandParameter = null) {
    Command = command;
    CommandParameter = commandParameter;

    if (Command is RelayCommandBase cmd) {
      this.Bind(cmd, nameof(RelayCommandBase.Icon), x => x.Icon, (s, _) => s.OnPropertyChanged(nameof(Icon)), false);
      this.Bind(cmd, nameof(RelayCommandBase.Text), x => x.Text, (s, _) => s.OnPropertyChanged(nameof(Text)), false);
    }
  }

  public void Add(ITreeItem menuItem) {
    Items.Add(menuItem);
  }

  public MenuItem? GetWithData(object? data) =>
    data == null ? null : Items.SingleOrDefault(x => ReferenceEquals(x.Data, data)) as MenuItem;

  public void RemoveWithData(object? data) {
    if ((GetWithData(data) is not { } menuItem)) return;
    Items.Remove(menuItem);
  }

  public void ReplaceWithData(object? data, MenuItem newMenuItem) {
    if ((GetWithData(data) is not { } menuItem)) return;
    var idx = Items.IndexOf(menuItem);
    Items.RemoveAt(idx);
    Items.Insert(idx, newMenuItem);
  }
}