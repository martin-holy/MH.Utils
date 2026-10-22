using System.Buffers.Binary;
using MH.Utils.Imaging.IsoBmff;

namespace MH.Utils.Tests.Imaging.IsoBmff;

[TestClass]
public class IsoBmffLayoutDifferTests {
  [TestMethod]
  public void Diff_UnchangedLayout_HasNoOperations() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var planner = _createPlanner(nodes, reader);
    var original = planner.CreateOriginalLayout();

    var diff = IsoBmffLayoutDiffer.Diff(original, original);

    Assert.AreEqual(0, diff.Moves.Count);
    Assert.AreEqual(0, diff.Writes.Count);
  }

  [TestMethod]
  public void Diff_MovedBox_CreatesMoveWithoutWrite() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var planner = _createPlanner(nodes, reader);
    var original = planner.CreateOriginalLayout();
    var node = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, x.Offset - 16, x.Size)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    var move = diff.Moves.Single();

    Assert.AreEqual(original[node].Offset, move.SourceOffset);
    Assert.AreEqual(original[node].Offset - 16, move.DestinationOffset);
    Assert.AreEqual(original[node].Size, move.Length);
    Assert.AreEqual(0, diff.Writes.Count);
  }

  [TestMethod]
  public void Diff_ResizedBox_WritesUpdatedHeaderAtPlannedOffset() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var planner = _createPlanner(nodes, reader);
    var original = planner.CreateOriginalLayout();
    var node = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var newSize = original[node].Size + 16;

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, x.Offset, newSize)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    Assert.AreEqual(0, diff.Moves.Count);
    _assertHeaderWrite(diff, edited, node);
    Assert.AreEqual(newSize, BinaryPrimitives.ReadUInt32BigEndian(diff.Writes.Single().Data));
  }

  [TestMethod]
  public void Diff_ChangedPayloadWithSameSize_WritesPayloadBytes() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var original = _createPlanner(nodes, reader).CreateOriginalLayout();
    var originalNode = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var originalBox = original[originalNode];
    var payload = Enumerable
      .Repeat((byte)0xA5, checked((int)(originalBox.Size - originalNode.Box.HeaderSize)))
      .ToArray();
    var editedNode = new IsoBmffNode(originalNode.Box, originalNode.Parent, originalNode, payload);
    var edited = new IsoBmffLayout(original.Boxes);

    var diff = IsoBmffLayoutDiffer.Diff(original, edited, [editedNode]);

    var write = diff.Writes.Single();

    Assert.AreEqual(0, diff.Moves.Count);
    Assert.AreEqual(originalBox.Offset + originalNode.Box.HeaderSize, write.Offset);
    CollectionAssert.AreEqual(payload, write.Data);
  }

  [TestMethod]
  public void Diff_MovedAndResizedBox_WritesHeaderAtDestination() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var original = _createPlanner(nodes, reader).CreateOriginalLayout();
    var node = nodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var oldBox = original[node];
    var newOffset = oldBox.Offset - 16;
    var newSize = oldBox.Size + 16;

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, newOffset, newSize)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    var move = diff.Moves.Single();

    Assert.AreEqual(oldBox.Offset, move.SourceOffset);
    Assert.AreEqual(newOffset, move.DestinationOffset);
    Assert.AreEqual(oldBox.Size, move.Length);
    _assertHeaderWrite(diff, edited, node);
    Assert.AreEqual(newSize, BinaryPrimitives.ReadUInt32BigEndian(diff.Writes.Single().Data));
  }

  [TestMethod]
  public void Diff_NewBox_WritesHeaderAndPayload() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var original = _createPlanner(nodes, reader).CreateOriginalLayout();
    var payload = Enumerable.Repeat((byte)0x5A, 24).ToArray();
    var newNode = new IsoBmffNode(new IsoBmffBox(-1, 8, 32, IsoBmffTypes.Free), -1, data: payload);
    var offset = original.Boxes.Max(x => x.End);
    var edited = new IsoBmffLayout(
      original.Boxes.Append(new IsoBmffLayoutBox(newNode, offset, newNode.Box.Size)));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    Assert.AreEqual(0, diff.Moves.Count);

    var headerWrite = diff.Writes.Single(x => x.Offset == offset);
    var payloadWrite = diff.Writes.Single(x => x.Offset == offset + newNode.Box.HeaderSize);

    Assert.AreEqual(32U, BinaryPrimitives.ReadUInt32BigEndian(headerWrite.Data));
    Assert.AreEqual(IsoBmffTypes.Free, BinaryPrimitives.ReadUInt32BigEndian(headerWrite.Data.AsSpan(4)));
    CollectionAssert.AreEqual(payload, payloadWrite.Data);
  }

  [TestMethod]
  public void Diff_ResizedFreeBox_WritesHeader() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var original = _createPlanner(nodes, reader).CreateOriginalLayout();
    var node = nodes.First(x => x.Box.IsFree);
    var newSize = original[node].Size + 8;

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, x.Offset, newSize)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    Assert.AreEqual(0, diff.Moves.Count);
    _assertHeaderWrite(diff, edited, node);
    Assert.AreEqual(newSize, BinaryPrimitives.ReadUInt32BigEndian(diff.Writes.Single().Data));
  }

  [TestMethod]
  public void Diff_MovedFreeBox_WritesHeaderWithoutMove() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var original = _createPlanner(nodes, reader).CreateOriginalLayout();
    var node = nodes.First(x => x.Box.IsFree);
    var newOffset = original[node].Offset + 16;

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, node)
          ? new IsoBmffLayoutBox(node, newOffset, x.Size)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    Assert.AreEqual(0, diff.Moves.Count);
    _assertHeaderWrite(diff, edited, node);
    Assert.AreEqual(newOffset, diff.Writes.Single().Offset);
  }

  [TestMethod]
  public void Diff_MovedContainer_CreatesMoveWithoutChildMoves() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var original = _createPlanner(nodes, reader).CreateOriginalLayout();
    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var oldMoov = original[moov];
    var newOffset = oldMoov.Offset + 16;

    var edited = new IsoBmffLayout(
      original.Boxes.Select(x =>
        ReferenceEquals(x.Node, moov)
          ? new IsoBmffLayoutBox(moov, newOffset, oldMoov.Size)
          : x));

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    var move = diff.Moves.Single();

    Assert.AreEqual(oldMoov.Offset, move.SourceOffset);
    Assert.AreEqual(newOffset, move.DestinationOffset);
    Assert.AreEqual(oldMoov.Size, move.Length);
    Assert.AreEqual(0, diff.Writes.Count);
  }

  [TestMethod]
  public void Diff_PlannerLayout_WritesChangedBoxHeaders() {
    using var stream = IsoBmffFileTests._createTestFile();
    var reader = new IsoBmffReader(stream);
    var nodes = reader._readNodes();
    var planner = _createPlanner(nodes, reader);
    var original = planner.CreateOriginalLayout();
    var moov = nodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var originalMoov = original[moov];
    var originalFree = original.TopLevelBoxes
      .Single(x => x.Node.Box.IsFree && x.Offset > originalMoov.Offset);

    IsoBmffFileTests._resize(moov, moov.Box.Size - 8);

    var plan = planner.CreateEditedLayout(original, moov);
    var diff = IsoBmffLayoutDiffer.Diff(original, plan.Layout, [moov]);

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.AreEqual(0, diff.Moves.Count);

    var moovWrite = diff.Writes.Single(x => x.Offset == plan.Layout[moov].Offset);
    Assert.AreEqual(moov.Box.Size, BinaryPrimitives.ReadUInt32BigEndian(moovWrite.Data));

    var editedFree = plan.Layout.TopLevelBoxes
      .Single(x => x.Node.Box.IsFree && x.Offset >= plan.Layout[moov].End);
    var freeWrite = diff.Writes.Single(x => x.Offset == editedFree.Offset);

    Assert.AreEqual(editedFree.Size, BinaryPrimitives.ReadUInt32BigEndian(freeWrite.Data));
    Assert.AreEqual(originalFree.Size + 8, editedFree.Size);
  }

  [TestMethod]
  public void Diff_MetadataEdit_WritesHeadersAndPreparedPayloads() {
    using var stream = IsoBmffFileTests._createTestFile(0, 128, withItemList: false);

    var file = new IsoBmffFile(stream);
    var originalNodes = file._metadata._editor._getOriginalNodes();
    var originalPlanner = new IsoBmffLayoutPlanner(originalNodes, originalNodes, file._reader.GetBoxChildrenOffset);
    var original = originalPlanner.CreateOriginalLayout();

    file._metadata.SetItemListKeywords("hello");

    var editedNodes = file._metadata._editor.GetEditedNodes();
    var planner = new IsoBmffLayoutPlanner(originalNodes, editedNodes, file._reader.GetBoxChildrenOffset);
    var plan = planner.CreateEditedLayout(original, editedNodes);
    var diff = IsoBmffLayoutDiffer.Diff(
      original,
      plan.Layout,
      editedNodes.Where(x => x.Data is not null));

    Assert.IsFalse(plan.RequiresFullRewrite);

    var moov = editedNodes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var meta = editedNodes.Single(x => x.Box.Type == IsoBmffTypes.Meta);
    var keyw = editedNodes.Single(x => x.Box.Type == IsoBmffTypes.Keyw);
    var data = editedNodes.Single(x => x.Box.Type == IsoBmffTypes.Data);

    _assertHeaderWrite(diff, plan.Layout, moov);
    _assertHeaderWrite(diff, plan.Layout, meta);
    _assertHeaderWrite(diff, plan.Layout, keyw);
    _assertHeaderWrite(diff, plan.Layout, data);

    var metaPayloadWrite = diff.Writes.Single(
      x => x.Offset == plan.Layout[meta].Offset + meta.Box.HeaderSize);
    var dataPayloadWrite = diff.Writes.Single(
      x => x.Offset == plan.Layout[data].Offset + data.Box.HeaderSize);

    CollectionAssert.AreEqual(meta.Data, metaPayloadWrite.Data);
    CollectionAssert.AreEqual(data.Data, dataPayloadWrite.Data);
  }

  [TestMethod]
  public void Diff_ExtendedHeader_WritesExtendedSizeEncoding() {
    var node = new IsoBmffNode(new IsoBmffBox(0, 16, 24, IsoBmffTypes.Uuid), -1);
    var original = new IsoBmffLayout([new IsoBmffLayoutBox(node, 0, 24)]);
    var edited = new IsoBmffLayout([new IsoBmffLayoutBox(node, 0, 32)]);

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    var write = diff.Writes.Single();

    Assert.AreEqual(0L, write.Offset);
    Assert.AreEqual(16, write.Data.Length);
    Assert.AreEqual(1U, BinaryPrimitives.ReadUInt32BigEndian(write.Data));
    Assert.AreEqual(IsoBmffTypes.Uuid, BinaryPrimitives.ReadUInt32BigEndian(write.Data.AsSpan(4)));
    Assert.AreEqual(32UL, BinaryPrimitives.ReadUInt64BigEndian(write.Data.AsSpan(8)));
  }

  [TestMethod]
  public void Diff_IncludesMovesForShiftedNestedBoxes() {
    var originalMoov = new IsoBmffNode(new IsoBmffBox(0, 8, 100, IsoBmffTypes.Moov), -1);
    var originalMeta = new IsoBmffNode(new IsoBmffBox(8, 8, 40, IsoBmffTypes.Meta), 0);
    var originalTrak = new IsoBmffNode(new IsoBmffBox(48, 8, 52, IsoBmffTypes.Trak), 0);
    var originalFree = new IsoBmffNode(new IsoBmffBox(100, 8, 100, IsoBmffTypes.Free), -1);

    var original = new IsoBmffLayout([
      new IsoBmffLayoutBox(originalMoov, 0, 100),
      new IsoBmffLayoutBox(originalMeta, 8, 40),
      new IsoBmffLayoutBox(originalTrak, 48, 52),
      new IsoBmffLayoutBox(originalFree, 100, 100)
    ]);

    var editedMoov = new IsoBmffNode(new IsoBmffBox(0, 8, 108, IsoBmffTypes.Moov), -1, originalMoov);
    var editedMeta = new IsoBmffNode(new IsoBmffBox(8, 8, 48, IsoBmffTypes.Meta), 0, originalMeta);
    var editedTrak = new IsoBmffNode(new IsoBmffBox(56, 8, 52, IsoBmffTypes.Trak), 0, originalTrak);
    var editedFree = new IsoBmffNode(new IsoBmffBox(108, 8, 92, IsoBmffTypes.Free), -1, originalFree);

    var edited = new IsoBmffLayout([
      new IsoBmffLayoutBox(editedMoov, 0, 108),
      new IsoBmffLayoutBox(editedMeta, 8, 48),
      new IsoBmffLayoutBox(editedTrak, 56, 52),
      new IsoBmffLayoutBox(editedFree, 108, 92)
    ]);

    var diff = IsoBmffLayoutDiffer.Diff(original, edited);

    Assert.AreEqual(1, diff.Moves.Count);
    Assert.AreEqual(48L, diff.Moves[0].SourceOffset);
    Assert.AreEqual(56L, diff.Moves[0].DestinationOffset);
    Assert.AreEqual(52L, diff.Moves[0].Length);
  }

  private static void _assertHeaderWrite(IsoBmffLayoutDiff diff, IsoBmffLayout layout, IsoBmffNode node) {
    var box = layout[node];
    var write = diff.Writes.Single(x =>
      x.Offset == box.Offset &&
      x.Data.Length == node.Box.HeaderSize);

    Assert.AreEqual(node.Box.Type, BinaryPrimitives.ReadUInt32BigEndian(write.Data.AsSpan(4)));

    if (node.Box.HeaderSize == 8)
      Assert.AreEqual(box.Size, BinaryPrimitives.ReadUInt32BigEndian(write.Data));
  }

  private static IsoBmffLayoutPlanner _createPlanner(IEnumerable<IsoBmffNode> boxes, IsoBmffReader reader) =>
    new(boxes, reader.GetBoxChildrenOffset, IsoBmffFileTests.ExpensiveBoxSize);
}