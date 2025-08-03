using System.Windows.Input;

namespace MH.Utils.Extensions;

public static class CommandExtensions {
  public static void ExecuteIfCan(this ICommand command, object? parameter) {
    if (command.CanExecute(parameter)) command.Execute(parameter);
  }
}