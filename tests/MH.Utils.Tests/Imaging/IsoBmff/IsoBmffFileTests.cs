using MH.Utils.Imaging.IsoBmff;
using System.Diagnostics;
using System.Text;

namespace MH.Utils.Tests.Imaging.IsoBmff;

[TestClass]
public class IsoBmffFileTests {
  public const long ExpensiveBoxSize = 2 * 1024 * 1024;

  //[TestMethod]
  public void DebugTest() {
    var path = @"e:\!test\vid\input.mp4";
    //var path = @"e:\!test\vid\keywords-default.mp4";
    //var path = @"e:\!test\vid\keywords-itemlist.mp4";
    //var path = @"e:\!test\vid\keywords-keys.mp4";
    //var path = @"e:\!test\vid\keywords-xmp.mp4";
    //var path = @"e:\Pictures\01 Digital_Foto\-=Sklad\vid\new\20240929_162412.mp4"; // rotated 90
    using var stream = File.OpenRead(path);
    var file = new IsoBmffFile(stream);

    if (file._metadata._getItemList() is { } itemList)
      Debug.WriteLine($"ItemList Keywords: {itemList.GetKeywords()}");

    if (file._metadata._getKeys() is { } keys)
      Debug.WriteLine($"Keys Keywords: {keys.GetKeywords()}");

    Debug.WriteLine($"Metadata Keywords: {string.Join(", ", file._metadata.GetKeywords() ?? [])}");

    if (file._getXmp() is { } xmp)
      Debug.WriteLine($"XMP Keywords: {string.Join(", ", xmp.GetKeywords() ?? [])}");

    //file.DumpBoxes();
    //var boxes = file.ReadBoxes();

    /*if (file._reader.Find(IsoBmffTypes.Moov) is { } moov) {
      var metadata = file._metadata.Read(moov);
      var xmp = file._metadata.Xmp;

      foreach (var entry in metadata)
        Debug.WriteLine($"key: {entry.Key}, value: {entry.Value}");
    }*/
  }

  [TestMethod]
  public void InsertBox_PreservesHierarchyOrder() {
    var boxes = new List<IsoBmffBoxNode> {
      new(new IsoBmffBox(0, 8, 100, IsoBmffTypes.Moov), -1),
      new(new IsoBmffBox(8, 8, 20, IsoBmffTypes.Mvhd), 0),
      new(new IsoBmffBox(28, 8, 40, IsoBmffTypes.Udta), 0),
      new(new IsoBmffBox(36, 12, 30, IsoBmffTypes.Meta), 2),
      new(new IsoBmffBox(48, 8, 20, IsoBmffTypes.Ilst), 3),
      new(new IsoBmffBox(68, 8, 50, IsoBmffTypes.Trak), 0),
    };

    var keywIndex = boxes.InsertBox(new IsoBmffBox(-1, 8, 29, IsoBmffTypes.Keyw), 4);
    var dataIndex = boxes.InsertBox(new IsoBmffBox(-1, 8, 21, IsoBmffTypes.Data), keywIndex);

    Assert.AreEqual(5, keywIndex);
    Assert.AreEqual(6, dataIndex);

    Assert.AreEqual(IsoBmffTypes.Moov, boxes[0].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Mvhd, boxes[1].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Udta, boxes[2].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Meta, boxes[3].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Ilst, boxes[4].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Keyw, boxes[5].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Data, boxes[6].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Trak, boxes[7].Box.Type);

    Assert.AreEqual(-1, boxes[0].Parent);
    Assert.AreEqual(0, boxes[1].Parent);
    Assert.AreEqual(0, boxes[2].Parent);
    Assert.AreEqual(2, boxes[3].Parent);
    Assert.AreEqual(3, boxes[4].Parent);
    Assert.AreEqual(4, boxes[5].Parent);
    Assert.AreEqual(5, boxes[6].Parent);
    Assert.AreEqual(0, boxes[7].Parent);

    var secondKeywIndex = boxes.InsertBox(new IsoBmffBox(-1, 8, 30, IsoBmffTypes.Keyw), 4);

    Assert.AreEqual(7, secondKeywIndex);
    Assert.AreEqual(IsoBmffTypes.Keyw, boxes[7].Box.Type);
    Assert.AreEqual(4, boxes[7].Parent);
    Assert.AreEqual(IsoBmffTypes.Trak, boxes[8].Box.Type);
  }

