using System.Buffers.Binary;
using MH.Utils.Imaging.IsoBmff;
using MH.Utils.Imaging.Xmp;

namespace MH.Utils.Tests.Imaging.IsoBmff;

[TestClass]
public class IsoBmffFileIntegrationTests {
  [TestMethod]
  public void EditItemListAndXmpKeywords_WritesAndReadsBothChanges() {
    using var input = _createTestFileWithXmp();
    var file = new IsoBmffFile(input);
    var originalBoxes = file._metadata._getOriginalBoxes();

    var originalPlanner = new IsoBmffLayoutPlanner(originalBoxes, originalBoxes, file._reader.GetBoxChildrenOffset);
    var original = originalPlanner.CreateOriginalLayout();

    file._metadata.SetKeywords("box-keyword");
    file.Xmp.SetKeywords(["xmp-keyword"]);

    var xmpBox = file._reader.Find(IsoBmffTypes.Uuid)
      ?? throw new InvalidDataException("XMP uuid box was not found.");

    var editedBoxes = file._metadata._getEditedBoxes();
    var editedXmpBox = editedBoxes.Single(x => x.Box.Type == IsoBmffTypes.Uuid && x.Box.Offset == xmpBox.Offset);

    var xmpPacket = file.Xmp.ToPacket();
    var xmpData = new byte[16 + xmpPacket.Length];

    input.Position = xmpBox.DataOffset;
    input.ReadExactly(xmpData.AsSpan(0, 16));
    xmpPacket.CopyTo(xmpData, 16);

    editedXmpBox.Data = xmpData;

    var editedXmpSize = editedXmpBox.Box.HeaderSize + xmpData.LongLength;
    editedXmpBox.Box = new IsoBmffBox(editedXmpBox.Box.Offset, editedXmpBox.Box.HeaderSize, editedXmpSize, editedXmpBox.Box.Type);

    var planner = new IsoBmffLayoutPlanner(originalBoxes, editedBoxes, file._reader.GetBoxChildrenOffset);
    var plan = planner.CreateEditedLayout(original, editedBoxes);

    Assert.IsFalse(plan.RequiresFullRewrite);

    var diff = IsoBmffLayoutDiffer.Diff(original, plan.Layout, editedBoxes.Where(x => x.Data is not null));

    var originalBytes = input.ToArray();
    using var output = new MemoryStream();
    output.Write(originalBytes);

    IsoBmffLayoutExecutor.ExecuteMoves(output, diff.Moves);
    IsoBmffLayoutExecutor.ExecuteWrites(output, diff.Writes);

    Assert.AreEqual(originalBytes.Length, output.Length);

    output.Position = 0;
    var rewrittenFile = new IsoBmffFile(output);

    Assert.AreEqual("box-keyword", rewrittenFile._metadata.GetItemListKeywords());
    CollectionAssert.AreEqual(new[] { "xmp-keyword" }, rewrittenFile.Xmp.GetKeywords());
  }

  [TestMethod]
  public void CreateItemListAndXmpKeywords_WritesAndReadsBothChanges() {
    using var input = IsoBmffFileTests._createTestFile(
      beforeFreeSize: 0,
      afterFreeSize: 16384,
      withItemList: false);

    var file = new IsoBmffFile(input);

    Assert.IsNull(file._metadata._getItemList());
    Assert.IsNull(file._reader.Find(IsoBmffTypes.Uuid));

    var originalBoxes = file._metadata._getOriginalBoxes();
    var originalPlanner = new IsoBmffLayoutPlanner(
      originalBoxes,
      originalBoxes,
      file._reader.GetBoxChildrenOffset);
    var original = originalPlanner.CreateOriginalLayout();

    file._metadata.SetKeywords("box-keyword");
    file.Xmp.SetKeywords(["xmp-keyword"]);

    var editedBoxes = file._getEditedBoxes();
    var xmpBox = editedBoxes.Single(x =>
      x.Box.Type == IsoBmffTypes.Uuid &&
      x.Box.Offset == -1);

    Assert.IsNotNull(xmpBox.Data);

    var planner = new IsoBmffLayoutPlanner(
      originalBoxes,
      editedBoxes,
      file._reader.GetBoxChildrenOffset);
    var plan = planner.CreateEditedLayout(original, editedBoxes);

    Assert.IsFalse(plan.RequiresFullRewrite);
    Assert.IsTrue(plan.Layout.TryGetValue(xmpBox, out _));

    var diff = IsoBmffLayoutDiffer.Diff(
      original,
      plan.Layout,
      editedBoxes.Where(x => x.Data is not null));

    var originalBytes = input.ToArray();
    using var output = new MemoryStream();
    output.Write(originalBytes);

    IsoBmffLayoutExecutor.ExecuteMoves(output, diff.Moves);
    IsoBmffLayoutExecutor.ExecuteWrites(output, diff.Writes);

    Assert.AreEqual(originalBytes.Length, output.Length);

    output.Position = 0;
    var rewrittenFile = new IsoBmffFile(output);

    Assert.AreEqual("box-keyword", rewrittenFile._metadata.GetItemListKeywords());
    CollectionAssert.AreEqual(new[] { "xmp-keyword" }, rewrittenFile.Xmp.GetKeywords());
  }

