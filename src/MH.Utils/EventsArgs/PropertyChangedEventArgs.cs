namespace MH.Utils.EventsArgs;

public class PropertyChangedEventArgs<T>(T oldValue, T newValue) : RoutedEventArgs {
  public T NewValue { get; } = newValue;
  public T OldValue { get; } = oldValue;
}