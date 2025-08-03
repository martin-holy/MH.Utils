using System;
using System.Runtime.CompilerServices;

namespace MH.Utils.BaseClasses;

public abstract class RelayCommandBase : ObservableObject {
  public string? Icon { get; set; }
  public string? Text { get; set; }

  protected Func<bool>? _canExecuteFunc;

  private event EventHandler? _canExecuteChanged;
  private static readonly ConditionalWeakTable<EventHandler, object> _wpfSubscribers = new();

  public event EventHandler? CanExecuteChanged {
    add {
      _canExecuteChanged += value;

      if (System.OperatingSystem.IsWindows() && value != null)
        _wpfSubscribers.Add(value, this);
    }

    remove {
      _canExecuteChanged -= value;

      if (System.OperatingSystem.IsWindows() && value != null)
        _wpfSubscribers.Remove(value);
    }
  }

  protected RelayCommandBase() { }

  protected RelayCommandBase(string? icon, string? text) {
    Icon = icon;
    Text = text;
  }

  public void RaiseCanExecuteChanged() =>
    _canExecuteChanged?.Invoke(this, EventArgs.Empty);

  public static void RaiseAllCanExecuteChanged(object? o, EventArgs e) {
    foreach (var handler in _wpfSubscribers)
      handler.Key?.Invoke(handler.Value, EventArgs.Empty);
  }

  public virtual bool CanExecute(object? parameter) =>
    _canExecuteFunc == null || _canExecuteFunc();
}