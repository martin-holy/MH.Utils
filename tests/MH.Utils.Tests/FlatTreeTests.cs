using MH.Utils.Tree;
using System.Collections.ObjectModel;

namespace MH.Utils.Tests;

[TestClass]
public class FlatTreeTests {
  [TestMethod]
  public void HiddenItem_NotPresentInFlatTree() {
    var root = new TreeItem(null, "Root") { IsExpanded = true };
    var a = new TreeItem(null, "A") { IsHidden = true };

    root.AddItems([a]);

    var roots = new ObservableCollection<ITreeItem> { root };
    var flatTree = new FlatTree(roots);

    flatTree.Reset();

    Assert.AreEqual(1, flatTree.Items.Count);
    Assert.AreSame(root, flatTree.Items[0].TreeItem);
    Assert.AreEqual(-1, flatTree.IndexOf(a));
  }

  [TestMethod]
  public void Unhide_InExpandedParent_InsertsItem() {
    var root = new TreeItem(null, "Root") { IsExpanded = true };
    var a = new TreeItem(null, "A") { IsHidden = true };

    root.AddItems([a]);

    var flatTree = new FlatTree([root]);
    flatTree.Reset();

    a.IsHidden = false;

    Assert.AreEqual(2, flatTree.Items.Count);
    Assert.AreEqual(1, flatTree.IndexOf(a));
  }

  [TestMethod]
  public void Hide_RemovesWholeSubtree() {
    var root = new TreeItem(null, "Root") { IsExpanded = true };
    var a = new TreeItem(null, "A") { IsExpanded = true };
    var b = new TreeItem(null, "B");

    a.AddItems([b]);
    root.AddItems([a]);

    var flatTree = new FlatTree([root]);
    flatTree.Reset();

    a.IsHidden = true;

    Assert.AreEqual(1, flatTree.Items.Count);
    Assert.AreEqual(-1, flatTree.IndexOf(a));
    Assert.AreEqual(-1, flatTree.IndexOf(b));
  }

  [TestMethod]
  public void Unhide_InCollapsedParent_DoesNotAppearUntilExpanded() {
    var root = new TreeItem(null, "Root");
    var a = new TreeItem(null, "A") { IsHidden = true };

    root.AddItems([a]);

    var flatTree = new FlatTree([root]);
    flatTree.Reset();

    a.IsHidden = false;

    Assert.AreEqual(-1, flatTree.IndexOf(a));

    root.IsExpanded = true;

    Assert.AreNotEqual(-1, flatTree.IndexOf(a));
  }

  [TestMethod]
  public void ExpandCollapse_DoesNotDuplicateNodes() {
    var root = new TreeItem(null, "Root");
    var a = new TreeItem(null, "A");

    root.AddItems([a]);

    var flatTree = new FlatTree([root]);
    flatTree.Reset();

    for (int i = 0; i < 10; i++) {
      root.IsExpanded = true;
      Assert.AreEqual(2, flatTree.Items.Count);

      root.IsExpanded = false;
      Assert.AreEqual(1, flatTree.Items.Count);
    }

    root.IsExpanded = true;

    Assert.AreEqual(2, flatTree.Items.Count);
    Assert.AreEqual(1, flatTree.IndexOf(a));
  }

  [TestMethod]
  public void Unhide_ExpandedSubtree_RestoresChildren() {
    var root = new TreeItem(null, "Root") { IsExpanded = true };
    var a = new TreeItem(null, "A") { IsExpanded = true, IsHidden = true };
    var b = new TreeItem(null, "B");

    a.AddItems([b]);
    root.AddItems([a]);

    var flatTree = new FlatTree([root]);
    flatTree.Reset();

    Assert.AreEqual(1, flatTree.Items.Count);

    a.IsHidden = false;

    Assert.AreEqual(3, flatTree.Items.Count);
    Assert.AreNotEqual(-1, flatTree.IndexOf(a));
    Assert.AreNotEqual(-1, flatTree.IndexOf(b));
  }

  [TestMethod]
  public void AddChild_ToExpandedParent_InsertsImmediately() {
    var root = new TreeItem(null, "Root") { IsExpanded = true };

    var flatTree = new FlatTree([root]);
    flatTree.Reset();

    var a = new TreeItem(null, "A");

    root.AddItems([a]);

    Assert.AreEqual(2, flatTree.Items.Count);
    Assert.AreEqual(1, flatTree.IndexOf(a));
  }

  [TestMethod]
  public void AddChild_ToCollapsedParent_AppearsAfterExpand() {
    var root = new TreeItem(null, "Root");

    var flatTree = new FlatTree([root]);
    flatTree.Reset();

    var a = new TreeItem(null, "A");

    root.AddItems([a]);

    Assert.AreEqual(-1, flatTree.IndexOf(a));

    root.IsExpanded = true;

    Assert.AreEqual(2, flatTree.Items.Count);
    Assert.AreEqual(1, flatTree.IndexOf(a));
  }

  [TestMethod]
  public void RemoveChild_RemovesFromFlatTree() {
    var root = new TreeItem(null, "Root") { IsExpanded = true };
    var a = new TreeItem(null, "A");

    root.AddItems([a]);

    var flatTree = new FlatTree([root]);
    flatTree.Reset();

    root.Items.Remove(a);

    Assert.AreEqual(1, flatTree.Items.Count);
    Assert.AreEqual(-1, flatTree.IndexOf(a));
  }

  [TestMethod]
  public void Unhide_PreservesCorrectOrdering() {
    var root = new TreeItem(null, "Root") { IsExpanded = true };

    var a = new TreeItem(null, "A");
    var b = new TreeItem(null, "B") { IsHidden = true };
    var c = new TreeItem(null, "C");

    root.AddItems([a, b, c]);

    var flatTree = new FlatTree([root]);
    flatTree.Reset();

    b.IsHidden = false;

    Assert.AreEqual(4, flatTree.Items.Count);

    Assert.AreSame(root, flatTree.Items[0].TreeItem);
    Assert.AreSame(a, flatTree.Items[1].TreeItem);
    Assert.AreSame(b, flatTree.Items[2].TreeItem);
    Assert.AreSame(c, flatTree.Items[3].TreeItem);
  }

  [TestMethod]
  public void ExpandCollapse_RaisesExpectedEvents() {
    var root = new TreeItem(null, "Root");
    var a = new TreeItem(null, "A");

    root.AddItems([a]);

    var flatTree = new FlatTree([root]);

    int inserted = 0;
    int removed = 0;
    int expanded = 0;

    flatTree.RangeInsertedEvent += (_, _) => inserted++;
    flatTree.RangeRemovedEvent += (_, _) => removed++;
    flatTree.IsExpandedChangedEvent += _ => expanded++;

    flatTree.Reset();

    root.IsExpanded = true;
    root.IsExpanded = false;

    Assert.AreEqual(1, inserted);
    Assert.AreEqual(1, removed);
    Assert.AreEqual(2, expanded);
  }
}