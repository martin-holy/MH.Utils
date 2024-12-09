using MH.Utils.Extensions;
using System.Globalization;

namespace MH.Utils.Tests.Extensions;

[TestClass]
public class DoubleExtensionsTests {
  [TestMethod]
  public void ToString_NullValue_ReturnsEmptyString() {
    double? value = null;
    IFormatProvider provider = CultureInfo.InvariantCulture;

    var result = value.ToString(provider);

    Assert.AreEqual(string.Empty, result);
  }

  [TestMethod]
  public void ToString_ValidValue_ReturnsFormattedString() {
    double? value = 123.45;
    IFormatProvider provider = CultureInfo.InvariantCulture;

    var result = value.ToString(provider);

    Assert.AreEqual("123.45", result);
  }
}