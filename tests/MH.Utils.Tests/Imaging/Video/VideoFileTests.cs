using MH.Utils.Imaging.IsoBmff;

namespace MH.Utils.Tests.Imaging.Video;

[TestClass]
public class VideoFileTests {
  [TestMethod]
  public void Layout_GrowIntoFreeAfter() {
    using var stream = _createTestFile(0, 64);
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var original = _snapshot(boxes);
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var edit = new IsoBmffBoxEdit(moov, moov.Box.Size + 24);
    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
    var originalLayout = planner.CreateOriginalLayout();
    var editedLayout = planner.CreateEditedLayout(originalLayout, edit);
    var diff = IsoBmffLayoutDiffer.Diff(originalLayout, editedLayout, edit);
    var after = _snapshot(reader._readBoxes());

    //Assert.AreEqual(0, diff.Moves.Count);
    Assert.AreEqual(1, diff.Writes.Count);
    Assert.AreEqual(moov.Box.Offset, editedLayout[moov].Offset);
    Assert.AreEqual(moov.Box.Size + 24, editedLayout[moov].Size);

    foreach (var box in boxes.Where(x => x.Parent >= 0)) {
      Assert.AreEqual(
        originalLayout[box].Offset,
        editedLayout[box].Offset,
        $"Box {box.Box.Type:X8} moved.");
    }

    CollectionAssert.AreEqual(original, after);
  }

  [TestMethod]
  public void Layout_GrowIntoFreeBefore() {
    using var stream = _createTestFile(64, 0);
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var originalLayout = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset).CreateOriginalLayout();
    var edit = new IsoBmffBoxEdit(moov, moov.Box.Size + 24);
    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
    var editedLayout = planner.CreateEditedLayout(originalLayout, edit);
    var diff = IsoBmffLayoutDiffer.Diff(originalLayout, editedLayout, edit);

    //Assert.AreEqual(1, diff.Moves.Count);

    var move = diff.Moves[0];

