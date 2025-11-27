using MH.Utils.BaseClasses;
using System.Collections.ObjectModel;

namespace MH.Utils.Tests;

[TestClass]
public class BindingUTests {

  class Test : ObservableObject {
    private TestData? _data;
    public TestData? Data { get { return _data; } set { _data = value; OnPropertyChanged(); } }
  }

  class TestData : ObservableObject {
    private string? _name;
    private ObservableCollection<string>? _strings;

    public string? Name { get { return _name; } set { _name = value; OnPropertyChanged(); } }
    public ObservableCollection<string>? Strings { get => _strings; set { _strings = value; OnPropertyChanged(); } }
  }

  [TestMethod]
  public void P1_RootProperty_InitialAndChange() {
    var list = new List<string?>();
    var o = new TestData { Name = "A" };
    o.Bind(o, x => x.Name, (t, p) =>  list.Add(p));
    o.Name = "B";
    CollectionAssert.AreEqual(new[] { "A", "B" }, list);
  }

  [TestMethod]
  public void P2_RootProperty_NullToValue() {
    var list = new List<string?>();
    var o = new TestData { Name = null };
    o.Bind(o, x => x.Name, (t, p) => list.Add(p));
    o.Name = "X";
    CollectionAssert.AreEqual(new[] { null, "X" }, list);
  }

  [TestMethod]
  public void P3_RootProperty_ValueToNull() {
    var list = new List<string?>();
    var o = new TestData { Name = "A" };
    o.Bind(o, x => x.Name, (t, p) => list.Add(p));
    o.Name = null;
    CollectionAssert.AreEqual(new[] { "A", null }, list);
  }

  [TestMethod]
  public void P4_NestedProperty_InitialAndChange() {
    var list = new List<string?>();
    var o = new Test { Data = new TestData { Name = "A" } };
    o.BindNested(o, x => x.Data.Name, (t, p) => list.Add(p));
    o.Data.Name = "B";
    CollectionAssert.AreEqual(new[] { "A", "B" }, list);
  }

  [TestMethod]
  public void P5_NestedProperty_NullData_ThenSetData() {
    var list = new List<string?>();
    var o = new Test { Data = null };
    o.BindNested(o, x => x.Data.Name, (t, p) => list.Add(p));
    o.Data = new TestData { Name = "X" };
    CollectionAssert.AreEqual(new[] { null, "X" }, list);
  }

  [TestMethod]
  public void C1_RootCollection_NullToNew() {
    var list = new List<string?>();
    var o = new TestData { Strings = null };    
    o.BindCollectionNested(o, x => x.Strings, (t, e) => { list.Add(t.Strings == null ? null : string.Join(", ", t.Strings)); });
    o.Strings = new ObservableCollection<string> { "A", "B" };
    CollectionAssert.AreEqual(new List<string?>() { "A, B" }, list);
  }

  [TestMethod]
  public void C2_RootCollection_AddRemove() {
    var list = new List<string?>();
    var o = new TestData { Strings = new ObservableCollection<string>() { "X" } };
    o.BindCollectionNested(o, x => x.Strings, (t, e) => { list.Add(t.Strings == null ? null : string.Join(", ", t.Strings)); });
    o.Strings.Add("A");
    o.Strings.Add("B");
    o.Strings.Remove("A");
    CollectionAssert.AreEqual(new[] {"X", "X, A", "X, A, B", "X, B" }, list);
  }

  [TestMethod]
  public void C3_NestedCollection_NullToNew() {
    var list = new List<string?>();
    var o = new Test { Data = new TestData { Strings = null } };
    o.BindCollectionNested(o, x => x.Data.Strings, (t, e) => { list.Add(t.Data.Strings == null ? null : string.Join(", ", t.Data.Strings)); });
    o.Data.Strings = new ObservableCollection<string> { "A" };
    CollectionAssert.AreEqual(new[] { "A" }, list);
  }

  [TestMethod]
  public void C4_NestedCollection_DataReplaced() {
    var list = new List<string?>();
    var o = new Test { Data = new TestData { Strings = new ObservableCollection<string> { "A" } } };
    o.BindCollectionNested(o, x => x.Data.Strings, (t, e) => { list.Add(t.Data.Strings == null ? null : string.Join(", ", t.Data.Strings)); });
    o.Data = new TestData { Strings = new ObservableCollection<string> { "B", "C" } };
    Assert.AreEqual(2, list.Count);
  }
}