  [TestMethod]
  public void FullRewrite_CreatesItemListAndXmpKeywords() {
    using var input = IsoBmffFileTests._createTestFile(beforeFreeSize: 0, afterFreeSize: 0, withItemList: false);

    var inputReader = new IsoBmffReader(input);
    var inputBoxes = inputReader._readBoxes();
    var inputMdat = inputBoxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var inputCo64 = inputBoxes.Single(x => x.Box.Type == IsoBmffTypes.Co64);
    var co64Data = new byte[checked((int)inputCo64.Box.DataSize)];

    BinaryPrimitives.WriteUInt32BigEndian(co64Data.AsSpan(4), 2);
    BinaryPrimitives.WriteUInt64BigEndian(co64Data.AsSpan(8), checked((ulong)inputMdat.Box.DataOffset));
    BinaryPrimitives.WriteUInt64BigEndian(co64Data.AsSpan(16), checked((ulong)inputMdat.Box.DataOffset + 8));

    input.Position = inputCo64.Box.DataOffset;
    input.Write(co64Data);
    input.Position = 0;

    var file = new IsoBmffFile(input);
    var originalBoxes = file._metadata._getOriginalBoxes();
    var originalPlanner = new IsoBmffLayoutPlanner(
      originalBoxes,
      originalBoxes,
      file._reader.GetBoxChildrenOffset);
    var original = originalPlanner.CreateOriginalLayout();

    file._metadata.SetKeywords("box-keyword");
    file.Xmp.SetKeywords(["xmp-keyword"]);

    var editedBoxes = file._getEditedBoxes();
    var planner = new IsoBmffLayoutPlanner(
      originalBoxes,
      editedBoxes,
      file._reader.GetBoxChildrenOffset);
    var plan = planner.CreateEditedLayout(original, editedBoxes);

    Assert.IsTrue(plan.RequiresFullRewrite);

    using var output = new MemoryStream();

    IsoBmffLayoutExecutor.ExecuteFullRewrite(
      input,
      output,
      originalBoxes,
      editedBoxes,
      plan.Layout,
      file._reader.GetBoxChildrenOffset);

    Assert.IsTrue(output.Length > input.Length);

    output.Position = 0;
    var rewrittenFile = new IsoBmffFile(output);

    Assert.AreEqual("box-keyword", rewrittenFile._metadata.GetItemListKeywords());
    CollectionAssert.AreEqual(new[] { "xmp-keyword" }, rewrittenFile.Xmp.GetKeywords());

    var rewrittenBoxes = rewrittenFile._reader._readBoxes();
    var rewrittenMdat = rewrittenBoxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var rewrittenCo64 = rewrittenBoxes.Single(x => x.Box.Type == IsoBmffTypes.Co64);

    Assert.AreEqual(
      checked((ulong)rewrittenMdat.Box.DataOffset),
      rewrittenFile._reader.ReadUInt64BigEndian(rewrittenCo64.Box.DataOffset + 8));
    Assert.AreEqual(
      checked((ulong)rewrittenMdat.Box.DataOffset + 8),
      rewrittenFile._reader.ReadUInt64BigEndian(rewrittenCo64.Box.DataOffset + 16));
  }

  [TestMethod]
  public void Save_InPlace_PersistsItemListAndXmpKeywords() {
    using var stream = _createTestFileWithXmp();
    var originalLength = stream.Length;
    var file = new IsoBmffFile(stream);

    file._metadata.SetKeywords("box-keyword");
    file.Xmp.SetKeywords(["xmp-keyword"]);

    file.Save();

    Assert.AreEqual(originalLength, stream.Length);

    stream.Position = 0;
    var savedFile = new IsoBmffFile(stream);

    Assert.AreEqual("box-keyword", savedFile._metadata.GetItemListKeywords());
    CollectionAssert.AreEqual(new[] { "xmp-keyword" }, savedFile.Xmp.GetKeywords());
  }

