using MH.Utils.BaseClasses;
using System.Collections.ObjectModel;

namespace MH.Utils.Tests;

[TestClass]
public class BindingUTests {

  public class Test : ObservableObject {
    private TestData? _data;
    public TestData? Data { get { return _data; } set { _data = value; OnPropertyChanged(); } }
  }

  public class TestData : ObservableObject {
    private string? _name;
    private ObservableCollection<string>? _strings;

    public string? Name { get { return _name; } set { _name = value; OnPropertyChanged(); } }
    public ObservableCollection<string>? Strings { get => _strings; set { _strings = value; OnPropertyChanged(); } }
  }

  [TestMethod]
  public void RootProperty_InitialAndChange() {
    var list = new List<string?>();
    var o = new TestData { Name = "A" };
    o.Bind(o, nameof(o.Name), x => x.Name, (t, p) => list.Add(p));
    o.Name = "B";
    CollectionAssert.AreEqual(new[] { "A", "B" }, list);
  }

  [TestMethod]
  public void RootProperty_NullToValue() {
    var list = new List<string?>();
    var o = new TestData { Name = null };
    o.Bind(o, nameof(o.Name), x => x.Name, (t, p) => list.Add(p));
    o.Name = "X";
    CollectionAssert.AreEqual(new[] { null, "X" }, list);
  }

  [TestMethod]
  public void RootProperty_ValueToNull() {
    var list = new List<string?>();
    var o = new TestData { Name = "A" };
    o.Bind(o, nameof(o.Name), x => x.Name, (t, p) => list.Add(p));
    o.Name = null;
    CollectionAssert.AreEqual(new[] { "A", null }, list);
  }

  [TestMethod]
  public void NestedProperty_InitialAndChange() {
    var list = new List<string?>();
    var o = new Test { Data = new TestData { Name = "A" } };
    o.Bind<Test, string>(
      o,
      [nameof(Test.Data), nameof(TestData.Name)],
      [s => (s as Test)?.Data, s => (s as TestData)?.Name],
      (t, p) => list.Add(p));
    o.Data.Name = "B";
    CollectionAssert.AreEqual(new[] { "A", "B" }, list);
  }

  [TestMethod]
  public void NestedProperty_Parent_NullToValue() {
    var list = new List<string?>();
    var o = new Test { Data = null };
    o.Bind<Test, string>(
      o,
      [nameof(Test.Data), nameof(TestData.Name)],
      [s => (s as Test)?.Data, s => (s as TestData)?.Name],
      (t, p) => list.Add(p));
    o.Data = new TestData { Name = "X" };
    CollectionAssert.AreEqual(new[] { null, "X" }, list);
  }

  [TestMethod]
  public void NestedProperty_Parent_ValueToNull() {
    var list = new List<string?>();
    var o = new Test { Data = new TestData { Name = "X" } };
    o.Bind<Test, string>(
      o,
      [nameof(Test.Data), nameof(TestData.Name)],
      [s => (s as Test)?.Data, s => (s as TestData)?.Name],
      (t, p) => list.Add(p));
    o.Data = null;
    CollectionAssert.AreEqual(new[] { "X", null }, list);
  }

  [TestMethod]
  public void Collection_InitialAndAdd() {
    var list = new List<string?>();
    var col = new ObservableCollection<string>() { "A" };
    this.Bind(
      col,
      (t, c, e) => { list.Add(c == null ? null : string.Join(", ", c)); });
    col.Add("B");
    CollectionAssert.AreEqual(new[] { "A", "A, B" }, list);
  }

  [TestMethod]
  public void RootCollection_InitialAndChange() {
    var list = new List<string?>();
    var o = new TestData { Strings = ["A"] };
    o.Bind(
      o,
      nameof(o.Strings),
      s => s.Strings,
      (t, c, e) => { list.Add(c == null ? null : string.Join(", ", c)); });
    o.Strings = ["B", "C"];
    CollectionAssert.AreEqual(new[] { "A", "B, C" }, list);
  }

