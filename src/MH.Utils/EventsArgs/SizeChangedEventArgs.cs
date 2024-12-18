using MH.Utils.Types;

namespace MH.Utils.EventsArgs;

public class SizeChangedEventArgs(SizeD previousSize, SizeD newSize, bool widthChanged, bool heightChanged) : RoutedEventArgs {
  public SizeD PreviousSize { get; } = previousSize;
  public SizeD NewSize { get; } = newSize;
  public bool WidthChanged { get; } = widthChanged;
  public bool HeightChanged { get; } = heightChanged;
}