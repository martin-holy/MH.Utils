using MH.Utils.DB;

namespace MH.Utils.Tests.DB;

[TestClass]
public class IdParserTests {
  [TestMethod]
  public void IdParser_Parse_Id() {
    int id = CsvParser.ParseInt("487");
    Assert.AreEqual(487, id);
  }

  [TestMethod]
  public void IdParser_Parse_IdOrDefault1() {
    int id = CsvParser.ParseIntOrDefault("487", 4);
    Assert.AreEqual(487, id);
  }

  [TestMethod]
  public void IdParser_Parse_IdOrDefault2() {
    int id = CsvParser.ParseIntOrDefault(string.Empty, 4);
    Assert.AreEqual(4, id);
  }

  [TestMethod]
  public void IdParser_Parse_Ids() {
    List<int> result = [];

    CsvParser.ParseInts("487,499,46787,97974,789974", result, static (state, id) => state.Add(id));

    CollectionAssert.AreEqual(new[] { 487, 499, 46787, 97974, 789974 }, result);
  }
}