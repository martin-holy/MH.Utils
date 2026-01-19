namespace MH.Utils;

public static class MathU {
  public static int GreatestCommonDivisor(int a, int b) {
    while (a != 0 && b != 0) {
      if (a > b)
        a %= b;
      else
        b %= a;
    }
    return a == 0 ? b : a;
  }
}