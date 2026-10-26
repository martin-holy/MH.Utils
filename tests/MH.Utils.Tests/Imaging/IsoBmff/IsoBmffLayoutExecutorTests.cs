using MH.Utils.Imaging.IsoBmff;
using MH.Utils.IO;
using System.Buffers.Binary;

namespace MH.Utils.Tests.Imaging.IsoBmff;

[TestClass]
public class IsoBmffLayoutExecutorTests {
  [TestMethod]
  public void MoveUp_NonOverlapping() =>
    _assertMove(100, 60, 20, 20);

  [TestMethod]
  public void MoveUp_Overlapping() =>
    _assertMove(100, 20, 0, 60);

  [TestMethod]
  public void MoveDown_NonOverlapping() =>
    _assertMove(100, 20, 60, 20);

  [TestMethod]
  public void MoveDown_Overlapping() =>
    _assertMove(100, 0, 20, 60);

  [TestMethod]
  public void MoveUp_LargerThanBuffer() =>
    _assertMove(100 + 64 * 1024 + 123, 100, 0, 64 * 1024 + 123);

  [TestMethod]
  public void MoveDown_LargerThanBuffer() =>
    _assertMove(100 + 64 * 1024 + 123, 0, 100, 64 * 1024 + 123);

  [TestMethod]
  public void Move_ZeroLength_DoesNothing() {
    var data = _createData(100);
    var original = data.ToArray();

    using var stream = new MemoryStream(data);

    BinaryInPlaceWriter.ExecuteMoves(stream, [new BinaryMove(20, 60, 0)]);

    CollectionAssert.AreEqual(original, data);
  }

  [TestMethod]
  public void Move_SameOffset_DoesNothing() {
    var data = _createData(100);
    var original = data.ToArray();

    using var stream = new MemoryStream(data);

    BinaryInPlaceWriter.ExecuteMoves(stream, [new BinaryMove(20, 20, 40)]);

    CollectionAssert.AreEqual(original, data);
  }

  [TestMethod]
  public void ExecuteMoves_ExecutesMultipleMovesInOrder() {
    var data = _createData(100);
    var expected = data.ToArray();

    using var stream = new MemoryStream(data);

    var moves = new[] {
      new BinaryMove(0, 20, 20),
      new BinaryMove(60, 40, 20),
      new BinaryMove(80, 10, 10)
    };

    foreach (var move in moves)
      _applyMove(expected, move);

    BinaryInPlaceWriter.ExecuteMoves(stream, moves);

    CollectionAssert.AreEqual(expected, data);
  }

  [TestMethod]
  public void ExecuteWrites_WritesBytesAtSpecifiedOffsets() {
    var data = _createData(100);
    var expected = data.ToArray();

    var writes = new[] {
      new BinaryWrite(10, [0xAA, 0xBB, 0xCC]),
      new BinaryWrite(70, [0x11, 0x22])
    };

    foreach (var write in writes)
      Array.Copy(write.Data, 0, expected, (int)write.Offset, write.Data.Length);

    using var stream = new MemoryStream(data);

    BinaryInPlaceWriter.ExecuteWrites(stream, writes);

    CollectionAssert.AreEqual(expected, data);
    Assert.AreEqual(100, stream.Length);
  }