    Assert.AreEqual(moov.Box.Offset, move.SourceOffset);
    Assert.AreEqual(moov.Box.Offset - 24, move.DestinationOffset);
    Assert.AreEqual(moov.Box.Size, move.Length);
    Assert.IsTrue(move.DestinationOffset < move.SourceOffset);
  }

  [TestMethod]
  public void Layout_GrowUsingBothFreeRegions() {
    using var stream = _createTestFile(16, 16);
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();
    var edit = new IsoBmffBoxEdit(moov, moov.Box.Size + 24);
    var edited = planner.CreateEditedLayout(original, edit);

    Assert.AreEqual(moov.Box.Offset - 8, edited[moov].Offset);
    Assert.AreEqual(moov.Box.Size + 24, edited[moov].Size);
  }

  [TestMethod]
  public void Layout_InsufficientFree_RequiresRewrite() {
    using var stream = _createTestFile(8, 8);
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var moov = boxes.Single(x => x.Box.Type == IsoBmffTypes.Moov);
    var planner = new IsoBmffLayoutPlanner(boxes, reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();
    var edit = new IsoBmffBoxEdit(moov, moov.Box.Size + 24);

    Assert.ThrowsException<InvalidOperationException>(() => planner.CreateEditedLayout(original, edit));
  }

  [TestMethod]
  public void MoveUp_NonOverlapping() {
    var data = _createData(100);
    var original = data.ToArray();
    using var stream = new MemoryStream(data);
    var move = new IsoBmffMove(60, 20, 20);
    IsoBmffLayoutExecutor.ExecuteMoves(stream, [move]);
    _assertMoved(data, original, 60, 20, 20);
  }

  [TestMethod]
  public void MoveUp_Overlapping() {
    var data = _createData(100);
    var original = data.ToArray();
    using var stream = new MemoryStream(data);
    var move = new IsoBmffMove(20, 0, 60);
    IsoBmffLayoutExecutor.ExecuteMoves(stream, [move]);
    _assertMoved(data, original, 20, 0, 60);
  }

  [TestMethod]
  public void MoveDown_NonOverlapping() {
    var data = _createData(100);
    var original = data.ToArray();
    using var stream = new MemoryStream(data);
    var move = new IsoBmffMove(20, 60, 20);
    IsoBmffLayoutExecutor.ExecuteMoves(stream, [move]);
    _assertMoved(data, original, 20, 60, 20);
  }

  [TestMethod]
  public void MoveDown_Overlapping() {
    var data = _createData(100);
    var original = data.ToArray();
    using var stream = new MemoryStream(data);
    var move = new IsoBmffMove(0, 20, 60);
    IsoBmffLayoutExecutor.ExecuteMoves(stream, [move]);
    _assertMoved(data, original, 0, 20, 60);
  }

  [TestMethod]
  public void MoveUp_LargerThanBuffer() {
    const int length = 64 * 1024 + 123;
    const int source = 100;
    const int destination = 0;

    var data = _createData(source + length);
    var original = data.ToArray();
    using var stream = new MemoryStream(data);
    var move = new IsoBmffMove(source, destination, length);
    IsoBmffLayoutExecutor.ExecuteMoves(stream, [move]);
    _assertMoved(data, original, source, destination, length);
  }

  [TestMethod]
  public void MoveDown_LargerThanBuffer() {
    const int length = 64 * 1024 + 123;
    const int source = 0;
    const int destination = 100;

    var data = _createData(destination + length);
    var original = data.ToArray();
    using var stream = new MemoryStream(data);
    var move = new IsoBmffMove(source, destination, length);
    IsoBmffLayoutExecutor.ExecuteMoves(stream, [move]);
    _assertMoved(data, original, source, destination, length);
  }

  [TestMethod]
  public void Move_ZeroLength_DoesNothing() {
    var data = _createData(100);
    var original = data.ToArray();
    using var stream = new MemoryStream(data);
    var move = new IsoBmffMove(20, 60, 0);
    IsoBmffLayoutExecutor.ExecuteMoves(stream, [move]);
    CollectionAssert.AreEqual(original, data);
  }

  [TestMethod]
  public void Move_SameOffset_DoesNothing() {
    var data = _createData(100);
    var original = data.ToArray();
    using var stream = new MemoryStream(data);
    var move = new IsoBmffMove(20, 20, 40);
    IsoBmffLayoutExecutor.ExecuteMoves(stream, [move]);
    CollectionAssert.AreEqual(original, data);
  }

  [TestMethod]
  public void ExecuteMoves_ExecutesMultipleMoves() {
    var data = _createData(100);
    var original = data.ToArray();
    using var stream = new MemoryStream(data);

    var moves = new[] {
      new IsoBmffMove(0, 20, 20),
      new IsoBmffMove(60, 40, 20)
    };

    IsoBmffLayoutExecutor.ExecuteMoves(stream, moves);

    _assertMoved(data, original, 0, 20, 20);
    _assertMoved(data, original, 60, 40, 20);
  }

  [TestMethod]
  public void ItemList_CreateStructure() {
    using var stream = _createTestFile(withItemList: false);

    var file = new IsoBmffFile(stream);

    var boxes = file._metadata._getEditedBoxes();

    var moovIndex = boxes.FindIndex(IsoBmffTypes.Moov);
    Assert.IsTrue(moovIndex >= 0);

    var originalMoovSize = boxes[moovIndex].Box.Size;

    var itemList = file._metadata._getOrCreateItemList();

    Assert.IsNotNull(itemList);

    boxes = file._metadata._getEditedBoxes();

    var udtaIndex = boxes.FindIndex(IsoBmffTypes.Udta, moovIndex);
    var metaIndex = boxes.FindIndex(IsoBmffTypes.Meta, udtaIndex);
    var ilstIndex = boxes.FindIndex(IsoBmffTypes.Ilst, metaIndex);

    Assert.IsTrue(udtaIndex >= 0);
    Assert.IsTrue(metaIndex >= 0);
    Assert.IsTrue(ilstIndex >= 0);

    Assert.AreEqual(moovIndex, boxes[udtaIndex].Parent);
    Assert.AreEqual(udtaIndex, boxes[metaIndex].Parent);
    Assert.AreEqual(metaIndex, boxes[ilstIndex].Parent);

    Assert.AreEqual(-1, boxes[udtaIndex].Box.Offset);
    Assert.AreEqual(-1, boxes[metaIndex].Box.Offset);
    Assert.AreEqual(-1, boxes[ilstIndex].Box.Offset);

    Assert.AreEqual(28, boxes[udtaIndex].Box.Size);
    Assert.AreEqual(20, boxes[metaIndex].Box.Size);
    Assert.AreEqual(8, boxes[ilstIndex].Box.Size);

    Assert.AreEqual(originalMoovSize + 28, boxes[moovIndex].Box.Size);

    var secondItemList = file._metadata._getOrCreateItemList();

    Assert.AreSame(itemList, secondItemList);
    Assert.AreEqual(boxes.Count, file._metadata._getEditedBoxes().Count);
    Assert.AreEqual(originalMoovSize + 28, boxes[moovIndex].Box.Size);
  }

  private static void _assertMoved(byte[] actual, byte[] original, int source, int destination, int length) {
    for (var i = 0; i < length; i++) {
      Assert.AreEqual(
        original[source + i],
        actual[destination + i],
        $"Byte at destination {destination + i} is incorrect.");
    }
  }

  private static byte[] _createData(int length) {
    var data = new byte[length];

    for (var i = 0; i < length; i++)
      data[i] = (byte)(i % 251);

    return data;
  }

  private sealed record BoxSnapshot(uint Type, long Offset, long Size, int Parent);

  private static List<BoxSnapshot> _snapshot(List<IsoBmffBoxNode> boxes) =>
    [.. boxes.Select(x => new BoxSnapshot(x.Box.Type, x.Box.Offset, x.Box.Size, x.Parent))];

  private static MemoryStream _createTestFile(int beforeFreeSize = 32, int afterFreeSize = 32, bool withItemList = true) {
    var stream = new MemoryStream();

    _writeBox(stream, "ftyp", 24);

    if (beforeFreeSize > 0)
      _writeBox(stream, "free", beforeFreeSize);

    _writeContainer(stream, "moov", moov => {
      _writeBox(moov, "mvhd", 108);

      if (withItemList)
        _writeItemList(moov);

      _writeVideoTrack(moov);
    });

    if (afterFreeSize > 0)
      _writeBox(stream, "free", afterFreeSize);

    _writeBox(stream, "mdat", 40);

    stream.Position = 0;
    return stream;
  }

  private static void _writeItemList(Stream moov) {
    _writeContainer(moov, "udta", udta => {
      _writeContainer(udta, "meta", meta => {
        _writeUInt32(meta, 0);

        _writeBox(meta, "hdlr", 33);

        _writeContainer(meta, "ilst", ilst => {
          _writeBox(ilst, "keyw", 28);
        });
      });
    });
  }

  private static void _writeVideoTrack(Stream moov) {
    _writeContainer(moov, "trak", trak => {
      _writeBox(trak, "tkhd", 92);

      _writeContainer(trak, "mdia", mdia => {
        _writeMdhd(mdia);

        _writeContainer(mdia, "hdlr", hdlr => {
          _writeUInt32(hdlr, 0); // version + flags
          _writeUInt32(hdlr, 0); // pre_defined
          _writeUInt32(hdlr, IsoBmffTypes.Vide);
        });

        _writeContainer(mdia, "minf", minf => {
          _writeBox(minf, "vmhd", 20);

          _writeContainer(minf, "dinf", dinf => {
            _writeContainer(dinf, "dref", dref => {
            });
          });

          _writeContainer(minf, "stbl", stbl => {
            _writeBox(stbl, "stsd", 32);
            _writeStts(stbl);
            _writeBox(stbl, "stsz", 32);
            _writeBox(stbl, "stsc", 32);
            _writeBox(stbl, "co64", 32);
          });
        });
      });
    });
  }

  private static void _writeStts(Stream stbl) {
    _writeContainer(stbl, "stts", stts => {
      _writeUInt32(stts, 0);      // version + flags
      _writeUInt32(stts, 1);      // entry count
      _writeUInt32(stts, 30);     // sample count
      _writeUInt32(stts, 3000);   // sample delta
    });
  }

  private static void _writeMdhd(Stream mdia) {
    _writeContainer(mdia, "mdhd", mdhd => {
      _writeUInt32(mdhd, 0);       // version + flags
      _writeUInt32(mdhd, 0);       // creation time
      _writeUInt32(mdhd, 0);       // modification time
      _writeUInt32(mdhd, 90000);   // timescale
      _writeUInt32(mdhd, 900000);  // duration = 10 seconds
    });
  }

  private static void _writeContainer(Stream stream, string type, Action<Stream> writeChildren) {
    using var data = new MemoryStream();
    writeChildren(data);

    _writeUInt32(stream, checked((uint)(8 + data.Length)));
    _writeType(stream, type);

    data.Position = 0;
    data.CopyTo(stream);
  }

  private static void _writeBox(Stream stream, string type, int totalSize) {
    ArgumentOutOfRangeException.ThrowIfLessThan(totalSize, 8);

    _writeUInt32(stream, checked((uint)totalSize));
    _writeType(stream, type);

    var dataSize = totalSize - 8;

    for (var i = 0; i < dataSize; i++)
      stream.WriteByte((byte)(i + 1));
  }

  private static void _writeType(Stream stream, string type) {
    if (type.Length != 4)
      throw new ArgumentException("Box type must contain exactly four characters.", nameof(type));

    stream.WriteByte((byte)type[0]);
    stream.WriteByte((byte)type[1]);
    stream.WriteByte((byte)type[2]);
    stream.WriteByte((byte)type[3]);
  }

  private static void _writeUInt32(Stream stream, uint value) {
    stream.WriteByte((byte)(value >> 24));
    stream.WriteByte((byte)(value >> 16));
    stream.WriteByte((byte)(value >> 8));
    stream.WriteByte((byte)value);
  }
}