using System;
using System.Collections.Generic;
using System.IO;

namespace MH.Utils.Imaging.IsoBmff;

internal static class IsoBmffLayoutExecutor {
  public static void ExecuteMoves(Stream stream, IEnumerable<IsoBmffMove> moves) {
    foreach (var move in moves)
      _move(stream, move);
  }

  private static void _move(Stream stream, IsoBmffMove move) {
    if (move.Length == 0 || move.SourceOffset == move.DestinationOffset)
      return;

    const int bufferSize = 64 * 1024;

    var buffer = new byte[(int)Math.Min(bufferSize, move.Length)];

    if (move.DestinationOffset < move.SourceOffset) {
      // Move up: start at the beginning.
      var position = 0L;

      while (position < move.Length) {
        var count = (int)Math.Min(buffer.Length, move.Length - position);

        _copyChunk(
          stream,
          move.SourceOffset + position,
          move.DestinationOffset + position,
          buffer,
          count);

        position += count;
      }
    }
    else {
      // Move down: start at the end.
      var position = move.Length;

      while (position > 0) {
        var count = (int)Math.Min(buffer.Length, position);

        position -= count;

        _copyChunk(
          stream,
          move.SourceOffset + position,
          move.DestinationOffset + position,
          buffer,
          count);
      }
    }
  }

  private static void _copyChunk(Stream stream, long source, long destination, byte[] buffer, int count) {
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
}