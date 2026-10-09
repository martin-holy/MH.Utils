using MH.Utils.Imaging.IsoBmff;

namespace MH.Utils.Tests.Imaging.IsoBmff;

[TestClass]
public class IsoBmffLayoutDifferTests {
  [TestMethod]
  public void Diff_UnchangedLayout_HasNoOperations() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset, IsoBmffFileTests.ExpensiveBoxSize);

    var original = planner.CreateOriginalLayout();

    var diff = IsoBmffLayoutDiffer.Diff(original, original);

    Assert.AreEqual(0, diff.Moves.Count);
    Assert.AreEqual(0, diff.Writes.Count);
  }

  [TestMethod]
  public void Diff_MovedBox_CreatesMove() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset, IsoBmffFileTests.ExpensiveBoxSize);

    var original = planner.CreateOriginalLayout();

    var node = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, x.Offset - 16, x.Size)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    var move = diff.Moves.Single();

    Assert.AreEqual(node.Box.Offset, move.SourceOffset);
    Assert.AreEqual(node.Box.Offset - 16, move.DestinationOffset);
    Assert.AreEqual(node.Box.Size, move.Length);

    Assert.AreEqual(0, diff.Writes.Count);
  }

  [TestMethod]
  public void Diff_ResizedBox_CreatesWrite() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset, IsoBmffFileTests.ExpensiveBoxSize);

    var original = planner.CreateOriginalLayout();

    var node = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var originalBox = original[node];

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, x.Offset, x.Size + 16)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    Assert.AreEqual(0, diff.Moves.Count);

    var write = diff.Writes.Single();

    Assert.AreEqual(originalBox.Offset, write.Offset);
    Assert.AreEqual(originalBox.Size + 16, write.Length);
  }

  [TestMethod]
  public void Diff_MovedAndResizedBox_CreatesMoveAndWrite() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset, IsoBmffFileTests.ExpensiveBoxSize);

    var original = planner.CreateOriginalLayout();

    var node = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var originalBox = original[node];

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(
            node,
            x.Offset - 16,
            x.Size + 16)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

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
    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset, IsoBmffFileTests.ExpensiveBoxSize);

    var original = planner.CreateOriginalLayout();

    var newNode = new IsoBmffBoxNode(
      new IsoBmffBox(-1, 8, 32, IsoBmffTypes.Free),
      -1);

    var edited = new IsoBmffLayout(
      original.Boxes.Append(
        new IsoBmffLayoutBox(
          newNode,
          original.Boxes.Max(x => x.End),
          newNode.Box.Size)));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    Assert.AreEqual(0, diff.Moves.Count);

    var write = diff.Writes.Single();

    Assert.AreEqual(newNode.Box.Size, write.Length);
    Assert.AreEqual(
      original.Boxes.Max(x => x.End),
      write.Offset);
  }

  [TestMethod]
  public void Diff_MovedFreeBox_DoesNotCreateMove() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset, IsoBmffFileTests.ExpensiveBoxSize);

    var original = planner.CreateOriginalLayout();

    var node = boxes.First(x => x.Box.Type == IsoBmffTypes.Free);
    var originalBox = original[node];

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, x.Offset + 16, x.Size)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    Assert.AreEqual(0, diff.Moves.Count);
    Assert.AreEqual(0, diff.Writes.Count);
  }

  [TestMethod]
  public void Diff_MovedContainer_DoesNotCreateMovesForChildren() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset, IsoBmffFileTests.ExpensiveBoxSize);

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

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    var move = diff.Moves.Single();

    Assert.AreEqual(moovOriginal.Offset, move.SourceOffset);
    Assert.AreEqual(moovOriginal.Offset + 16, move.DestinationOffset);
    Assert.AreEqual(moovOriginal.Size, move.Length);
  }

  [TestMethod]
  public void Diff_PlannerLayout_ReportsActualChanges() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();

    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset, IsoBmffFileTests.ExpensiveBoxSize);

    var original = planner.CreateOriginalLayout();

    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var originalMoov = original[moov];
    var originalFree = original.TopLevelBoxes
      .Single(x => x.Node.Box.Type == IsoBmffTypes.Free && x.Offset > originalMoov.Offset);

    moov.Box = new IsoBmffBox(moov.Box.Offset, moov.Box.HeaderSize, moov.Box.Size - 8, moov.Box.Type);

    var edited = planner.CreateEditedLayout(original, moov);

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    Assert.AreEqual(0, diff.Moves.Count);
    Assert.AreEqual(2, diff.Writes.Count);

    var moovWrite = diff.Writes.Single(x => x.Offset == edited[moov].Offset);

    Assert.AreEqual(originalMoov.Offset, moovWrite.Offset);
    Assert.AreEqual(originalMoov.Size - 8, moovWrite.Length);

    var editedFree = edited.TopLevelBoxes
      .Single(x => x.Node.Box.Type == IsoBmffTypes.Free && x.Offset >= edited[moov].End);

    var freeWrite = diff.Writes.Single(x => x.Offset == editedFree.Offset);

    Assert.AreEqual(originalFree.Offset - 8, freeWrite.Offset);
    Assert.AreEqual(originalFree.Size + 8, freeWrite.Length);
  }
}