  [TestMethod]
  public void Save_InPlace_PersistsKeysKeywordsAndComment() {
    using var stream = IsoBmffFileTests._createTestFile(
      beforeFreeSize: 0,
      afterFreeSize: 16384,
      withItemList: false);
    using var file = new IsoBmffFile(stream);

    file._metadata.SetKeysKeywords("keys-keyword");
    file._metadata.SetKeysComment("keys-comment");
    file.Save();

    Assert.AreEqual("keys-keyword", file._metadata.GetKeysKeywords());
    Assert.AreEqual("keys-comment", file._metadata.GetKeysComment());

    var moovBox = file._reader.Find(IsoBmffTypes.Moov)
      ?? throw new InvalidDataException("The moov box was not found.");
    var metaBox = file._reader.FindChild(moovBox, IsoBmffTypes.Meta)
      ?? throw new InvalidDataException("The meta box was not written.");
    var keysBox = file._reader.FindChild(
      metaBox,
      IsoBmffTypes.Keys,
      file._reader.GetBoxChildrenOffset(metaBox))
      ?? throw new InvalidDataException("The keys box was not written.");

    Assert.AreEqual(2u, file._reader.ReadUInt32BigEndian(keysBox.DataOffset + 4));
  }

  [TestMethod]
  public void Save_Path_FullRewriteCreatesXmpAndRelocatesCo64() {
    using var source = IsoBmffFileTests._createTestFile(
      beforeFreeSize: 0,
      afterFreeSize: 0,
      withItemList: false);

    _setCo64Offsets(source);

    var originalLength = source.Length;
    var path = Path.Combine(Path.GetTempPath(), $"isobmff-{Guid.NewGuid():N}.mp4");

    try {
      source.Position = 0;

      using (var output = File.Create(path))
        source.CopyTo(output);

      using (var file = new IsoBmffFile(path)) {
        Assert.IsNull(file._metadata._getItemList());
        Assert.IsNull(file._reader.Find(IsoBmffTypes.Uuid));

        file._metadata.SetKeywords("box-keyword");
        file.Xmp.SetKeywords(["xmp-keyword"]);
        file.Save();
      }

      Assert.IsTrue(new FileInfo(path).Length > originalLength);

      using var rewrittenFile = new IsoBmffFile(path);
      var boxes = rewrittenFile._reader._readBoxes();
      var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
      var co64 = boxes.Single(x => x.Box.Type == IsoBmffTypes.Co64);

      Assert.AreEqual("box-keyword", rewrittenFile._metadata.GetItemListKeywords());
      CollectionAssert.AreEqual(new[] { "xmp-keyword" }, rewrittenFile.Xmp.GetKeywords());
      Assert.AreEqual(
        checked((ulong)mdat.Box.DataOffset),
        rewrittenFile._reader.ReadUInt64BigEndian(co64.Box.DataOffset + 8));
      Assert.AreEqual(
        checked((ulong)mdat.Box.DataOffset + 8),
        rewrittenFile._reader.ReadUInt64BigEndian(co64.Box.DataOffset + 16));
    }
    finally {
      if (File.Exists(path))
        File.Delete(path);
    }
  }

  [TestMethod]
  public void Save_Path_PersistsItemListAndXmpKeywords() {
    using var source = _createTestFileWithXmp();
    var path = Path.Combine(Path.GetTempPath(), $"isobmff-{Guid.NewGuid():N}.mp4");

    try {
      using (var destination = File.Create(path))
        source.CopyTo(destination);

      using (var file = new IsoBmffFile(path)) {
        file._metadata.SetKeywords("box-keyword");
        file.Xmp.SetKeywords(["xmp-keyword"]);
        file.Save();
      }

      using var savedFile = new IsoBmffFile(path);

      Assert.AreEqual("box-keyword", savedFile._metadata.GetItemListKeywords());
      CollectionAssert.AreEqual(new[] { "xmp-keyword" }, savedFile.Xmp.GetKeywords());
    }
    finally {
      if (File.Exists(path))
        File.Delete(path);
    }
  }

