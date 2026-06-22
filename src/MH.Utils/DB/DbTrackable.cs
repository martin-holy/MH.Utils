using MH.Utils.BaseClasses;
using System.ComponentModel;

namespace MH.Utils.DB;

public interface IDbTrackable : INotifyPropertyChanged {
  bool IsModified { get; set; }
  bool ArePropsModified { get; set; }
  int ChangesCount { get; }
}

public class DbTrackable : ObservableObject, IDbTrackable {
  private bool _isModified;
  private bool _arePropsModified;

  public bool IsModified { get => _isModified; set => _setIsModified(value); }
  public bool ArePropsModified { get => _arePropsModified; set { _arePropsModified = value; OnPropertyChanged(); } }
  public int ChangesCount { get; private set; }

  private void _setIsModified(bool value) {
    _isModified = value;

    if (value)
      ChangesCount++;
    else
      ChangesCount = 0;

    OnPropertyChanged(nameof(IsModified));
    OnPropertyChanged(nameof(ArePropsModified));
  }
}