  [TestMethod]
  public void FullRewrite_RelocatesStcoAndCo64Entries() {
    const int mdatPayloadSize = 16;
    const int stcoSize = 20;
    const int co64Size = 24;
    const long mdatOffset = stcoSize + co64Size;
    const ulong originalMdatDataOffset = (ulong)(mdatOffset + 8);

    var stcoData = new byte[12];
    BinaryPrimitives.WriteUInt32BigEndian(stcoData.AsSpan(4), 1);
    BinaryPrimitives.WriteUInt32BigEndian(stcoData.AsSpan(8), (uint)originalMdatDataOffset);

    var co64Data = new byte[16];
    BinaryPrimitives.WriteUInt32BigEndian(co64Data.AsSpan(4), 1);
    BinaryPrimitives.WriteUInt64BigEndian(co64Data.AsSpan(8), originalMdatDataOffset);

    using var source = new MemoryStream();
    _writeBox(source, IsoBmffTypes.Stco, stcoData);
    _writeBox(source, IsoBmffTypes.Co64, co64Data);
    _writeBox(source, IsoBmffTypes.Mdat, _createData(mdatPayloadSize));
    source.Position = 0;

    var reader = new IsoBmffReader(source);
    var originalNodes = reader._readNodes();
    var stco = originalNodes.Single(x => x.Box.Type == IsoBmffTypes.Stco);
    var co64 = originalNodes.Single(x => x.Box.Type == IsoBmffTypes.Co64);
    var mdat = originalNodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat);
    var editedNodes = originalNodes.ToList();
    var free = new IsoBmffNode(new IsoBmffBox(-1, 8, 8, IsoBmffTypes.Free), -1);

    editedNodes.Insert(editedNodes.IndexOf(mdat), free);

    var layoutBoxes = originalNodes
      .Select(x => new IsoBmffLayoutBox(
        x,
        x.Box.Offset + (ReferenceEquals(x, mdat) ? 8 : 0),
        x.Box.Size))
      .Append(new IsoBmffLayoutBox(free, mdat.Box.Offset, 8));
    var layout = new IsoBmffLayout(layoutBoxes);

    using var destination = new MemoryStream();

    IsoBmffLayoutExecutor.ExecuteFullRewrite(
      source,
      destination,
      originalNodes,
      editedNodes,
      layout,
      reader.GetBoxChildrenOffset);

    var rewrittenReader = new IsoBmffReader(destination);
    var rewrittenNodes = rewrittenReader._readNodes();
    var rewrittenStco = rewrittenNodes.Single(x => x.Box.Type == IsoBmffTypes.Stco).Box;
    var rewrittenCo64 = rewrittenNodes.Single(x => x.Box.Type == IsoBmffTypes.Co64).Box;
    var rewrittenMdat = rewrittenNodes.Single(x => x.Box.Type == IsoBmffTypes.Mdat).Box;
    var expectedOffset = checked((ulong)rewrittenMdat.DataOffset);

    Assert.AreEqual(source.Length + 8, destination.Length);
    Assert.AreEqual(
      (uint)expectedOffset,
      rewrittenReader.ReadUInt32BigEndian(rewrittenStco.DataOffset + 8));
    Assert.AreEqual(
      expectedOffset,
      rewrittenReader.ReadUInt64BigEndian(rewrittenCo64.DataOffset + 8));

    var expectedMdatPayload = _createData(mdatPayloadSize);
    var actualMdatPayload = new byte[mdatPayloadSize];

    destination.Position = rewrittenMdat.DataOffset;
    destination.ReadExactly(actualMdatPayload);

    CollectionAssert.AreEqual(expectedMdatPayload, actualMdatPayload);
  }

  private static void _writeBox(Stream stream, uint type, byte[] payload) {
    Span<byte> header = stackalloc byte[8];

    BinaryPrimitives.WriteUInt32BigEndian(header, checked((uint)(8 + payload.Length)));
    BinaryPrimitives.WriteUInt32BigEndian(header[4..], type);

    stream.Write(header);
    stream.Write(payload);
  }

  private static void _assertMove(int dataLength, int source, int destination, int length) {
    var data = _createData(dataLength);
    var original = data.ToArray();
    var expected = original.ToArray();

    _applyMove(expected, new BinaryMove(source, destination, length));

    using var stream = new MemoryStream(data);

    BinaryInPlaceWriter.ExecuteMoves(stream, [new BinaryMove(source, destination, length)]);

    CollectionAssert.AreEqual(expected, data);
  }

  private static void _applyMove(byte[] data, BinaryMove move) =>
    Array.Copy(data, (int)move.SourceOffset, data, (int)move.DestinationOffset, (int)move.Length);

  private static byte[] _createData(int length) {
    var data = new byte[length];

    for (var i = 0; i < data.Length; i++)
      data[i] = (byte)(i % 251);

    return data;
  }
}