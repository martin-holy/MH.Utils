using System;

namespace MH.Utils.EventsArgs;

public class RoutedEventArgs : EventArgs {
  public object? OriginalSource { get; set; }
  public object? DataContext { get; set; }
}