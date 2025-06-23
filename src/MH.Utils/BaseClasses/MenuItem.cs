using MH.Utils.Interfaces;
using System.Linq;
using System.Windows.Input;

namespace MH.Utils.BaseClasses;

public class MenuItem : TreeItem {
  public ICommand? Command { get; set; }
  public string? InputGestureText { get; set; }
  public new string Icon => !string.IsNullOrEmpty(base.Icon) ? base.Icon : (Command as RelayCommand)?.Icon ?? string.Empty;
  public string Text => !string.IsNullOrEmpty(Name) ? Name : (Command as RelayCommand)?.Text ?? string.Empty;

  public MenuItem(string? icon, string name) : base(icon, name) { }

  public MenuItem(ICommand command) {
    Command = command;
  }

  public void Add(MenuItem menuItem) {
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