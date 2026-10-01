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
    Assert.IsTrue(move.Direction == IsoBmffMoveDirection.Up);
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

  private sealed record BoxSnapshot(uint Type, long Offset, long Size, int Parent);

  private static List<BoxSnapshot> _snapshot(List<IsoBmffBoxNode> boxes) =>
    [.. boxes.Select(x => new BoxSnapshot(x.Box.Type, x.Box.Offset, x.Box.Size, x.Parent))];

  private static MemoryStream _createTestFile(int beforeFreeSize = 32, int afterFreeSize = 32) {
    var stream = new MemoryStream();

    _writeBox(stream, "ftyp", 24);

    if (beforeFreeSize > 0)
      _writeBox(stream, "free", beforeFreeSize);

    _writeContainer(stream, "moov", moov => {
      _writeBox(moov, "mvhd", 108);

      _writeContainer(moov, "meta", meta => {
        // meta is a FullBox: version + flags.
        _writeUInt32(meta, 0);

        _writeBox(meta, "hdlr", 33);
        _writeBox(meta, "keys", 43);

        _writeContainer(meta, "ilst", ilst => {
          _writeBox(ilst, "keyw", 28);
        });
      });

      _writeContainer(moov, "trak", trak => {
        _writeBox(trak, "tkhd", 92);

        _writeContainer(trak, "mdia", mdia => {
          _writeBox(mdia, "mdhd", 32);
          _writeBox(mdia, "hdlr", 44);

          _writeContainer(mdia, "minf", minf => {
            _writeBox(minf, "vmhd", 20);

            _writeContainer(minf, "dinf", dinf => {
              _writeContainer(dinf, "dref", dref => {
              });
            });

            _writeContainer(minf, "stbl", stbl => {
              _writeBox(stbl, "stsd", 32);
              _writeBox(stbl, "stts", 32);
              _writeBox(stbl, "stsz", 32);
              _writeBox(stbl, "stsc", 32);
              _writeBox(stbl, "co64", 32);
            });
          });
        });
      });
    });

    if (afterFreeSize > 0)
      _writeBox(stream, "free", afterFreeSize);

    _writeBox(stream, "mdat", 40);

    stream.Position = 0;
    return stream;
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