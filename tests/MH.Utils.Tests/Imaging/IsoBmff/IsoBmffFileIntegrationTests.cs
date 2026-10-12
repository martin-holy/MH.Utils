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