using MH.Utils.Imaging.IsoBmff;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;

namespace MH.Utils.Tests.Imaging.IsoBmff;

[TestClass]
public class IsoBmffFileTests {
  public const long ExpensiveBoxSize = 2 * 1024 * 1024;

  //[TestMethod]
  public void DebugTest() {
    var path = @"e:\!test\vid\sup.mp4";
    //var path = @"e:\!test\vid\keywords-default.mp4";
    //var path = @"e:\!test\vid\keywords-itemlist.mp4";
    //var path = @"e:\!test\vid\keywords-keys.mp4";
    //var path = @"e:\!test\vid\keywords-xmp.mp4";
    //var path = @"e:\Pictures\01 Digital_Foto\-=Sklad\vid\new\20240929_162412.mp4"; // rotated 90

    using var file = new IsoBmffFile(path);

    Debug.WriteLine("original boxes");
    file._reader._dumpBoxes();

    //file._metadata.SetItemListKeywords("jiný delší text než předtím");
    //file._metadata.SetComment("test comment");
    file._metadata.SetKeysKeywords("test keys keyword");
    file._metadata.SetKeysComment("test keys comment");
    file.Save();

    Debug.WriteLine("edited boxes");
    file._reader._dumpBoxes();

    /*using var stream = File.OpenRead(path);
    var file = new IsoBmffFile(stream);

    if (file._metadata._getItemList() is { } itemList)
      Debug.WriteLine($"ItemList Keywords: {itemList.GetKeywords()}");

    if (file._metadata._getKeys() is { } keys)
      Debug.WriteLine($"Keys Keywords: {keys.GetKeywords()}");

    Debug.WriteLine($"Metadata Keywords: {string.Join(", ", file._metadata.GetKeywords() ?? [])}");

    if (file._getXmp() is { } xmp)
      Debug.WriteLine($"XMP Keywords: {string.Join(", ", xmp.GetKeywords() ?? [])}");*/

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
    var nodes = new List<IsoBmffNode> {
      new(new IsoBmffBox(0, 8, 100, IsoBmffTypes.Moov), -1),
      new(new IsoBmffBox(8, 8, 20, IsoBmffTypes.Mvhd), 0),
      new(new IsoBmffBox(28, 8, 40, IsoBmffTypes.Udta), 0),
      new(new IsoBmffBox(36, 12, 30, IsoBmffTypes.Meta), 2),
      new(new IsoBmffBox(48, 8, 20, IsoBmffTypes.Ilst), 3),
      new(new IsoBmffBox(68, 8, 50, IsoBmffTypes.Trak), 0),
    };

    var keywIndex = nodes.InsertNode(IsoBmffTypes.Keyw, 29, 4);
    var dataIndex = nodes.InsertNode(IsoBmffTypes.Data, 21, keywIndex);

    Assert.AreEqual(5, keywIndex);
    Assert.AreEqual(6, dataIndex);

    Assert.AreEqual(IsoBmffTypes.Moov, nodes[0].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Mvhd, nodes[1].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Udta, nodes[2].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Meta, nodes[3].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Ilst, nodes[4].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Keyw, nodes[5].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Data, nodes[6].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Trak, nodes[7].Box.Type);

    Assert.AreEqual(-1, nodes[0].Parent);
    Assert.AreEqual(0, nodes[1].Parent);
    Assert.AreEqual(0, nodes[2].Parent);
    Assert.AreEqual(2, nodes[3].Parent);
    Assert.AreEqual(3, nodes[4].Parent);
    Assert.AreEqual(4, nodes[5].Parent);
    Assert.AreEqual(5, nodes[6].Parent);
    Assert.AreEqual(0, nodes[7].Parent);

    var secondKeywIndex = nodes.InsertNode(IsoBmffTypes.Keyw, 30, 4);

    Assert.AreEqual(7, secondKeywIndex);
    Assert.AreEqual(IsoBmffTypes.Keyw, nodes[7].Box.Type);
    Assert.AreEqual(4, nodes[7].Parent);
    Assert.AreEqual(IsoBmffTypes.Trak, nodes[8].Box.Type);
  }

