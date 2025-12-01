using BenchmarkDotNet.Attributes;
using MH.Utils.Tests;
using System.Collections.ObjectModel;
using System.ComponentModel;
using static MH.Utils.Tests.BindingUTests;

namespace MH.Utils.Benchmarks;

[ShortRunJob]
[MemoryDiagnoser]
public class BindingBenchmarks {

  private BindingUTests.Test _root;
  private BindingUTests.TestData _data;
  private ObservableCollection<string> _collection;

  private IDisposable _binding;
  private int _sinkCounter;

  [GlobalSetup]
  public void Setup() {
    _sinkCounter = 0;
    _data = new BindingUTests.TestData { Name = "A", Strings = ["X"] };
    _root = new BindingUTests.Test { Data = _data };
    _collection = _data.Strings!;
  }

  [Benchmark]
  public void Create_RootProperty_Binding() {
    _binding?.Dispose();
    _binding = _data.Bind(_data, nameof(_data.Name), x => x.Name, (t, p) => _sinkCounter++);
  }

  [Benchmark]
  public void Create_NestedProperty_Binding() {
    _binding?.Dispose();
    _binding = _root.Bind<Test, Test, string>(
      _root,
      [nameof(Test.Data), nameof(TestData.Name)],
      [s => (s as Test)?.Data, s => (s as TestData)?.Name],
      (t, p) => _sinkCounter++);
  }

  [Benchmark]
  public void Create_RootCollection_Binding() {
    _binding?.Dispose();
    _binding = _data.Bind(_data, nameof(_data.Strings), s => s.Strings, (t, c, e) => _sinkCounter++);
  }

  [Benchmark]
  public void Create_NestedCollection_Binding() {
    _binding?.Dispose();
    _binding = _root.Bind<Test, Test, ObservableCollection<string>>(
      _root,
      [nameof(Test.Data), nameof(TestData.Strings)],
      [s => (s as Test)?.Data, s => (s as TestData)?.Strings],
      (t, c, e) => _sinkCounter++);
  }

  [Benchmark]
  public void Direct_PropertyChanged_Subscription() {
    void Handler(object? s, PropertyChangedEventArgs e) {
      _sinkCounter++;
    }

    _data.PropertyChanged += Handler;
    _data.Name = "Z";
    _data.PropertyChanged -= Handler;
  }

  /*[Benchmark]
  public void Change_RootProperty() {
    _data.Name = "B";
  }

  [Benchmark]
  public void Change_NestedProperty() {
    _root.Data.Name = "B";
  }

  [Benchmark]
  public void Change_RootCollection_AddItem() {
    _data.Strings!.Add("A");
  }

  [Benchmark]
  public void Change_RootCollection_RemoveItem() {
    if (_data.Strings!.Contains("X"))
      _data.Strings.Remove("X");
  }

  [Benchmark]
  public void Change_NestedCollection_AddItem() {
    _root.Data.Strings!.Add("B");
  }

  [Benchmark]
  public void Change_NestedCollection_RemoveItem() {
    var coll = _root.Data.Strings!;
    if (coll.Count > 0)
      coll.RemoveAt(0);
  }

  [Benchmark]
  public void Replace_RootCollection() {
    _data.Strings = new ObservableCollection<string> { "A", "B" };
  }

  [Benchmark]
  public void Replace_NestedCollection() {
    _root.Data.Strings = new ObservableCollection<string> { "P", "Q" };
  }

  [Benchmark]
  public void Replace_NestedObject() {
    _root.Data = new BindingUTests.TestData {
      Name = "NEW",
      Strings = new ObservableCollection<string> { "M" }
    };
  }*/
}