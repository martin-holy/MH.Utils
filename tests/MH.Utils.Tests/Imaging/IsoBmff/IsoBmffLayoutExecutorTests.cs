using MH.Utils.Imaging.IsoBmff;

namespace MH.Utils.Tests.Imaging.IsoBmff;

using Microsoft.VisualStudio.TestTools.UnitTesting;

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

    IsoBmffLayoutExecutor.ExecuteMoves(stream, [new IsoBmffMove(20, 60, 0)]);

    CollectionAssert.AreEqual(original, data);
  }

  [TestMethod]
  public void Move_SameOffset_DoesNothing() {
    var data = _createData(100);
    var original = data.ToArray();

    using var stream = new MemoryStream(data);

    IsoBmffLayoutExecutor.ExecuteMoves(stream, [new IsoBmffMove(20, 20, 40)]);

    CollectionAssert.AreEqual(original, data);
  }

  [TestMethod]
  public void ExecuteMoves_ExecutesMultipleMovesInOrder() {
    var data = _createData(100);
    var expected = data.ToArray();

    using var stream = new MemoryStream(data);

    var moves = new[] {
      new IsoBmffMove(0, 20, 20),
      new IsoBmffMove(60, 40, 20),
      new IsoBmffMove(80, 10, 10)
    };

    foreach (var move in moves)
      _applyMove(expected, move);

    IsoBmffLayoutExecutor.ExecuteMoves(stream, moves);

    CollectionAssert.AreEqual(expected, data);
  }

  [TestMethod]
  public void ExecuteWrites_WritesBytesAtSpecifiedOffsets() {
    var data = _createData(100);
    var expected = data.ToArray();

    var writes = new[] {
      new IsoBmffWrite(10, [0xAA, 0xBB, 0xCC]),
      new IsoBmffWrite(70, [0x11, 0x22])
    };

    foreach (var write in writes)
      Array.Copy(write.Data, 0, expected, (int)write.Offset, write.Data.Length);

    using var stream = new MemoryStream(data);

    IsoBmffLayoutExecutor.ExecuteWrites(stream, writes);

    CollectionAssert.AreEqual(expected, data);
    Assert.AreEqual(100, stream.Length);
  }

  private static void _assertMove(int dataLength, int source, int destination, int length) {
    var data = _createData(dataLength);
    var original = data.ToArray();
    var expected = original.ToArray();

    _applyMove(expected, new IsoBmffMove(source, destination, length));

    using var stream = new MemoryStream(data);

    IsoBmffLayoutExecutor.ExecuteMoves(stream, [new IsoBmffMove(source, destination, length)]);

    CollectionAssert.AreEqual(expected, data);
  }

  private static void _applyMove(byte[] data, IsoBmffMove move) =>
    Array.Copy(data, (int)move.SourceOffset, data, (int)move.DestinationOffset, (int)move.Length);

  private static byte[] _createData(int length) {
    var data = new byte[length];

    for (var i = 0; i < data.Length; i++)
      data[i] = (byte)(i % 251);

    return data;
  }
}