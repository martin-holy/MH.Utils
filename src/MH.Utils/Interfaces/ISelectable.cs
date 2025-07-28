using System.ComponentModel;

namespace MH.Utils.Interfaces;

public interface ISelectable : INotifyPropertyChanged {
  public bool IsSelected { get; set; }
}