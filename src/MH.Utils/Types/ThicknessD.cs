namespace MH.Utils.Types;

public struct ThicknessD(double left, double top, double right, double bottom) {
  public double Left { get; set; } = left;
  public double Top { get; set; } = top;
  public double Right { get; set; } = right;
  public double Bottom { get; set; } = bottom;

  public ThicknessD(double uniformLength) :
    this(uniformLength, uniformLength, uniformLength, uniformLength) { }
}