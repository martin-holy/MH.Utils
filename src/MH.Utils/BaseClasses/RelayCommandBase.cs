using System;
using System.Runtime.CompilerServices;

namespace MH.Utils.BaseClasses;

public abstract class RelayCommandBase : ObservableObject {
  private string? _icon;
  private string? _text;

  public string? Icon { get => _icon; set { _icon = value; OnPropertyChanged(); } }
  public string? Text { get => _text; set { _text = value; OnPropertyChanged(); } }

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
    _icon = icon;
    _text = text;
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