  [TestMethod]
  public void RootCollection_NullToValue() {
    var list = new List<string?>();
    var o = new TestData { Strings = null };
    o.Bind(
      o,
      nameof(o.Strings),
      s => s.Strings,
      (t, c, e) => { list.Add(c == null ? null : string.Join(", ", c)); });
    o.Strings = ["A", "B"];
    CollectionAssert.AreEqual(new[] { null, "A, B" }, list);
  }

  [TestMethod]
  public void RootCollection_ValueToNull() {
    var list = new List<string?>();
    var o = new TestData { Strings = ["A", "B"] };
    o.Bind(
      o,
      nameof(o.Strings),
      s => s.Strings,
      (t, c, e) => { list.Add(c == null ? null : string.Join(", ", c)); });
    o.Strings = null;
    CollectionAssert.AreEqual(new[] { "A, B", null }, list);
  }

  [TestMethod]
  public void RootCollection_AddRemove() {
    var list = new List<string?>();
    var o = new TestData { Strings = ["X"] };
    o.Bind(
      o,
      nameof(o.Strings),
      s => s.Strings,
      (t, c, e) => { list.Add(c == null ? null : string.Join(", ", c)); });
    o.Strings.Add("A");
    o.Strings.Add("B");
    o.Strings.Remove("A");
    CollectionAssert.AreEqual(new[] { "X", "X, A", "X, A, B", "X, B" }, list);
  }

  [TestMethod]
  public void NestedCollection_InitialAndChange() {
    var list = new List<string?>();
    var o = new Test { Data = new TestData { Strings = ["A"] } };
    o.Bind<Test, ObservableCollection<string>>(
      o,
      [nameof(Test.Data), nameof(TestData.Strings)],
      [s => (s as Test)?.Data, s => (s as TestData)?.Strings],
      (t, c, e) => { list.Add(c == null ? null : string.Join(", ", c)); });
    o.Data.Strings = ["B", "C"];
    CollectionAssert.AreEqual(new[] { "A", "B, C" }, list);
  }

  [TestMethod]
  public void NestedCollection_NullToValue() {
    var list = new List<string?>();
    var o = new Test { Data = new TestData { Strings = null } };
    o.Bind<Test, ObservableCollection<string>>(
      o,
      [nameof(Test.Data), nameof(TestData.Strings)],
      [s => (s as Test)?.Data, s => (s as TestData)?.Strings],
      (t, c, e) => { list.Add(c == null ? null : string.Join(", ", c)); });
    o.Data.Strings = ["A"];
    CollectionAssert.AreEqual(new[] { null, "A" }, list);
  }

  [TestMethod]
  public void NestedCollection_Parent_ValueToValue() {
    var list = new List<string?>();
    var o = new Test { Data = new TestData { Strings = ["A"] } };
    o.Bind<Test, ObservableCollection<string>>(
      o,
      [nameof(Test.Data), nameof(TestData.Strings)],
      [s => (s as Test)?.Data, s => (s as TestData)?.Strings],
      (t, c, e) => { list.Add(c == null ? null : string.Join(", ", c)); });
    o.Data = new TestData { Strings = ["B", "C"] };
    CollectionAssert.AreEqual(new[] { "A", "B, C" }, list);
  }

  [TestMethod]
  public void NestedCollection_Parent_NullToValue() {
    var list = new List<string?>();
    var o = new Test { Data = null };
    o.Bind<Test, ObservableCollection<string>>(
      o,
      [nameof(Test.Data), nameof(TestData.Strings)],
      [s => (s as Test)?.Data, s => (s as TestData)?.Strings],
      (t, c, e) => { list.Add(c == null ? null : string.Join(", ", c)); });
    o.Data = new TestData { Strings = ["A", "B"] };
    CollectionAssert.AreEqual(new[] { null, "A, B" }, list);
  }

  [TestMethod]
  public void NestedCollection_Parent_ValueToNull() {
    var list = new List<string?>();
    var o = new Test { Data = new TestData { Strings = ["A", "B"] } };
    o.Bind<Test, ObservableCollection<string>>(
      o,
      [nameof(Test.Data), nameof(TestData.Strings)],
      [s => (s as Test)?.Data, s => (s as TestData)?.Strings],
      (t, c, e) => { list.Add(c == null ? null : string.Join(", ", c)); });
    o.Data = null;
    CollectionAssert.AreEqual(new[] { "A, B", null }, list);
  }
}