  [TestMethod]
  public void Save_Path_MovesXmp_AppliesMultipleEditsAndPreservesUnchangedBytes() {
    using var source = _createTestFileWithXmpBeforeFree();
    var path = Path.Combine(Path.GetTempPath(), $"isobmff-{Guid.NewGuid():N}.mp4");

    try {
      using (var output = File.Create(path))
        source.CopyTo(output);

      var originalBytes = File.ReadAllBytes(path);
      var originalMdatBox = new IsoBmffReader(source).Find(IsoBmffTypes.Mdat)
        ?? throw new InvalidDataException("Test mdat box was not found.");
      var originalMdatBytes = originalBytes.AsSpan(
        checked((int)originalMdatBox.Offset),
        checked((int)originalMdatBox.Size)).ToArray();
      var originalFtypSize = new IsoBmffReader(source).Find(IsoBmffTypes.Ftyp)?.Size
        ?? throw new InvalidDataException("Test ftyp box was not found.");

      using (var file = new IsoBmffFile(path)) {
        var originalBoxes = file._metadata._getOriginalBoxes();
        var originalPlanner = new IsoBmffLayoutPlanner(
          originalBoxes,
          originalBoxes,
          file._reader.GetBoxChildrenOffset);
        var original = originalPlanner.CreateOriginalLayout();
        var originalXmp = originalBoxes.Single(x => x.Box.Type == IsoBmffTypes.Uuid);

        file._metadata.SetKeywords("intermediate-box-keyword");
        file._metadata.SetKeywords("box-keyword");
        file.Xmp.SetKeywords(["intermediate-xmp-keyword"]);
        file.Xmp.SetKeywords(["xmp-keyword"]);

        var editedBoxes = file._getEditedBoxes();
        var planner = new IsoBmffLayoutPlanner(
          originalBoxes,
          editedBoxes,
          file._reader.GetBoxChildrenOffset);
        var plan = planner.CreateEditedLayout(original, editedBoxes);

        Assert.IsFalse(plan.RequiresFullRewrite);

        var editedXmp = editedBoxes.Single(x => x.Box.Type == IsoBmffTypes.Uuid);
        var plannedXmp = plan.Layout[editedXmp];
        var diff = IsoBmffLayoutDiffer.Diff(
          original,
          plan.Layout,
          editedBoxes.Where(x => x.Data is not null));

        Assert.AreNotEqual(originalXmp.Box.Offset, plannedXmp.Offset);
        Assert.IsTrue(diff.Moves.Any(x =>
          x.SourceOffset == originalXmp.Box.Offset &&
          x.DestinationOffset == plannedXmp.Offset));

        file.Save();
      }

      var rewrittenBytes = File.ReadAllBytes(path);
      var rewrittenFile = new IsoBmffFile(path);
      var rewrittenBoxes = rewrittenFile._reader._readBoxes();
      var rewrittenMdat = rewrittenBoxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
      var rewrittenFtyp = rewrittenBoxes.Single(x => x.Box.Type == IsoBmffTypes.Ftyp);

      Assert.AreEqual(originalMdatBox.Offset, rewrittenMdat.Box.Offset);
      Assert.AreEqual(originalFtypSize, rewrittenFtyp.Box.Size);
      CollectionAssert.AreEqual(
        originalBytes.AsSpan(0, checked((int)originalFtypSize)).ToArray(),
        rewrittenBytes.AsSpan(0, checked((int)rewrittenFtyp.Box.Size)).ToArray());
      CollectionAssert.AreEqual(
        originalMdatBytes,
        rewrittenBytes.AsSpan(
          checked((int)rewrittenMdat.Box.Offset),
          checked((int)rewrittenMdat.Box.Size)).ToArray());

      Assert.AreEqual("box-keyword", rewrittenFile._metadata.GetItemListKeywords());
      CollectionAssert.AreEqual(new[] { "xmp-keyword" }, rewrittenFile.Xmp.GetKeywords());
      rewrittenFile.Dispose();
    }
    finally {
      if (File.Exists(path))
        File.Delete(path);
    }
  }

  [TestMethod]
  public void FindXmp_SkipsUuidBoxesWithOtherUserTypes() {
    using var input = _createTestFileWithOtherUuidBeforeXmp();
    var reader = new IsoBmffReader(input);
    var uuidBoxes = reader._readBoxes()
      .Where(x => x.Box.Type == IsoBmffTypes.Uuid)
      .ToArray();

    Assert.AreEqual(2, uuidBoxes.Length);

    var entry = IsoBmffXmp.Find(input, reader);

    Assert.IsNotNull(entry);
    Assert.AreEqual(uuidBoxes[1].Box.Offset, entry!.Item!.Value.Offset);
    StringAssert.Contains(entry.Value, "initial-xmp");
  }

