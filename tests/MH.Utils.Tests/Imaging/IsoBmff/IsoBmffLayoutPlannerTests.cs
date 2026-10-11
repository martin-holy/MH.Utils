using MH.Utils.Imaging.IsoBmff;

namespace MH.Utils.Tests.Imaging.IsoBmff;

[TestClass]
public class IsoBmffLayoutPlannerTests {
  [TestMethod]
  public void Layout_NoChange_KeepsOriginalLayout() {
    using var stream = IsoBmffFileTests._createTestFile(32, 32);

    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);

    var original = planner.CreateOriginalLayout();
    var plan = planner.CreateEditedLayout(original, boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov));

    Assert.IsFalse(plan.RequiresFullRewrite);
    _assertSameLayout(original, plan.Layout);
    _assertValidTopLevelLayout(plan.Layout, stream.Length);
  }

  [TestMethod]
  public void Layout_GrowIntoFreeAfter_KeepsMdatFixed() {
    using var stream = IsoBmffFileTests._createTestFile(0, 64);

    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var freeBefore = boxes.Single(x => x.Box.Type == IsoBmffTypes.Free && x.Box.End <= moov.Box.Offset);
    var freeAfter = boxes.Single(x => x.Box.Type == IsoBmffTypes.Free && x.Box.Offset >= moov.Box.End);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var free = boxes.Single(x => x.Box.Type == IsoBmffTypes.Free);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var uuid = new IsoBmffBoxNode(new IsoBmffBox(moov.Box.End, 8, 20, IsoBmffTypes.Uuid), -1);

    boxes.Insert(boxes.IndexOf(moov) + 1, uuid);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var free = boxes.Single(x => x.Box.Type == IsoBmffTypes.Free);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();

    IsoBmffFileTests._resize(moov, moov.Box.Size - 16);

    var plan = planner.CreateEditedLayout(original, moov);
    var edited = plan.Layout;

    Assert.IsFalse(plan.RequiresFullRewrite);

    var free = edited.TopLevelBoxes.Single(x => x.Node.Box.Type == IsoBmffTypes.Free);

    Assert.AreEqual(16, free.Size);
    Assert.AreEqual(edited[moov].End, free.Offset);
    _assertValidTopLevelLayout(edited, stream.Length);
  }

  [TestMethod]
  public void Layout_LargeBox_RequiresFullRewrite() {
    using var stream = IsoBmffFileTests._createTestFile(0, 0);

    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();

    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    mdat.Box = new IsoBmffBox(
      mdat.Box.Offset,
      mdat.Box.HeaderSize,
      IsoBmffFileTests.ExpensiveBoxSize,
      mdat.Box.Type);

    var planner = new IsoBmffLayoutPlanner(
      boxes,
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
    var boxes = reader._readBoxes();

    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var large = new IsoBmffBoxNode(
      new IsoBmffBox(moov.Box.End, 8, IsoBmffFileTests.ExpensiveBoxSize, IsoBmffTypes.Uuid),
      -1);

    boxes.Insert(boxes.IndexOf(moov) + 1, large);

    var planner = new IsoBmffLayoutPlanner(
      boxes,
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
    var boxes = reader._readBoxes();

    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var free = boxes.Single(x => x.Box.Type == IsoBmffTypes.Free && x.Box.Offset >= moov.Box.End);

    var uuid = new IsoBmffBoxNode(new IsoBmffBox(moov.Box.End, 8, 20, IsoBmffTypes.Uuid), -1);

    boxes.Insert(boxes.IndexOf(moov) + 1, uuid);

    var original = new IsoBmffLayout(boxes.Select(x => new IsoBmffLayoutBox(x, x.Box.Offset, x.Box.Size)));

    moov.Box = new IsoBmffBox(moov.Box.Offset, moov.Box.HeaderSize, moov.Box.Size + 8, moov.Box.Type);
    uuid.Box = new IsoBmffBox(uuid.Box.Offset, uuid.Box.HeaderSize, uuid.Box.Size + 8, uuid.Box.Type);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var boxes = reader._readBoxes();

    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var uuid = new IsoBmffBoxNode(new IsoBmffBox(moov.Box.End, 8, 20, IsoBmffTypes.Uuid), -1);

    boxes.Insert(boxes.IndexOf(moov) + 1, uuid);

    var original = new IsoBmffLayout(boxes.Select(x => new IsoBmffLayoutBox(x, x.Box.Offset, x.Box.Size)));

    var originalUuidOffset = uuid.Box.Offset;

    moov.Box = new IsoBmffBox(moov.Box.Offset, moov.Box.HeaderSize, moov.Box.Size + 8, moov.Box.Type);
    uuid.Box = new IsoBmffBox(uuid.Box.Offset, uuid.Box.HeaderSize, uuid.Box.Size + 8, uuid.Box.Type);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var boxes = reader._readBoxes();

    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var free = boxes.Single(x => x.Box.Type == IsoBmffTypes.Free && x.Box.Offset >= moov.Box.End);

    var uuid = new IsoBmffBoxNode(new IsoBmffBox(moov.Box.End, 8, 20, IsoBmffTypes.Uuid), -1);

    boxes.Insert(boxes.IndexOf(moov) + 1, uuid);

    var original = new IsoBmffLayout(boxes.Select(x => new IsoBmffLayoutBox(x, x.Box.Offset, x.Box.Size)));

    moov.Box = new IsoBmffBox(moov.Box.Offset, moov.Box.HeaderSize, moov.Box.Size + 12, moov.Box.Type);
    uuid.Box = new IsoBmffBox(uuid.Box.Offset, uuid.Box.HeaderSize, uuid.Box.Size - 12, uuid.Box.Type);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var boxes = reader._readBoxes();

    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var free = boxes.Single(x => x.Box.Type == IsoBmffTypes.Free && x.Box.Offset >= moov.Box.End);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var uuid = new IsoBmffBoxNode(new IsoBmffBox(moov.Box.End, 8, 20, IsoBmffTypes.Uuid), -1);

    boxes.Insert(boxes.IndexOf(moov) + 1, uuid);

    var original = new IsoBmffLayout(boxes.Select(x => new IsoBmffLayoutBox(x, x.Box.Offset, x.Box.Size)));

    moov.Box = new IsoBmffBox(moov.Box.Offset, moov.Box.HeaderSize, moov.Box.Size - 8, moov.Box.Type);
    uuid.Box = new IsoBmffBox(uuid.Box.Offset, uuid.Box.HeaderSize, uuid.Box.Size - 8, uuid.Box.Type);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var boxes = reader._readBoxes();

    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var uuid = new IsoBmffBoxNode(new IsoBmffBox(moov.Box.End, 8, 20, IsoBmffTypes.Uuid), -1);

    boxes.Insert(boxes.IndexOf(moov) + 1, uuid);

    var original = new IsoBmffLayout(boxes.Select(x => new IsoBmffLayoutBox(x, x.Box.Offset, x.Box.Size)));

    moov.Box = new IsoBmffBox(moov.Box.Offset, moov.Box.HeaderSize, moov.Box.Size + 8, moov.Box.Type);
    uuid.Box = new IsoBmffBox(uuid.Box.Offset, uuid.Box.HeaderSize, uuid.Box.Size + 8, uuid.Box.Type);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
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
    var originalBoxes = file._metadata._getOriginalBoxes();
    var originalPlanner = new IsoBmffLayoutPlanner(originalBoxes, originalBoxes, file._reader.GetBoxChildrenOffset);
    var original = originalPlanner.CreateOriginalLayout();

    IsoBmffLayoutPlan CreatePlan() {
      var editedBoxes = file._metadata._getEditedBoxes();
      var planner = new IsoBmffLayoutPlanner(originalBoxes, editedBoxes, file._reader.GetBoxChildrenOffset);

      return planner.CreateEditedLayout(original, editedBoxes);
    }

    file._metadata.SetKeywords("hello");

    var editedBoxes = file._metadata._getEditedBoxes();
    var keyw = editedBoxes.Single(x => x.Box.Type == IsoBmffTypes.Keyw);
    var data = editedBoxes.Single(x => x.Box.Type == IsoBmffTypes.Data);
    var meta = editedBoxes.Single(x => x.Box.Type == IsoBmffTypes.Meta);
    var ilst = editedBoxes.Single(x => x.Box.Type == IsoBmffTypes.Ilst);

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

      if (box.Node.Box.Type == IsoBmffTypes.Free)
        Assert.IsTrue(box.Size >= 8, "A free box must be at least 8 bytes.");

      if (i > 0)
        Assert.AreEqual(boxes[i - 1].End, box.Offset,
          $"Gap or overlap before {box.Node.Box.TypeName}.");
    }

    Assert.AreEqual(expectedFileSize, boxes[^1].End);
  }
}