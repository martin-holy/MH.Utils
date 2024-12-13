using System;

namespace MH.Utils.BaseClasses;

public class SelectionRange : ObservableObject {
  private double _min;
  private double _max;
  private double _start;
  private double _end;

  public double Min { get => _min; set { _min = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsOnMin)); } }
  public double Max { get => _max; set { _max = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsOnMax)); } }
  public double Start { get => _start; set { _start = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsOnMin)); } }
  public double End { get => _end; set { _end = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsOnMax)); } }
  public bool IsOnMin => _min == _start;
  public bool IsOnMax => _max == _end;
  public bool IsFullRange => IsOnMin && IsOnMax;

  public event EventHandler? ChangedEvent;

  public bool Fits(double value) =>
    value >= Start && value <= End;

  public void RaiseChanged() => ChangedEvent?.Invoke(this, EventArgs.Empty);

  public void Reset(double min, double max) {
    Min = min;
    Start = min;
    End = max;
    Max = max;
  }

  public void SetFullRange() {
    Start = _min;
    End = _max;
  }

  public void Zero() {
    Start = 0;
    End = 0;
    Min = 0;
    Max = 0;
  }
}