  [TestMethod]
  public void RemoveBox_UpdatesParentIndexes() {
    var boxes = new List<IsoBmffBoxNode> {
      new(new IsoBmffBox(0, 8, 200, IsoBmffTypes.Moov), -1),
      new(new IsoBmffBox(8, 8, 20, IsoBmffTypes.Mvhd), 0),

      new(new IsoBmffBox(28, 8, 60, IsoBmffTypes.Udta), 0),
      new(new IsoBmffBox(36, 12, 40, IsoBmffTypes.Meta), 2),
      new(new IsoBmffBox(48, 8, 30, IsoBmffTypes.Ilst), 3),
      new(new IsoBmffBox(78, 8, 20, IsoBmffTypes.Keyw), 4),
      new(new IsoBmffBox(98, 8, 12, IsoBmffTypes.Data), 5),

      new(new IsoBmffBox(110, 8, 40, IsoBmffTypes.Trak), 0),
      new(new IsoBmffBox(150, 8, 20, IsoBmffTypes.Mdia), 7),
    };

    // Remove keyw + its data.
    boxes.RemoveBox(5);

    Assert.AreEqual(IsoBmffTypes.Moov, boxes[0].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Mvhd, boxes[1].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Udta, boxes[2].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Meta, boxes[3].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Ilst, boxes[4].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Trak, boxes[5].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Mdia, boxes[6].Box.Type);

    Assert.AreEqual(-1, boxes[0].Parent);
    Assert.AreEqual(0, boxes[1].Parent);
    Assert.AreEqual(0, boxes[2].Parent);
    Assert.AreEqual(2, boxes[3].Parent);
    Assert.AreEqual(3, boxes[4].Parent);
    Assert.AreEqual(0, boxes[5].Parent);
    Assert.AreEqual(5, boxes[6].Parent);

    Assert.AreEqual(180, boxes[0].Box.Size);
    Assert.AreEqual(20, boxes[1].Box.Size);
    Assert.AreEqual(40, boxes[2].Box.Size);
    Assert.AreEqual(20, boxes[3].Box.Size);
    Assert.AreEqual(10, boxes[4].Box.Size);
  }

