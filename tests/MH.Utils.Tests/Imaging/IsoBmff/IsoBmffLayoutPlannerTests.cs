using MH.Utils.Imaging.IsoBmff;
using System.Buffers.Binary;

namespace MH.Utils.Tests.Imaging.IsoBmff;

[TestClass]
public class IsoBmffLayoutPlannerTests {
  [TestMethod]
  public void Layout_NoChange_KeepsOriginalLayout() {
    using var stream = IsoBmffFileTests._createTestFile(32, 32);

    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);

    var original = planner.CreateOriginalLayout();
    var plan = planner.CreateEditedLayout(original, nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov));

    Assert.IsFalse(plan.RequiresFullRewrite);
    _assertSameLayout(original, plan.Layout);
    _assertValidTopLevelLayout(plan.Layout, stream.Length);
  }

  [TestMethod]
  public void Layout_GrowIntoFreeAfter_KeepsMdatFixed() {
    using var stream = IsoBmffFileTests._createTestFile(0, 64);

    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();
    var originalSize = moov.Box.Size;

    IsoBmffFileTests._resize(moov, originalSize + 24);

    var plan = planner.CreateEditedLayout(original, moov);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(originalSize + 24, edited[moov].Size);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
    _assertValidTopLevelLayout(edited, stream.Length);
  }

  [TestMethod]
  public void Layout_GrowIntoFreeBefore_KeepsMdatFixed() {
    using var stream = IsoBmffFileTests._createTestFile(64, 0);

    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();
    var originalSize = moov.Box.Size;

    IsoBmffFileTests._resize(moov, originalSize + 24);

    var plan = planner.CreateEditedLayout(original, moov);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(originalSize + 24, edited[moov].Size);
    Assert.AreEqual(original[moov].Offset - 24, edited[moov].Offset);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
    _assertValidTopLevelLayout(edited, stream.Length);
  }

  [TestMethod]
  public void Layout_GrowUsingBothFreeRegions_KeepsMdatFixed() {
    using var stream = IsoBmffFileTests._createTestFile(16, 16);

    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();
    var originalSize = moov.Box.Size;

    IsoBmffFileTests._resize(moov, originalSize + 24);

    var plan = planner.CreateEditedLayout(original, moov);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(originalSize + 24, edited[moov].Size);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
    _assertValidTopLevelLayout(edited, stream.Length);
  }

  [TestMethod]
  public void Layout_GrowLeavingSmallFragment_MergesIntoEarlierFree() {
    using var stream = IsoBmffFileTests._createTestFile(32, 32);

    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var freeBefore = nodes.Single(x => x.Box.IsFree && x.Box.End <= moov.Box.Offset);
    var freeAfter = nodes.Single(x => x.Box.IsFree && x.Box.Offset >= moov.Box.End);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();

    var originalMoovSize = moov.Box.Size;
    var originalBeforeSize = original[freeBefore].Size;
    var originalAfterSize = original[freeAfter].Size;

    // Leave 7 bytes in the free box after moov.
    IsoBmffFileTests._resize(moov, originalMoovSize + originalAfterSize - 7);

    var plan = planner.CreateEditedLayout(original, moov);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(originalMoovSize + originalAfterSize - 7, edited[moov].Size);
    Assert.AreEqual(originalBeforeSize + 7, edited[freeBefore].Size);
    Assert.IsFalse(edited.TryGetValue(freeAfter, out _));
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
    _assertValidTopLevelLayout(edited, stream.Length);
  }

  [TestMethod]
  public void Layout_GrowLeavingSmallFragment_WithoutAnotherFreeRequiresRewrite() {
    using var stream = IsoBmffFileTests._createTestFile(0, 32);

    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();

    IsoBmffFileTests._resize(moov, moov.Box.Size + 25);

    var plan = planner.CreateEditedLayout(original, moov);

    Assert.IsTrue(plan.RequiresFullRewrite);
    Assert.AreEqual(moov.Box.Size, plan.Layout[moov].Size);
  }

  [TestMethod]
  public void Layout_ShrinkIntoFreeAfter_EnlargesFree() {
    using var stream = IsoBmffFileTests._createTestFile(0, 32);

    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var free = nodes.Single(x => x.Box.IsFree);
    var mdat = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();
    var originalSize = moov.Box.Size;

    IsoBmffFileTests._resize(moov, originalSize - 24);

    var plan = planner.CreateEditedLayout(original, moov);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(originalSize - 24, edited[moov].Size);
    Assert.AreEqual(original[free].Size + 24, edited[free].Size);
    Assert.AreEqual(original[free].Offset - 24, edited[free].Offset);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
    _assertValidTopLevelLayout(edited, stream.Length);
  }

  [TestMethod]
  public void Layout_ShrinkWithLaterFree_MovesSmallBoxesLeft() {
    using var stream = IsoBmffFileTests._createTestFile(0, 32);

    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var uuid = new IsoBmffNode(new IsoBmffBox(moov.Box.End, 8, 20, IsoBmffTypes.Uuid), -1);

    nodes.Insert(nodes.IndexOf(moov) + 1, uuid);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();
    var originalUuidOffset = original[uuid].Offset;

    IsoBmffFileTests._resize(moov, moov.Box.Size - 6);

    var plan = planner.CreateEditedLayout(original, moov);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(originalUuidOffset - 6, edited[uuid].Offset);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
  }

  [TestMethod]
  public void Layout_ShrinkByLessThanFreeHeader_UsesLaterFree() {
    using var stream = IsoBmffFileTests._createTestFile(0, 32);

    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var free = nodes.Single(x => x.Box.IsFree);
    var mdat = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();

    IsoBmffFileTests._resize(moov, moov.Box.Size - 3);

    var plan = planner.CreateEditedLayout(original, moov);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(original[free].Size + 3, edited[free].Size);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
    _assertValidTopLevelLayout(edited, stream.Length);
  }

  [TestMethod]
  public void Layout_ShrinkByLessThanFreeHeader_WithoutFreeRequiresRewrite() {
    using var stream = IsoBmffFileTests._createTestFile(0, 0);

    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();

    IsoBmffFileTests._resize(moov, moov.Box.Size - 3);

    var plan = planner.CreateEditedLayout(original, moov);

    Assert.IsTrue(plan.RequiresFullRewrite);
    Assert.AreEqual(moov.Box.Size, plan.Layout[moov].Size);
  }

  [TestMethod]
  public void Layout_ShrinkWithoutFree_CreatesFree() {
    using var stream = IsoBmffFileTests._createTestFile(0, 0);

    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();

    IsoBmffFileTests._resize(moov, moov.Box.Size - 16);

    var plan = planner.CreateEditedLayout(original, moov);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);

    var free = edited.TopLevelBoxes.Single(x => x.Node.Box.IsFree);

    Assert.AreEqual(16, free.Size);
    Assert.AreEqual(edited[moov].End, free.Offset);
    _assertValidTopLevelLayout(edited, stream.Length);
  }

  [TestMethod]
  public void Layout_LargeBox_RequiresFullRewrite() {
    using var stream = IsoBmffFileTests._createTestFile(0, 0);

    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();

    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    mdat.Box = new IsoBmffBox(
      mdat.Box.Offset,
      mdat.Box.HeaderSize,
      IsoBmffFileTests.ExpensiveBoxSize,
      mdat.Box.Type);

    var planner = new IsoBmffLayoutPlanner(
      nodes,
      reader.GetBoxChildrenOffset,
      IsoBmffFileTests.ExpensiveBoxSize);

    var original = planner.CreateOriginalLayout();
    IsoBmffFileTests._resize(moov, moov.Box.Size + 16);

    var plan = planner.CreateEditedLayout(original, moov);

    Assert.IsTrue(plan.RequiresFullRewrite);
    Assert.AreEqual(moov.Box.Size, plan.Layout[moov].Size);
  }

  [TestMethod]
  public void Layout_LargeNonMdatBox_RequiresFullRewrite() {
    using var stream = IsoBmffFileTests._createTestFile(0, 0);

    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();

    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var large = new IsoBmffNode(
      new IsoBmffBox(moov.Box.End, 8, IsoBmffFileTests.ExpensiveBoxSize, IsoBmffTypes.Uuid),
      -1);

    nodes.Insert(nodes.IndexOf(moov) + 1, large);

    var planner = new IsoBmffLayoutPlanner(
      nodes,
      reader.GetBoxChildrenOffset,
      IsoBmffFileTests.ExpensiveBoxSize);

    var original = planner.CreateOriginalLayout();
    IsoBmffFileTests._resize(moov, moov.Box.Size + 16);

    var plan = planner.CreateEditedLayout(original, moov);

    Assert.IsTrue(plan.RequiresFullRewrite);
  }

  [TestMethod]
  public void Layout_MultipleEdits_GrowBoth_UsesSameFree() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();

    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var free = nodes.Single(x => x.Box.IsFree && x.Box.Offset >= moov.Box.End);

    var uuid = new IsoBmffNode(new IsoBmffBox(moov.Box.End, 8, 20, IsoBmffTypes.Uuid), -1);

    nodes.Insert(nodes.IndexOf(moov) + 1, uuid);

    var original = new IsoBmffLayout(nodes.Select(x => new IsoBmffLayoutBox(x, x.Box.Offset, x.Box.Size)));

    moov.Box = new IsoBmffBox(moov.Box.Offset, moov.Box.HeaderSize, moov.Box.Size + 8, moov.Box.Type);
    uuid.Box = new IsoBmffBox(uuid.Box.Offset, uuid.Box.HeaderSize, uuid.Box.Size + 8, uuid.Box.Type);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var plan = planner.CreateEditedLayout(original, [moov, uuid]);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(original[moov].Size + 8, edited[moov].Size);
    Assert.AreEqual(original[uuid].Size + 8, edited[uuid].Size);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
    Assert.AreEqual(original[free].Size - 16, edited[free].Size);
  }

  [TestMethod]
  public void Layout_MultipleEdits_FirstGrowthMovesSecondEditedBox() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();

    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var uuid = new IsoBmffNode(new IsoBmffBox(moov.Box.End, 8, 20, IsoBmffTypes.Uuid), -1);

    nodes.Insert(nodes.IndexOf(moov) + 1, uuid);

    var original = new IsoBmffLayout(nodes.Select(x => new IsoBmffLayoutBox(x, x.Box.Offset, x.Box.Size)));

    var originalUuidOffset = uuid.Box.Offset;

    moov.Box = new IsoBmffBox(moov.Box.Offset, moov.Box.HeaderSize, moov.Box.Size + 8, moov.Box.Type);
    uuid.Box = new IsoBmffBox(uuid.Box.Offset, uuid.Box.HeaderSize, uuid.Box.Size + 8, uuid.Box.Type);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var plan = planner.CreateEditedLayout(original, [moov, uuid]);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(originalUuidOffset + 8, edited[uuid].Offset);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
  }

  [TestMethod]
  public void Layout_MultipleEdits_GrowAndShrink_Cancels() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();

    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var free = nodes.Single(x => x.Box.IsFree && x.Box.Offset >= moov.Box.End);

    var uuid = new IsoBmffNode(new IsoBmffBox(moov.Box.End, 8, 20, IsoBmffTypes.Uuid), -1);

    nodes.Insert(nodes.IndexOf(moov) + 1, uuid);

    var original = new IsoBmffLayout(nodes.Select(x => new IsoBmffLayoutBox(x, x.Box.Offset, x.Box.Size)));

    moov.Box = new IsoBmffBox(moov.Box.Offset, moov.Box.HeaderSize, moov.Box.Size + 12, moov.Box.Type);
    uuid.Box = new IsoBmffBox(uuid.Box.Offset, uuid.Box.HeaderSize, uuid.Box.Size - 12, uuid.Box.Type);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var plan = planner.CreateEditedLayout(original, [moov, uuid]);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(original[moov].Size + 12, edited[moov].Size);
    Assert.AreEqual(original[uuid].Size - 12, edited[uuid].Size);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
    Assert.AreEqual(original[free].Size, edited[free].Size);
  }

  [TestMethod]
  public void Layout_MultipleEdits_ShrinkBoth_EnlargesSameFree() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();

    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var free = nodes.Single(x => x.Box.IsFree && x.Box.Offset >= moov.Box.End);
    var mdat = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var uuid = new IsoBmffNode(new IsoBmffBox(moov.Box.End, 8, 20, IsoBmffTypes.Uuid), -1);

    nodes.Insert(nodes.IndexOf(moov) + 1, uuid);

    var original = new IsoBmffLayout(nodes.Select(x => new IsoBmffLayoutBox(x, x.Box.Offset, x.Box.Size)));

    moov.Box = new IsoBmffBox(moov.Box.Offset, moov.Box.HeaderSize, moov.Box.Size - 8, moov.Box.Type);
    uuid.Box = new IsoBmffBox(uuid.Box.Offset, uuid.Box.HeaderSize, uuid.Box.Size - 8, uuid.Box.Type);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var plan = planner.CreateEditedLayout(original, [moov, uuid]);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(original[moov].Size - 8, edited[moov].Size);
    Assert.AreEqual(original[uuid].Size - 8, edited[uuid].Size);
    Assert.AreEqual(original[free].Size + 16, edited[free].Size);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
  }

  [TestMethod]
  public void Layout_MultipleEdits_ReverseInputOrder_IsDeterministic() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();

    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var uuid = new IsoBmffNode(new IsoBmffBox(moov.Box.End, 8, 20, IsoBmffTypes.Uuid), -1);

    nodes.Insert(nodes.IndexOf(moov) + 1, uuid);

    var original = new IsoBmffLayout(nodes.Select(x => new IsoBmffLayoutBox(x, x.Box.Offset, x.Box.Size)));

    moov.Box = new IsoBmffBox(moov.Box.Offset, moov.Box.HeaderSize, moov.Box.Size + 8, moov.Box.Type);
    uuid.Box = new IsoBmffBox(uuid.Box.Offset, uuid.Box.HeaderSize, uuid.Box.Size + 8, uuid.Box.Type);

    var planner = new IsoBmffLayoutPlanner(nodes, reader.GetBoxChildrenOffset);
    var plan = planner.CreateEditedLayout(original, [uuid, moov]);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(original[moov].Size + 8, edited[moov].Size);
    Assert.AreEqual(original[uuid].Size + 8, edited[uuid].Size);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
  }

  [TestMethod]
  public void Layout_MetadataEdits_UsesOriginalAndEditedTrees() {
    using var stream = IsoBmffFileTests._createTestFile(0, 128, withItemList: false);

    var file = new IsoBmffFile(stream);
    var originalNodes = file._metadata._editor._getOriginalNodes();
    var originalPlanner = new IsoBmffLayoutPlanner(originalNodes, originalNodes, file._reader.GetBoxChildrenOffset);
    var original = originalPlanner.CreateOriginalLayout();

    IsoBmffLayoutPlan CreatePlan() {
      var editedNodes = file._metadata._editor.GetEditedNodes();
      var planner = new IsoBmffLayoutPlanner(originalNodes, editedNodes, file._reader.GetBoxChildrenOffset);

      return planner.CreateEditedLayout(original, editedNodes);
    }

    file._metadata.SetKeywords("hello");

    var editedNodes = file._metadata._editor.GetEditedNodes();
    var keyw = editedNodes.Single(x => x.Box.Type == IsoBmffTypes.Keyw);
    var data = editedNodes.Single(x => x.Box.Type == IsoBmffTypes.Data);
    var meta = editedNodes.Single(x => x.Box.Type == IsoBmffTypes.Meta);
    var ilst = editedNodes.Single(x => x.Box.Type == IsoBmffTypes.Ilst);

    var plan = CreatePlan();

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(keyw.Box.Size, plan.Layout[keyw].Size);
    Assert.AreEqual(data.Box.Size, plan.Layout[data].Size);
    Assert.AreEqual(
      plan.Layout[meta].Offset + meta.Box.HeaderSize + meta.Data!.LongLength,
      plan.Layout[ilst].Offset);
    _assertValidTopLevelLayout(plan.Layout, stream.Length);

    file._metadata.SetKeywords("a longer value");
    plan = CreatePlan();

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(keyw.Box.Size, plan.Layout[keyw].Size);
    Assert.AreEqual(data.Box.Size, plan.Layout[data].Size);

    file._metadata.SetKeywords(null);
    plan = CreatePlan();

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.IsFalse(plan.Layout.TryGetValue(keyw, out _));
    Assert.IsFalse(plan.Layout.TryGetValue(data, out _));
    _assertValidTopLevelLayout(plan.Layout, stream.Length);
  }

  [TestMethod]
  public void Layout_InsertTopLevelBox_ConsumesAdjacentFreeSpace() {
    using var stream = IsoBmffFileTests._createTestFile(0, 64);
    var reader = new IsoBmffReader(stream);
    var originalNodes = reader._readNodes();
    var editedNodes = originalNodes.ToList();
    var mdat = originalNodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var free = originalNodes.Single(x => x.Box.IsFree && x.Box.Offset < mdat.Box.Offset);
    var newNode = new IsoBmffNode(new IsoBmffBox(-1, 8, 24, IsoBmffTypes.Uuid), -1, data: new byte[16]);

    var original = new IsoBmffLayoutPlanner(
      originalNodes,
      originalNodes,
      reader.GetBoxChildrenOffset).CreateOriginalLayout();

    editedNodes.Insert(editedNodes.IndexOf(mdat), newNode);

    var plan = new IsoBmffLayoutPlanner(
      originalNodes,
      editedNodes,
      reader.GetBoxChildrenOffset).CreateEditedLayout(original, editedNodes);

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(original[mdat].Offset, plan.Layout[mdat].Offset);
    Assert.AreEqual(original[free].Size - newNode.Box.Size, plan.Layout[free].Size);
    Assert.AreEqual(original[mdat].Offset - newNode.Box.Size, plan.Layout[newNode].Offset);
    Assert.AreEqual(original[mdat].Offset, plan.Layout[newNode].End);
    _assertValidTopLevelLayout(plan.Layout, stream.Length);
  }

  [TestMethod]
  public void Layout_InsertTopLevelBox_WithoutFreeSpaceRequiresFullRewrite() {
    using var stream = IsoBmffFileTests._createTestFile(0, 0);
    var reader = new IsoBmffReader(stream);
    var originalNodes = reader._readNodes();
    var editedNodes = originalNodes.ToList();
    var mdat = originalNodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var newNode = new IsoBmffNode(new IsoBmffBox(-1, 8, 24, IsoBmffTypes.Uuid), -1, data: new byte[16]);

    var original = new IsoBmffLayoutPlanner(
      originalNodes,
      originalNodes,
      reader.GetBoxChildrenOffset).CreateOriginalLayout();

    editedNodes.Insert(editedNodes.IndexOf(mdat), newNode);

    var plan = new IsoBmffLayoutPlanner(
      originalNodes,
      editedNodes,
      reader.GetBoxChildrenOffset).CreateEditedLayout(original, editedNodes);

    Assert.IsTrue(plan.RequiresFullRewrite);
    Assert.IsTrue(plan.Layout.TryGetValue(newNode, out _));
    Assert.AreEqual(stream.Length + newNode.Box.Size, plan.Layout.TopLevelBoxes.Last().End);
  }

  [TestMethod]
  public void Layout_GrowingPast32BitSize_PromotesHeaderAndRequiresRewrite() {
    var originalSize = (long)uint.MaxValue - 8;
    var originalNode = new IsoBmffNode(new IsoBmffBox(0, 8, originalSize, IsoBmffTypes.Moov), -1);
    var editedNode = new IsoBmffNode(new IsoBmffBox(0, 8, originalSize + 16, IsoBmffTypes.Moov), -1, originalNode);

    var original = new IsoBmffLayout([new IsoBmffLayoutBox(originalNode, 0, originalSize)]);
    var planner = new IsoBmffLayoutPlanner([originalNode], [editedNode], _ => 0);

    var plan = planner.CreateEditedLayout(original, editedNode);
    var plannedBox = plan.Layout[editedNode];

    Assert.IsTrue(plan.RequiresFullRewrite);
    Assert.AreEqual(16, plannedBox.HeaderSize);
    Assert.AreEqual(originalSize + 24, plannedBox.Size);
  }

  [TestMethod]
  public void Diff_ResizingSizeZeroBox_WritesExplicitSize() {
    using var stream = new MemoryStream();
    Span<byte> header = stackalloc byte[8];
    BinaryPrimitives.WriteUInt32BigEndian(header, 0);
    BinaryPrimitives.WriteUInt32BigEndian(header[4..], IsoBmffTypes.Uuid);
    stream.Write(header);
    stream.Write(new byte[4]);
    stream.Position = 0;

    var reader = new IsoBmffReader(stream);
    var originalBox = reader.ReadBox(stream.Length)
      ?? throw new InvalidDataException("Test box was not found.");

    Assert.AreEqual(12, originalBox.Size);

    var originalNode = new IsoBmffNode(originalBox, -1);
    var editedNode = new IsoBmffNode(
      new IsoBmffBox(originalBox.Offset, originalBox.HeaderSize, 10, originalBox.Type),
      -1,
      originalNode);
    var original = new IsoBmffLayout([
      new IsoBmffLayoutBox(originalNode, originalBox.Offset, originalBox.Size)
    ]);
    var edited = new IsoBmffLayout([new IsoBmffLayoutBox(editedNode, originalBox.Offset, 10)]);

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    Assert.AreEqual(1, diff.Writes.Count);
    Assert.AreEqual<uint>(10, BinaryPrimitives.ReadUInt32BigEndian(diff.Writes[0].Data));
  }

  [TestMethod]
  public void Layout_MetadataEdit_PreservesHierarchyAndValidBoxSizes() {
    using var stream = IsoBmffFileTests._createTestFile(
      beforeFreeSize: 0,
      afterFreeSize: 128,
      withItemList: false);

    var file = new IsoBmffFile(stream);
    file._metadata.SetKeywords("layout-invariants");

    var originalNodes = file._metadata._editor._getOriginalNodes();
    var editedNodes = file._metadata._editor.GetEditedNodes();
    var original = new IsoBmffLayoutPlanner(
      originalNodes,
      originalNodes,
      file._reader.GetBoxChildrenOffset).CreateOriginalLayout();
    var plan = new IsoBmffLayoutPlanner(
      originalNodes,
      editedNodes,
      file._reader.GetBoxChildrenOffset).CreateEditedLayout(original, editedNodes);

    Assert.IsFalse(plan.RequiresFullRewrite);
    _assertValidLayout(plan.Layout, editedNodes, stream.Length);
  }

  private static void _assertValidLayout(IsoBmffLayout layout, IReadOnlyList<IsoBmffNode> nodes, long expectedFileSize) {
    var boxes = layout.Boxes.ToList();

    Assert.IsTrue(boxes.Count > 0);

    foreach (var box in boxes) {
      Assert.IsTrue(box.HeaderSize is 8 or 16, $"Invalid header size for {box.Node.Box.TypeName}.");
      Assert.IsTrue(box.Size >= box.HeaderSize, $"Invalid size for {box.Node.Box.TypeName}.");

      if (box.HeaderSize == 8)
        Assert.IsTrue(box.Size <= uint.MaxValue, $"{box.Node.Box.TypeName} needs an extended-size header.");

      if (box.Node.Box.IsFree)
        Assert.IsTrue(box.Size >= box.HeaderSize, "A free box must be large enough to contain its header.");

      if (box.Node.Parent < 0)
        continue;

      Assert.IsTrue(box.Node.Parent < nodes.Count, $"Invalid parent index for {box.Node.Box.TypeName}.");

      var parentNode = nodes[box.Node.Parent];
      Assert.IsTrue(layout.TryGetValue(parentNode, out var parent), $"Parent box for {box.Node.Box.TypeName} is missing.");

      Assert.IsTrue(box.Offset >= parent!.Offset + parent.HeaderSize, $"{box.Node.Box.TypeName} starts before its parent payload.");
      Assert.IsTrue(box.End <= parent.End, $"{box.Node.Box.TypeName} extends beyond its parent.");
    }

    foreach (var siblings in boxes.GroupBy(x => x.Node.Parent)) {
      var ordered = siblings.OrderBy(x => x.Offset).ToList();

      for (var i = 1; i < ordered.Count; i++)
        Assert.AreEqual(
          ordered[i - 1].End,
          ordered[i].Offset,
          $"Gap or overlap between sibling boxes before {ordered[i].Node.Box.TypeName}.");
    }

    var topLevel = layout.TopLevelBoxes.ToList();

    Assert.AreEqual(0L, topLevel[0].Offset);
    Assert.AreEqual(expectedFileSize, topLevel[^1].End);
  }

  private static void _assertSameLayout(IsoBmffLayout expected, IsoBmffLayout actual) {
    Assert.AreEqual(expected.Boxes.Count(), actual.Boxes.Count());

    foreach (var expectedBox in expected.Boxes) {
      Assert.IsTrue(actual.TryGetValue(expectedBox.Node, out var actualBox));
      Assert.AreEqual(expectedBox.Offset, actualBox!.Offset);
      Assert.AreEqual(expectedBox.Size, actualBox.Size);
    }
  }

  private static void _assertValidTopLevelLayout(IsoBmffLayout layout, long expectedFileSize) {
    var boxes = layout.TopLevelBoxes.ToList();

    Assert.IsTrue(boxes.Count > 0);
    Assert.AreEqual(0L, boxes[0].Offset);

    for (var i = 0; i < boxes.Count; i++) {
      var box = boxes[i];

      Assert.IsTrue(box.Size >= box.Node.Box.HeaderSize,
        $"Invalid size for {box.Node.Box.TypeName}.");

      if (box.Node.Box.IsFree)
        Assert.IsTrue(box.Size >= 8, "A free box must be at least 8 bytes.");

      if (i > 0)
        Assert.AreEqual(boxes[i - 1].End, box.Offset,
          $"Gap or overlap before {box.Node.Box.TypeName}.");
    }

    Assert.AreEqual(expectedFileSize, boxes[^1].End);
  }
}