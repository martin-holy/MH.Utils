using MH.Utils.Imaging.IsoBmff;

namespace MH.Utils.Tests.Imaging.IsoBmff;

[TestClass]
public class IsoBmffLayoutDifferTests {
  [TestMethod]
  public void Diff_UnchangedLayout_HasNoOperations() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = _createPlanner(boxes, reader);

    var original = planner.CreateOriginalLayout();

    var diff = IsoBmffLayoutDiffer.Diff(original, original, []);

    Assert.AreEqual(0, diff.Moves.Count);
    Assert.AreEqual(0, diff.Writes.Count);
  }

  [TestMethod]
  public void Diff_MovedBox_CreatesMove() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = _createPlanner(boxes, reader);

    var original = planner.CreateOriginalLayout();
    var node = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, x.Offset - 16, x.Size)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited, []);

    var move = diff.Moves.Single();

    Assert.AreEqual(original[node].Offset, move.SourceOffset);
    Assert.AreEqual(original[node].Offset - 16, move.DestinationOffset);
    Assert.AreEqual(original[node].Size, move.Length);
    Assert.AreEqual(0, diff.Writes.Count);
  }

  [TestMethod]
  public void Diff_ResizedBox_NotMarkedForWrite_DoesNotCreateWrite() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = _createPlanner(boxes, reader);

    var original = planner.CreateOriginalLayout();
    var node = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, x.Offset, x.Size + 16)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited, []);

    Assert.AreEqual(0, diff.Moves.Count);
    Assert.AreEqual(0, diff.Writes.Count);
  }

  [TestMethod]
  public void Diff_ResizedBox_MarkedForWrite_CreatesWrite() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = _createPlanner(boxes, reader);

    var original = planner.CreateOriginalLayout();
    var node = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var originalBox = original[node];

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, x.Offset, x.Size + 16)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited, [node]);

    Assert.AreEqual(0, diff.Moves.Count);

    var write = diff.Writes.Single();

    Assert.AreEqual(originalBox.Offset, write.Offset);
    Assert.AreEqual(originalBox.Size + 16, write.Length);
  }

  [TestMethod]
  public void Diff_ChangedBoxWithSameSize_CreatesWrite() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = _createPlanner(boxes, reader);

    var original = planner.CreateOriginalLayout();
    var node = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var originalBox = original[node];

    // The layout is unchanged. The explicit write set indicates that
    // the box contents changed without changing its serialized size.
    var edited = new IsoBmffLayout(original.Boxes);

    var diff = IsoBmffLayoutDiffer.Diff(original, edited, [node]);

    Assert.AreEqual(0, diff.Moves.Count);

    var write = diff.Writes.Single();

    Assert.AreEqual(originalBox.Offset, write.Offset);
    Assert.AreEqual(originalBox.Size, write.Length);
  }

  [TestMethod]
  public void Diff_MovedAndResizedBox_CreatesMoveAndWrite() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = _createPlanner(boxes, reader);

    var original = planner.CreateOriginalLayout();
    var node = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var originalBox = original[node];

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, x.Offset - 16, x.Size + 16)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited, [node]);

    var move = diff.Moves.Single();

    Assert.AreEqual(originalBox.Offset, move.SourceOffset);
    Assert.AreEqual(originalBox.Offset - 16, move.DestinationOffset);
    Assert.AreEqual(originalBox.Size, move.Length);

    var write = diff.Writes.Single();

    Assert.AreEqual(originalBox.Offset - 16, write.Offset);
    Assert.AreEqual(originalBox.Size + 16, write.Length);
  }

  [TestMethod]
  public void Diff_NewBox_CreatesWrite() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = _createPlanner(boxes, reader);

    var original = planner.CreateOriginalLayout();
    var newNode = new IsoBmffBoxNode(
      new IsoBmffBox(-1, 8, 32, IsoBmffTypes.Free),
      -1);

    var offset = original.Boxes.Max(x => x.End);
    var edited = new IsoBmffLayout(
      original.Boxes.Append(
        new IsoBmffLayoutBox(newNode, offset, newNode.Box.Size)));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited, []);

    Assert.AreEqual(0, diff.Moves.Count);

    var write = diff.Writes.Single();

    Assert.AreEqual(offset, write.Offset);
    Assert.AreEqual(newNode.Box.Size, write.Length);
  }

  [TestMethod]
  public void Diff_ResizedFreeBox_CreatesWrite() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = _createPlanner(boxes, reader);

    var original = planner.CreateOriginalLayout();
    var node = boxes.First(x => x.Box.Type == IsoBmffTypes.Free);
    var originalBox = original[node];

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, x.Offset, x.Size + 8)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited, []);

    Assert.AreEqual(0, diff.Moves.Count);

    var write = diff.Writes.Single();

    Assert.AreEqual(originalBox.Offset, write.Offset);
    Assert.AreEqual(originalBox.Size + 8, write.Length);
  }

  [TestMethod]
  public void Diff_MovedFreeBox_DoesNotCreateMove_ButCreatesWrite() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = _createPlanner(boxes, reader);

    var original = planner.CreateOriginalLayout();
    var node = boxes.First(x => x.Box.Type == IsoBmffTypes.Free);
    var originalBox = original[node];

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, x.Offset + 16, x.Size)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited, []);

    Assert.AreEqual(0, diff.Moves.Count);

    var write = diff.Writes.Single();

    Assert.AreEqual(originalBox.Offset + 16, write.Offset);
    Assert.AreEqual(originalBox.Size, write.Length);
  }

  [TestMethod]
  public void Diff_MovedContainer_DoesNotCreateMovesForChildren() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = _createPlanner(boxes, reader);

    var original = planner.CreateOriginalLayout();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var moovOriginal = original[moov];

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, moov)
          ? new IsoBmffLayoutBox(
            moov,
            moovOriginal.Offset + 16,
            moovOriginal.Size)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited, []);

    var move = diff.Moves.Single();

    Assert.AreEqual(moovOriginal.Offset, move.SourceOffset);
    Assert.AreEqual(moovOriginal.Offset + 16, move.DestinationOffset);
    Assert.AreEqual(moovOriginal.Size, move.Length);

    Assert.AreEqual(0, diff.Writes.Count);
  }

  [TestMethod]
  public void Diff_PlannerLayout_ReportsActualChanges() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = _createPlanner(boxes, reader);

    var original = planner.CreateOriginalLayout();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var originalMoov = original[moov];

    var originalFree = original.TopLevelBoxes
      .Single(x => x.Node.Box.Type == IsoBmffTypes.Free && x.Offset > originalMoov.Offset);

    moov.Box = new IsoBmffBox(moov.Box.Offset, moov.Box.HeaderSize, moov.Box.Size - 8, moov.Box.Type);

    var plan = planner.CreateEditedLayout(original, moov);

    var diff = IsoBmffLayoutDiffer.Diff(original, plan.Layout, [moov]);

    Assert.AreEqual(0, diff.Moves.Count);
    Assert.AreEqual(2, diff.Writes.Count);

    var moovWrite = diff.Writes.Single(x => x.Offset == plan.Layout[moov].Offset);

    Assert.AreEqual(originalMoov.Offset, moovWrite.Offset);
    Assert.AreEqual(originalMoov.Size - 8, moovWrite.Length);

    var editedFree = plan.Layout.TopLevelBoxes
      .Single(x => x.Node.Box.Type == IsoBmffTypes.Free && x.Offset >= plan.Layout[moov].End);

    var freeWrite = diff.Writes.Single(x => x.Offset == editedFree.Offset);

    Assert.AreEqual(originalFree.Offset - 8, freeWrite.Offset);
    Assert.AreEqual(originalFree.Size + 8, freeWrite.Length);
  }

  private static IsoBmffLayoutPlanner _createPlanner(IEnumerable<IsoBmffBoxNode> boxes, IsoBmffReader reader) =>
    new(boxes, reader.GetBoxChildrenOffset, IsoBmffFileTests.ExpensiveBoxSize);
}