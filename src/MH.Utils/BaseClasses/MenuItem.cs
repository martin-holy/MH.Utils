using System.Windows.Input;

namespace MH.Utils.BaseClasses;

public class MenuItem : TreeItem {
  public ICommand? Command { get; set; }
  public string? InputGestureText { get; set; }

  public MenuItem(string? icon, string name) : base(icon, name) { }

  public MenuItem(ICommand command) {
    Command = command;
  }

  public void Add(MenuItem menuItem) {
    Items.Add(menuItem);
  }
}