using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MH.Utils.IO;

public readonly record struct BinaryMove(long SourceOffset, long DestinationOffset, long Length) {
  public bool IsRightward => DestinationOffset > SourceOffset;
  public bool IsLeftward => DestinationOffset < SourceOffset;
}

public readonly record struct BinaryWrite(long Offset, byte[] Data);

public interface IBinaryWritePlan {
  bool RequiresFullRewrite { get; }
  internal IReadOnlyList<BinaryMove> Moves { get; }
  internal IReadOnlyList<BinaryWrite> Writes { get; }
  internal void WriteFullRewrite(Stream source, Stream destination);
}

public static class BinaryInPlaceWriter {
  private const int _bufferSize = 64 * 1024;

  internal static void ExecuteMoves(Stream stream, IEnumerable<BinaryMove> moves) {
    var buffer = new byte[_bufferSize];

    foreach (var move in _orderMoves(moves))
      _move(stream, move, buffer);
  }

  internal static void ExecuteWrites(Stream stream, IEnumerable<BinaryWrite> writes) {
    foreach (var write in writes.OrderBy(write => write.Offset)) {
      stream.Position = write.Offset;
      stream.Write(write.Data);
    }
  }

  public static void Write(string filePath, IBinaryWritePlan plan) {
    if (plan.RequiresFullRewrite) {
      _writeFullRewrite(filePath, plan);
      return;
    }

    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
    ExecuteMoves(stream, plan.Moves);
    ExecuteWrites(stream, plan.Writes);
    stream.Flush(true);
  }

  public static void Write(Stream stream, IBinaryWritePlan plan) {
    if (!stream.CanRead || !stream.CanWrite || !stream.CanSeek)
      throw new ArgumentException("The stream must support reading, writing, and seeking.", nameof(stream));

    if (plan.RequiresFullRewrite) {
      _writeFullRewrite(stream, plan);
      return;
    }

    ExecuteMoves(stream, plan.Moves);
    ExecuteWrites(stream, plan.Writes);
    stream.Flush();
  }

  private static void _writeFullRewrite(string filePath, IBinaryWritePlan plan) {
    var temporaryPath = _createTemporaryPath(filePath);

    try {
      using (var source = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
      using (var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
        plan.WriteFullRewrite(source, destination);
        destination.Flush(true);
      }

      File.Move(temporaryPath, filePath, true);
    }
    finally {
      _deleteTemporaryFile(temporaryPath);
    }
  }

  private static void _writeFullRewrite(Stream stream, IBinaryWritePlan plan) {
    var temporaryPath = _createTemporaryPath(null);

    try {
      using (var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
        plan.WriteFullRewrite(stream, destination);
        destination.Flush(true);
      }

      using var source = new FileStream(temporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read);
      stream.Position = 0;
      source.CopyTo(stream);
      stream.SetLength(source.Length);
      stream.Flush();
    }
    finally {
      _deleteTemporaryFile(temporaryPath);
    }
  }

  private static BinaryMove[] _orderMoves(IEnumerable<BinaryMove> moves) {
    var allMoves = moves.ToArray();
    return [
      .. allMoves
        .Where(move => move.IsRightward)
        .OrderByDescending(move => move.SourceOffset),
      .. allMoves
        .Where(move => move.IsLeftward)
        .OrderBy(move => move.SourceOffset)];
  }

  private static void _move(Stream stream, BinaryMove move, byte[] buffer) {
    if (move.Length == 0 || move.SourceOffset == move.DestinationOffset) return;

    if (move.IsLeftward) {
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
      stream.ReadExactly(buffer.AsSpan(0, count));

      stream.Position = destination;
      stream.Write(buffer, 0, count);
    }
    finally {
      stream.Position = previous;
    }
  }

  private static string _createTemporaryPath(string? filePath) {
    var directory = filePath is null
      ? Path.GetTempPath()
      : Path.GetDirectoryName(Path.GetFullPath(filePath))!;

    return Path.Combine(directory, $".{Path.GetRandomFileName()}.tmp");
  }

  private static void _deleteTemporaryFile(string path) {
    if (File.Exists(path))
      File.Delete(path);
  }
}