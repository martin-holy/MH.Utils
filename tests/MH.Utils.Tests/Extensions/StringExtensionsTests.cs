using MH.Utils.Extensions;
using System.Globalization;

namespace MH.Utils.Tests.Extensions;

[TestClass]
public class StringExtensionsTests {
  [TestMethod]
  public void ToDouble_NullOrEmptyValue_ReturnsNull() {
    string? value = null;
    IFormatProvider provider = CultureInfo.InvariantCulture;

    var result = value.ToDouble(provider);

    Assert.AreEqual(null, result);
  }

  [TestMethod]
  public void ToDouble_ValidValue_ReturnsDouble() {
    var value = "123.45";
    IFormatProvider provider = CultureInfo.InvariantCulture;

    var result = value.ToDouble(provider);

    Assert.AreEqual(123.45, result);
  }
}