  [TestMethod]
  public void ItemList_CreateUpdateDeleteKeywords() {
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

    // Set Keywords
    file._metadata.SetKeywords("hello");

    boxes = file._metadata._getEditedBoxes();

    var keywIndex = boxes.FindIndex(IsoBmffTypes.Keyw, ilstIndex);
    var dataIndex = boxes.FindIndex(IsoBmffTypes.Data, keywIndex);

    Assert.IsTrue(keywIndex >= 0);
    Assert.IsTrue(dataIndex >= 0);

    Assert.AreEqual(ilstIndex, boxes[keywIndex].Parent);
    Assert.AreEqual(keywIndex, boxes[dataIndex].Parent);

    Assert.AreEqual(-1, boxes[keywIndex].Box.Offset);
    Assert.AreEqual(-1, boxes[dataIndex].Box.Offset);

    Assert.AreEqual(29, boxes[keywIndex].Box.Size);
    Assert.AreEqual(21, boxes[dataIndex].Box.Size);

    Assert.AreEqual(37, boxes[ilstIndex].Box.Size);
    Assert.AreEqual(49, boxes[metaIndex].Box.Size);
    Assert.AreEqual(57, boxes[udtaIndex].Box.Size);
    Assert.AreEqual(originalMoovSize + 57, boxes[moovIndex].Box.Size);

    Assert.AreEqual("hello", itemList.GetKeywords());

    // Update Keywords
    file._metadata.SetKeywords("world");

    Assert.AreEqual("world", itemList.GetKeywords());

    var keywCount = boxes.Count(x => x.Box.Type == IsoBmffTypes.Keyw);
    var dataCount = boxes.Count(x => x.Box.Type == IsoBmffTypes.Data);

    Assert.AreEqual(1, keywCount);
    Assert.AreEqual(1, dataCount);

    boxes = file._metadata._getEditedBoxes();

    Assert.AreEqual(37, boxes[ilstIndex].Box.Size);
    Assert.AreEqual(49, boxes[metaIndex].Box.Size);
    Assert.AreEqual(57, boxes[udtaIndex].Box.Size);
    Assert.AreEqual(originalMoovSize + 28 + 29, boxes[moovIndex].Box.Size);

    // Update Keywords 2
    file._metadata.SetKeywords("a much longer keyword value");

    Assert.AreEqual("a much longer keyword value", itemList.GetKeywords());

    var valueSize = Encoding.UTF8.GetByteCount("a much longer keyword value");
    var expectedItemSize = 24 + valueSize;

    Assert.AreEqual(expectedItemSize, boxes[keywIndex].Box.Size);
    Assert.AreEqual(16 + valueSize, boxes[dataIndex].Box.Size);

    Assert.AreEqual(8 + expectedItemSize, boxes[ilstIndex].Box.Size);
    Assert.AreEqual(20 + expectedItemSize, boxes[metaIndex].Box.Size);
    Assert.AreEqual(28 + expectedItemSize, boxes[udtaIndex].Box.Size);

    // Update Keywords 3
    var smallerValue = "x";
    var smallerValueSize = Encoding.UTF8.GetByteCount(smallerValue);
    var expectedSmallerItemSize = 24 + smallerValueSize;

    file._metadata.SetKeywords(smallerValue);

    boxes = file._metadata._getEditedBoxes();

    Assert.AreEqual(expectedSmallerItemSize, boxes[keywIndex].Box.Size);
    Assert.AreEqual(16 + smallerValueSize, boxes[dataIndex].Box.Size);

    Assert.AreEqual(8 + expectedSmallerItemSize, boxes[ilstIndex].Box.Size);
    Assert.AreEqual(20 + expectedSmallerItemSize, boxes[metaIndex].Box.Size);
    Assert.AreEqual(28 + expectedSmallerItemSize, boxes[udtaIndex].Box.Size);

    Assert.AreEqual("x", itemList.GetKeywords());

    // Remove
    file._metadata.SetKeywords(null);

    boxes = file._metadata._getEditedBoxes();

    keywIndex = boxes.FindIndex(IsoBmffTypes.Keyw, ilstIndex);
    Assert.AreEqual(-1, keywIndex);

    dataIndex = boxes.FindIndex(IsoBmffTypes.Data);
    Assert.AreEqual(-1, dataIndex);

    Assert.AreEqual(8, boxes[ilstIndex].Box.Size);
    Assert.AreEqual(20, boxes[metaIndex].Box.Size);
    Assert.AreEqual(28, boxes[udtaIndex].Box.Size);
    Assert.AreEqual(originalMoovSize + 28, boxes[moovIndex].Box.Size);

    Assert.IsNull(itemList.GetKeywords());
  }

  private sealed record BoxSnapshot(uint Type, long Offset, long Size, int Parent);

  private static List<BoxSnapshot> _snapshot(List<IsoBmffBoxNode> boxes) =>
    [.. boxes.Select(x => new BoxSnapshot(x.Box.Type, x.Box.Offset, x.Box.Size, x.Parent))];

  internal static MemoryStream _createTestFile(int beforeFreeSize = 32, int afterFreeSize = 32, bool withItemList = true) {
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

  internal static void _resize(IsoBmffBoxNode node, long size) {
    node.Box = new IsoBmffBox(node.Box.Offset, node.Box.HeaderSize, size, node.Box.Type);
  }
}