  [TestMethod]
  public void RemoveBox_UpdatesParentIndexes() {
    var nodes = new List<IsoBmffNode> {
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
    nodes.RemoveNode(5);

    Assert.AreEqual(IsoBmffTypes.Moov, nodes[0].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Mvhd, nodes[1].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Udta, nodes[2].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Meta, nodes[3].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Ilst, nodes[4].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Trak, nodes[5].Box.Type);
    Assert.AreEqual(IsoBmffTypes.Mdia, nodes[6].Box.Type);

    Assert.AreEqual(-1, nodes[0].Parent);
    Assert.AreEqual(0, nodes[1].Parent);
    Assert.AreEqual(0, nodes[2].Parent);
    Assert.AreEqual(2, nodes[3].Parent);
    Assert.AreEqual(3, nodes[4].Parent);
    Assert.AreEqual(0, nodes[5].Parent);
    Assert.AreEqual(5, nodes[6].Parent);

    Assert.AreEqual(180, nodes[0].Box.Size);
    Assert.AreEqual(20, nodes[1].Box.Size);
    Assert.AreEqual(40, nodes[2].Box.Size);
    Assert.AreEqual(20, nodes[3].Box.Size);
    Assert.AreEqual(10, nodes[4].Box.Size);
  }

  [TestMethod]
  public void ItemList_CreateUpdateDeleteKeywords() {
    using var stream = _createTestFile(withItemList: false);

    var file = new IsoBmffFile(stream);
    var originalNodes = file._metadata._editor._getOriginalNodes();
    var originalBoxSizes = originalNodes.Select(x => x.Box.Size).ToArray();
    var nodes = file._metadata._editor.GetEditedNodes();

    var moovIndex = nodes.FindIndex(IsoBmffTypes.Moov);
    Assert.IsTrue(moovIndex >= 0);

    var originalMoovSize = nodes[moovIndex].Box.Size;

    var itemList = file._metadata._getOrCreateItemList();

    Assert.IsNotNull(itemList);

    nodes = file._metadata._editor.GetEditedNodes();

    var udtaIndex = nodes.FindIndex(IsoBmffTypes.Udta, moovIndex);
    var metaIndex = nodes.FindIndex(IsoBmffTypes.Meta, udtaIndex);
    var ilstIndex = nodes.FindIndex(IsoBmffTypes.Ilst, metaIndex);

    Assert.IsTrue(udtaIndex >= 0);
    Assert.IsTrue(metaIndex >= 0);
    Assert.IsTrue(ilstIndex >= 0);

    Assert.AreEqual(moovIndex, nodes[udtaIndex].Parent);
    Assert.AreEqual(udtaIndex, nodes[metaIndex].Parent);
    Assert.AreEqual(metaIndex, nodes[ilstIndex].Parent);

    Assert.AreEqual(-1, nodes[udtaIndex].Box.Offset);
    Assert.AreEqual(-1, nodes[metaIndex].Box.Offset);
    Assert.AreEqual(-1, nodes[ilstIndex].Box.Offset);

    Assert.AreEqual(28, nodes[udtaIndex].Box.Size);
    Assert.AreEqual(20, nodes[metaIndex].Box.Size);
    Assert.AreEqual(8, nodes[ilstIndex].Box.Size);
    CollectionAssert.AreEqual(new byte[4], nodes[metaIndex].Data);

    Assert.AreEqual(originalMoovSize + 28, nodes[moovIndex].Box.Size);

    var secondItemList = file._metadata._getOrCreateItemList();

    Assert.AreSame(itemList, secondItemList);
    Assert.AreEqual(nodes.Count, file._metadata._editor.GetEditedNodes().Count);
    Assert.AreEqual(originalMoovSize + 28, nodes[moovIndex].Box.Size);

    // Set Keywords
    file._metadata.SetItemListKeywords("hello");

    nodes = file._metadata._editor.GetEditedNodes();

    var keywIndex = nodes.FindIndex(IsoBmffTypes.Keyw, ilstIndex);
    var dataIndex = nodes.FindIndex(IsoBmffTypes.Data, keywIndex);

    Assert.IsTrue(keywIndex >= 0);
    Assert.IsTrue(dataIndex >= 0);

    Assert.AreEqual(ilstIndex, nodes[keywIndex].Parent);
    Assert.AreEqual(keywIndex, nodes[dataIndex].Parent);

    Assert.AreEqual(-1, nodes[keywIndex].Box.Offset);
    Assert.AreEqual(-1, nodes[dataIndex].Box.Offset);

    Assert.AreEqual(29, nodes[keywIndex].Box.Size);
    Assert.AreEqual(21, nodes[dataIndex].Box.Size);
    _assertKeywordData(nodes[dataIndex], "hello");

    Assert.AreEqual(37, nodes[ilstIndex].Box.Size);
    Assert.AreEqual(49, nodes[metaIndex].Box.Size);
    Assert.AreEqual(57, nodes[udtaIndex].Box.Size);
    Assert.AreEqual(originalMoovSize + 57, nodes[moovIndex].Box.Size);

    Assert.AreEqual("hello", itemList.GetKeywords());

    // Update Keywords
    file._metadata.SetItemListKeywords("world");

    Assert.AreEqual("world", itemList.GetKeywords());

    var keywCount = nodes.Count(x => x.Box.Type == IsoBmffTypes.Keyw);
    var dataCount = nodes.Count(x => x.Box.Type == IsoBmffTypes.Data);

    Assert.AreEqual(1, keywCount);
    Assert.AreEqual(1, dataCount);

    nodes = file._metadata._editor.GetEditedNodes();
    _assertKeywordData(nodes[dataIndex], "world");

    Assert.AreEqual(37, nodes[ilstIndex].Box.Size);
    Assert.AreEqual(49, nodes[metaIndex].Box.Size);
    Assert.AreEqual(57, nodes[udtaIndex].Box.Size);
    Assert.AreEqual(originalMoovSize + 28 + 29, nodes[moovIndex].Box.Size);

    // Update Keywords 2
    const string longerValue = "a much longer keyword value";
    file._metadata.SetItemListKeywords(longerValue);

    Assert.AreEqual(longerValue, itemList.GetKeywords());

    nodes = file._metadata._editor.GetEditedNodes();

    var valueSize = Encoding.UTF8.GetByteCount(longerValue);
    var expectedItemSize = 24 + valueSize;

    Assert.AreEqual(expectedItemSize, nodes[keywIndex].Box.Size);
    Assert.AreEqual(16 + valueSize, nodes[dataIndex].Box.Size);
    _assertKeywordData(nodes[dataIndex], longerValue);

    Assert.AreEqual(8 + expectedItemSize, nodes[ilstIndex].Box.Size);
    Assert.AreEqual(20 + expectedItemSize, nodes[metaIndex].Box.Size);
    Assert.AreEqual(28 + expectedItemSize, nodes[udtaIndex].Box.Size);

    // Update Keywords 3
    const string smallerValue = "x";
    var smallerValueSize = Encoding.UTF8.GetByteCount(smallerValue);
    var expectedSmallerItemSize = 24 + smallerValueSize;

    file._metadata.SetItemListKeywords(smallerValue);

    nodes = file._metadata._editor.GetEditedNodes();

    Assert.AreEqual(expectedSmallerItemSize, nodes[keywIndex].Box.Size);
    Assert.AreEqual(16 + smallerValueSize, nodes[dataIndex].Box.Size);
    _assertKeywordData(nodes[dataIndex], smallerValue);

    Assert.AreEqual(8 + expectedSmallerItemSize, nodes[ilstIndex].Box.Size);
    Assert.AreEqual(20 + expectedSmallerItemSize, nodes[metaIndex].Box.Size);
    Assert.AreEqual(28 + expectedSmallerItemSize, nodes[udtaIndex].Box.Size);

    Assert.AreEqual("x", itemList.GetKeywords());

    // Remove
    file._metadata.SetItemListKeywords(null);

    nodes = file._metadata._editor.GetEditedNodes();

    keywIndex = nodes.FindIndex(IsoBmffTypes.Keyw, ilstIndex);
    Assert.AreEqual(-1, keywIndex);

    dataIndex = nodes.FindIndex(IsoBmffTypes.Data);
    Assert.AreEqual(-1, dataIndex);

    Assert.AreEqual(8, nodes[ilstIndex].Box.Size);
    Assert.AreEqual(20, nodes[metaIndex].Box.Size);
    Assert.AreEqual(28, nodes[udtaIndex].Box.Size);
    Assert.AreEqual(originalMoovSize + 28, nodes[moovIndex].Box.Size);

    Assert.IsNull(itemList.GetKeywords());
    CollectionAssert.AreEqual(originalBoxSizes, originalNodes.Select(x => x.Box.Size).ToArray());
  }

  [TestMethod]
  public void ItemList_UpdateKeywords_AfterSave() {
    using var stream = _createTestFile(beforeFreeSize: 128, afterFreeSize: 0, withItemList: false);

    var originalLength = stream.Length;
    var originalMoov = new IsoBmffReader(stream).Find(IsoBmffTypes.Moov)
      ?? throw new InvalidDataException("Original moov box was not found.");
    var originalMdatPayload = _readBoxPayload(stream, IsoBmffTypes.Mdat);

    using var file = new IsoBmffFile(stream);

    file._metadata.SetItemListKeywords("first");
    file.Save();

    var movedMoov = file._reader.Find(IsoBmffTypes.Moov)
      ?? throw new InvalidDataException("Saved moov box was not found.");

    Assert.IsTrue(movedMoov.Offset < originalMoov.Offset);
    Assert.AreEqual(originalLength, stream.Length);
    Assert.AreEqual("first", file._metadata.GetItemListKeywords());
    CollectionAssert.AreEqual(originalMdatPayload, _readBoxPayload(stream, IsoBmffTypes.Mdat));

    file._metadata.SetItemListKeywords("second");
    file.Save();

    Assert.AreEqual("second", file._metadata.GetItemListKeywords());
    Assert.AreEqual(originalLength, stream.Length);
    CollectionAssert.AreEqual(originalMdatPayload, _readBoxPayload(stream, IsoBmffTypes.Mdat));
  }

  [TestMethod]
  public void Save_FullRewrite_AddsXmpAndMultipleEdits_FileBacked() {
    using var source = _createTestFile(
      beforeFreeSize: 0,
      afterFreeSize: 0,
      withItemList: false,
      withChunkOffsetTable: false);

    var originalLength = source.Length;
    var originalBytes = source.ToArray();
    var originalMdatPayload = _readBoxPayload(source, IsoBmffTypes.Mdat);
    var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");

    try {
      File.WriteAllBytes(path, originalBytes);

      using (var file = new IsoBmffFile(path)) {
        file._metadata.SetItemListKeywords("metadata keyword");
        file._metadata.SetItemListComment("metadata comment");
        file.Xmp.SetKeywords(["XMP keyword"]);
        file.Save();
      }

      using var savedFile = new IsoBmffFile(path);

      Assert.IsTrue(new FileInfo(path).Length > originalLength);
      Assert.AreEqual("metadata keyword", savedFile._metadata.GetItemListKeywords());
      Assert.AreEqual("metadata comment", savedFile._metadata.GetItemListComment());
      Assert.IsNotNull(savedFile._reader.Find(IsoBmffTypes.Uuid));
      CollectionAssert.AreEqual(new string[] { "XMP keyword" }, savedFile.Xmp.GetKeywords());
      CollectionAssert.AreEqual(originalMdatPayload, _readBoxPayload(savedFile._reader._stream, IsoBmffTypes.Mdat));
    }
    finally {
      if (File.Exists(path))
        File.Delete(path);
    }
  }

  private static byte[] _readBoxPayload(Stream stream, uint type) {
    var reader = new IsoBmffReader(stream);
    var box = reader.Find(type)
      ?? throw new InvalidDataException($"The {IsoBmffTypes.GetTypeName(type)} box was not found.");
    var payload = new byte[checked((int)box.DataSize)];

    stream.Position = box.DataOffset;
    stream.ReadExactly(payload);

    return payload;
  }

  private static void _assertKeywordData(IsoBmffNode dataNode, string value) {
    var valueBytes = Encoding.UTF8.GetBytes(value);
    var expected = new byte[8 + valueBytes.Length];

    BinaryPrimitives.WriteUInt32BigEndian(expected, 1);
    valueBytes.CopyTo(expected, 8);

    Assert.IsNotNull(dataNode.Data);
    CollectionAssert.AreEqual(expected, dataNode.Data);
  }

  internal static MemoryStream _createTestFile(
    int beforeFreeSize = 32,
    int afterFreeSize = 32,
    bool withItemList = true,
    bool withChunkOffsetTable = true) {

    var stream = new MemoryStream();

    _writeBox(stream, "ftyp", 24);

    if (beforeFreeSize > 0)
      _writeBox(stream, "free", beforeFreeSize);

    _writeContainer(stream, "moov", moov => {
      _writeBox(moov, "mvhd", 108);

      if (withItemList)
        _writeItemList(moov);

      _writeVideoTrack(moov, withChunkOffsetTable);
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
          _writeContainer(ilst, "keyw", keyw => {
            _writeBox(keyw, "data", 20);
          });
        });
      });
    });
  }

  private static void _writeVideoTrack(Stream moov, bool withChunkOffsetTable) {
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

            if (withChunkOffsetTable)
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

  internal static void _resize(IsoBmffNode node, long size) {
    node.Box = new IsoBmffBox(node.Box.Offset, node.Box.HeaderSize, size, node.Box.Type);
  }
}