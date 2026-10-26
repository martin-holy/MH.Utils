using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MH.Utils.IO;

public static class BinaryInPlaceWriter {
  public static void ExecuteMoves(Stream stream, IEnumerable<BinaryMove> moves) {
    foreach (var move in _orderMoves(moves))
      _move(stream, move);
  }

  public static void ExecuteWrites(Stream stream, IEnumerable<BinaryWrite> writes) {
    foreach (var write in writes.OrderBy(x => x.Offset)) {
      stream.Position = write.Offset;
      stream.Write(write.Data, 0, write.Data.Length);
    }
  }

  // TODO change it to open its own stream after IsoBmffFile is changed to only use the stream for reading.
  public static Stream Write(Stream source, string filePath, IBinaryWritePlan plan) {
    if (!plan.RequiresFullRewrite) {
      _writeToStream(source, source, plan);
      source.Flush();
      return source;
    }

    var directory = Path.GetDirectoryName(filePath)!;
    var tmpPath = _createTemporaryPath(directory, Path.GetFileName(filePath));

    try {
      using (var tmp = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)) {
        _writeToStream(source, tmp, plan);
        tmp.Flush(flushToDisk: true);
      }

      source.Dispose();

      try {
        File.Move(tmpPath, filePath, overwrite: true);
      }
      catch {
        // BUG: If the move fails, we should still return a stream to the original file so that the caller can continue to use it.
        source = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        throw;
      }

      return new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
    }
    finally {
      _deleteTemporaryFile(tmpPath);
    }
  }

  public static void Write(Stream source, IBinaryWritePlan writePlan) {
    if (!writePlan.RequiresFullRewrite) {
      _writeToStream(source, source, writePlan);
      source.Flush();
      return;
    }

    var tmpPath = _createTemporaryPath(Path.GetTempPath(), "mh_utils_BinaryInPlaceWriter");

    try {
      using var tmp = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
      _writeToStream(source, tmp, writePlan);
      tmp.Flush(flushToDisk: true);
      tmp.Position = 0;
      source.Position = 0;
      tmp.CopyTo(source);
      source.SetLength(tmp.Length);
      source.Flush();
    }
    finally {
      _deleteTemporaryFile(tmpPath);
    }
  }

  private static string _createTemporaryPath(string directory, string name) =>
    Path.Combine(directory, $".{name}.{Guid.NewGuid():N}.tmp");

  private static void _deleteTemporaryFile(string path) {
    try {
      if (File.Exists(path))
        File.Delete(path);
    }
    catch { }
  }

  private static void _writeToStream(Stream source, Stream destination, IBinaryWritePlan plan) {
    if (plan.RequiresFullRewrite)
      plan.WriteFullRewrite(source, destination);
    else
      _executeInPlace(destination, plan);
  }

  private static void _executeInPlace(Stream stream, IBinaryWritePlan plan) {
    ExecuteMoves(stream, plan.Moves);
    ExecuteWrites(stream, plan.Writes);
  }

  // TODO decide where to validate BinaryMove instances. They are validated on cration, than in WritePlan and than here
  private static IEnumerable<BinaryMove> _orderMoves(IEnumerable<BinaryMove> moves) {
    var allMoves = moves.Where(x => !x.IsEmpty).ToList();

    return allMoves
      .Where(x => x.DestinationOffset > x.SourceOffset)
      .OrderByDescending(x => x.SourceOffset)
      .Concat(allMoves
        .Where(x => x.DestinationOffset < x.SourceOffset)
        .OrderBy(x => x.SourceOffset));
  }

  // TODO decide where to validate BinaryMove instances. They are validated on cration, than in WritePlan and than here
  private static void _move(Stream stream, BinaryMove move) {
    if (move.Length == 0 || move.SourceOffset == move.DestinationOffset)
      return;

    const int bufferSize = 64 * 1024;
    var buffer = new byte[(int)Math.Min(bufferSize, move.Length)];

    if (move.DestinationOffset < move.SourceOffset) {
      var position = 0L;

      while (position < move.Length) {
        var count = (int)Math.Min(buffer.Length, move.Length - position);
        _copyChunk(stream, move.SourceOffset + position, move.DestinationOffset + position, buffer, count);
        position += count;
      }
    }
    else {
      var position = move.Length;

      while (position > 0) {
        var count = (int)Math.Min(buffer.Length, position);
        position -= count;
        _copyChunk(stream, move.SourceOffset + position, move.DestinationOffset + position, buffer, count);
      }
    }
  }

  private static void _copyChunk(Stream stream, long source, long destination, byte[] buffer, int count) {
    var previous = stream.Position;

    try {
      stream.Position = source;

      var read = 0;
      while (read < count) {
        var n = stream.Read(buffer, read, count - read);
        if (n == 0) throw new EndOfStreamException();
        read += n;
      }

      stream.Position = destination;
      stream.Write(buffer, 0, count);
    }
    finally {
      stream.Position = previous;
    }
  }
}