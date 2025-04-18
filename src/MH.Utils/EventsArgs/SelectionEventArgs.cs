﻿using System;
using System.Collections.Generic;

namespace MH.Utils.EventsArgs; 

public class SelectionEventArgs<T>(List<T> items, T item, bool isCtrlOn, bool isShiftOn) : EventArgs {
  public List<T> Items { get; } = items;
  public T Item { get; } = item;
  public bool IsCtrlOn { get; } = isCtrlOn;
  public bool IsShiftOn { get; } = isShiftOn;
}