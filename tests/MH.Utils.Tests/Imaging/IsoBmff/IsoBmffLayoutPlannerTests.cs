using MH.Utils.Imaging.IsoBmff;

namespace MH.Utils.Tests.Imaging.IsoBmff;

[TestClass]
public class IsoBmffLayoutPlannerTests {
  private const long ExpensiveBoxSize = 2 * 1024 * 1024;

  [TestMethod]
  public void Layout_NoChange_KeepsOriginalLayout() {
    using var stream = IsoBmffFileTests._createTestFile(32, 32);

    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);

    var original = planner.CreateOriginalLayout();
    var edited = planner.CreateEditedLayout(
      original,
      boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov));

    _assertSameLayout(original, edited);
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

    var edited = planner.CreateEditedLayout(original, moov);

    Assert.AreEqual(originalSize + 24, edited[moov].Size);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
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

    var edited = planner.CreateEditedLayout(original, moov);

    Assert.AreEqual(original[moov].Offset - 24, edited[moov].Offset);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
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

    var edited = planner.CreateEditedLayout(original, moov);

    Assert.AreEqual(originalSize + 24, edited[moov].Size);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
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

    var edited = planner.CreateEditedLayout(original, moov);

    Assert.AreEqual(originalSize - 24, edited[moov].Size);
    Assert.AreEqual(original[free].Size + 24, edited[free].Size);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
  }

  [TestMethod]
  public void Layout_ShrinkWithLaterFree_MovesSmallBoxesLeft() {
    using var stream = IsoBmffFileTests._createTestFile(0, 32);

    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    // We need an additional small box between moov and free.
    var uuid = new IsoBmffBoxNode(new IsoBmffBox(moov.Box.End, 8, 20, IsoBmffTypes.Uuid), -1);

    // Insert it into the fixture hierarchy.
    var uuidIndex = boxes.IndexOf(moov) + 1;
    boxes.Insert(uuidIndex, uuid);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();

    var originalSize = moov.Box.Size;
    IsoBmffFileTests._resize(moov, originalSize - 6);

    var edited = planner.CreateEditedLayout(original, moov);

    Assert.AreEqual(original[uuid].Offset - 6, edited[uuid].Offset);
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

    var originalSize = moov.Box.Size;
    IsoBmffFileTests._resize(moov, originalSize - 3);

    var edited = planner.CreateEditedLayout(original, moov);

    Assert.AreEqual(original[free].Size + 3, edited[free].Size);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
  }

  [TestMethod]
  public void Layout_ShrinkWithoutFree_CreatesFree() {
    using var stream = IsoBmffFileTests._createTestFile(0, 0);

    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();

    var originalSize = moov.Box.Size;
    IsoBmffFileTests._resize(moov, originalSize - 16);

    var edited = planner.CreateEditedLayout(original, moov);

    var free = edited.TopLevelBoxes.Single(x => x.Node.Box.Type == IsoBmffTypes.Free);

    Assert.AreEqual(16, free.Size);
    Assert.AreEqual(edited[moov].End, free.Offset);
  }

  [TestMethod]
  public void Layout_LargeBox_IsKeptFixed() {
    using var stream = IsoBmffFileTests._createTestFile(0, 0);

    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();

    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    mdat.Box = new IsoBmffBox(mdat.Box.Offset, mdat.Box.HeaderSize, ExpensiveBoxSize, mdat.Box.Type);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset, ExpensiveBoxSize);

    var original = planner.CreateOriginalLayout();

    IsoBmffFileTests._resize(moov, moov.Box.Size + 16);

    Assert.ThrowsException<InvalidOperationException>(
      () => planner.CreateEditedLayout(original, moov));
  }

  [TestMethod]
  public void Layout_LargeNonMdatBox_IsAlsoExpensive() {
    using var stream = IsoBmffFileTests._createTestFile(0, 0);

    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();

    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var large = new IsoBmffBoxNode(new IsoBmffBox(moov.Box.End, 8, ExpensiveBoxSize, IsoBmffTypes.Uuid), -1);

    boxes.Insert(boxes.IndexOf(moov) + 1, large);

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset, ExpensiveBoxSize);

    var original = planner.CreateOriginalLayout();

    IsoBmffFileTests._resize(moov, moov.Box.Size + 16);

    Assert.ThrowsException<InvalidOperationException>(
      () => planner.CreateEditedLayout(original, moov));
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

    var edited = planner.CreateEditedLayout(original, [moov, uuid]);

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

    var edited = planner.CreateEditedLayout(original, [moov, uuid]);

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

    var edited = planner.CreateEditedLayout(original, [moov, uuid]);

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

    var edited = planner.CreateEditedLayout(original, [moov, uuid]);

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

    var edited = planner.CreateEditedLayout(original, [uuid, moov]);

    Assert.AreEqual(original[moov].Size + 8, edited[moov].Size);
    Assert.AreEqual(original[uuid].Size + 8, edited[uuid].Size);
    Assert.AreEqual(original[mdat].Offset, edited[mdat].Offset);
  }

  private static void _assertSameLayout(IsoBmffLayout expected, IsoBmffLayout actual) {
    Assert.AreEqual(expected.Boxes.Count(), actual.Boxes.Count());

    foreach (var expectedBox in expected.Boxes) {
      Assert.IsTrue(actual.TryGetValue(expectedBox.Node, out var actualBox));
      Assert.AreEqual(expectedBox.Offset, actualBox!.Offset);
      Assert.AreEqual(expectedBox.Size, actualBox.Size);
    }
  }
}