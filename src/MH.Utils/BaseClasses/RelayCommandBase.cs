using System;

namespace MH.Utils.BaseClasses;

public abstract class RelayCommandBase : ObservableObject {
  public string? Icon { get; set; }
  public string? Text { get; set; }

  protected Func<bool>? _canExecuteFunc;

  public static event EventHandler? CanExecuteChangedEvent;

  public event EventHandler? CanExecuteChanged {
    add => CanExecuteChangedEvent += value;
    remove => CanExecuteChangedEvent -= value;
  }

  protected RelayCommandBase() { }

  protected RelayCommandBase(string? icon, string? text) {
    Icon = icon;
    Text = text;
  }

  protected void _raiseCanExecuteChanged() =>
    RaiseCanExecuteChanged(this, EventArgs.Empty);

  public static void RaiseCanExecuteChanged(object? o, EventArgs e) =>
    CanExecuteChangedEvent?.Invoke(o, e);

  public virtual bool CanExecute(object? parameter) =>
    _canExecuteFunc == null || _canExecuteFunc();
}