  private static MemoryStream _createTestFileWithOtherUuidBeforeXmp() {
    using var source = _createTestFileWithXmp();
    var reader = new IsoBmffReader(source);
    var xmpBox = reader.Find(IsoBmffTypes.Uuid)
      ?? throw new InvalidDataException("Test XMP uuid box was not found.");
    var sourceBytes = source.ToArray();
    var xmpOffset = checked((int)xmpBox.Offset);
    var otherUuidData = new byte[20];

    for (var i = 0; i < 16; i++)
      otherUuidData[i] = (byte)i;

    var result = new MemoryStream();

    result.Write(sourceBytes.AsSpan(0, xmpOffset));
    _writeBox(result, IsoBmffTypes.Uuid, otherUuidData);
    result.Write(sourceBytes.AsSpan(xmpOffset));

    result.Position = 0;
    return result;
  }

  private static MemoryStream _createTestFileWithXmpBeforeFree() {
    using var baseStream = IsoBmffFileTests._createTestFile(
      beforeFreeSize: 0,
      afterFreeSize: 0,
      withItemList: false);

    var reader = new IsoBmffReader(baseStream);
    var mdat = reader.Find(IsoBmffTypes.Mdat)
      ?? throw new InvalidDataException("Test mdat box was not found.");
    var baseBytes = baseStream.ToArray();

    var initialXmp = new XmpMetadata(null);
    initialXmp.SetKeywords(["initial-xmp"]);

    var packet = initialXmp.ToPacket();
    var xmpPayload = new byte[16 + packet.Length];

    new byte[] {
      0xBE, 0x7A, 0xCF, 0xCB, 0x97, 0xA9, 0x42, 0xE8,
      0x9C, 0x71, 0x99, 0x94, 0x91, 0xE3, 0xAF, 0xAC
    }.CopyTo(xmpPayload, 0);

    packet.CopyTo(xmpPayload, 16);

    var result = new MemoryStream();
    var mdatOffset = checked((int)mdat.Offset);

    result.Write(baseBytes.AsSpan(0, mdatOffset));
    _writeBox(result, IsoBmffTypes.Uuid, xmpPayload);
    _writeBox(result, IsoBmffTypes.Free, new byte[128 * 1024 - 8]);
    result.Write(baseBytes.AsSpan(mdatOffset));

    result.Position = 0;
    return result;
  }

  private static void _setCo64Offsets(Stream stream) {
    var reader = new IsoBmffReader(stream);
    var boxes = reader._readBoxes();
    var co64 = boxes.Single(x => x.Box.Type == IsoBmffTypes.Co64);
    var mdat = boxes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var data = new byte[checked((int)co64.Box.DataSize)];

    BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), 2);
    BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(8), checked((ulong)mdat.Box.DataOffset));
    BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(16), checked((ulong)mdat.Box.DataOffset + 8));

    stream.Position = co64.Box.DataOffset;
    stream.Write(data);
    stream.Position = 0;
  }

  private static MemoryStream _createTestFileWithXmp() {
    using var baseStream = IsoBmffFileTests._createTestFile(
      beforeFreeSize: 0,
      afterFreeSize: 8192,
      withItemList: false);

    var baseReader = new IsoBmffReader(baseStream);
    var mdat = baseReader.Find(IsoBmffTypes.Mdat)
      ?? throw new InvalidDataException("Test mdat box was not found.");
    var baseBytes = baseStream.ToArray();

    var initialXmp = new XmpMetadata(null);
    initialXmp.SetKeywords(["initial-xmp"]);

    var xmpPacket = initialXmp.ToPacket();
    var xmpPayload = new byte[16 + xmpPacket.Length];

    new byte[] {
      0xBE, 0x7A, 0xCF, 0xCB, 0x97, 0xA9, 0x42, 0xE8,
      0x9C, 0x71, 0x99, 0x94, 0x91, 0xE3, 0xAF, 0xAC
    }.CopyTo(xmpPayload, 0);

    xmpPacket.CopyTo(xmpPayload, 16);

    var result = new MemoryStream();
    var mdatOffset = checked((int)mdat.Offset);

    result.Write(baseBytes.AsSpan(0, mdatOffset));
    _writeBox(result, IsoBmffTypes.Uuid, xmpPayload);
    _writeBox(result, IsoBmffTypes.Free, new byte[8192 - 8]);
    result.Write(baseBytes.AsSpan(mdatOffset));

    result.Position = 0;
    return result;
  }

  private static void _writeBox(Stream stream, uint type, byte[] payload) {
    Span<byte> header = stackalloc byte[8];

    BinaryPrimitives.WriteUInt32BigEndian(header, checked((uint)(8 + payload.Length)));
    BinaryPrimitives.WriteUInt32BigEndian(header[4..], type);

    stream.Write(header);
    stream.Write(payload);
  }
}