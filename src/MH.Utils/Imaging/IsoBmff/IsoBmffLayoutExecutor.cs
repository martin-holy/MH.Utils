using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

  public static void ExecuteWrites(Stream stream, IEnumerable<IsoBmffWrite> writes) {
    foreach (var write in writes) {
      stream.Position = write.Offset;
      stream.Write(write.Data);
    }
  }

  public static void ExecuteFullRewrite(
    Stream source,
    Stream destination,
    IEnumerable<IsoBmffBoxNode> originalBoxes,
    IEnumerable<IsoBmffBoxNode> editedBoxes,
    IsoBmffLayout layout,
    Func<IsoBmffBox, long> getBoxChildrenOffset) {

    if (ReferenceEquals(source, destination))
      throw new ArgumentException("Full rewrite requires separate source and destination streams.", nameof(destination));

    if (!source.CanRead || !source.CanSeek)
      throw new ArgumentException("Source stream must be readable and seekable.", nameof(source));

    if (!destination.CanWrite || !destination.CanSeek)
      throw new ArgumentException("Destination stream must be writable and seekable.", nameof(destination));

    var original = originalBoxes.ToList();
    var edited = editedBoxes.ToList();

    destination.SetLength(0);
    destination.Position = 0;

    foreach (var box in layout.TopLevelBoxes)
      _writeBox(source, destination, box.Node, original, edited, layout, getBoxChildrenOffset);

    var finalLength = layout.TopLevelBoxes.LastOrDefault()?.End ?? 0;
    destination.SetLength(finalLength);
  }

  private static void _writeBox(
    Stream source,
    Stream destination,
    IsoBmffBoxNode node,
    List<IsoBmffBoxNode> originalBoxes,
    List<IsoBmffBoxNode> editedBoxes,
    IsoBmffLayout layout,
    Func<IsoBmffBox, long> getBoxChildrenOffset) {

    var box = layout[node];

    if (destination.Position != box.Offset)
      throw new InvalidDataException("Planned boxes are not contiguous.");

    destination.Write(IsoBmffLayoutDiffer._createHeader(box));

    var editedIndex = editedBoxes.IndexOf(node);
    var children = editedIndex < 0
      ? []
      : editedBoxes.Where(x => x.Parent == editedIndex).ToList();

    if (children.Count > 0 || IsoBmffReader._isContainer(node.Box.Type)) {
      _writeContainer(
        source,
        destination,
        node,
        children,
        originalBoxes,
        editedBoxes,
        layout,
        getBoxChildrenOffset);
    }
    else {
      _writeLeaf(source, destination, node, box, originalBoxes, layout);
    }

    if (destination.Position != box.End)
      throw new InvalidDataException($"Written size does not match the planned size for {node.Box.TypeName}.");
  }

  private static void _writeContainer(
    Stream source,
    Stream destination,
    IsoBmffBoxNode node,
    List<IsoBmffBoxNode> children,
    List<IsoBmffBoxNode> originalBoxes,
    List<IsoBmffBoxNode> editedBoxes,
    IsoBmffLayout layout,
    Func<IsoBmffBox, long> getBoxChildrenOffset) {

    var originalNode = _findOriginalNode(originalBoxes, node);
    long originalPrefixSize = 0;

    if (node.Data is { } data) {
      destination.Write(data);
      originalPrefixSize = originalNode is null
        ? 0
        : getBoxChildrenOffset(originalNode.Box);
    }
    else if (originalNode is not null) {
      originalPrefixSize = getBoxChildrenOffset(originalNode.Box);
      _copyRange(source, destination, originalNode.Box.DataOffset, originalPrefixSize);
    }

    foreach (var child in children)
      _writeBox(source, destination, child, originalBoxes, editedBoxes, layout, getBoxChildrenOffset);

    if (originalNode is not null) {
      var originalIndex = originalBoxes.IndexOf(originalNode);
      var originalChildrenSize = originalBoxes
        .Where(x => x.Parent == originalIndex)
        .Sum(x => x.Box.Size);
      var tailSize = originalNode.Box.DataSize - originalPrefixSize - originalChildrenSize;

      if (tailSize < 0)
        throw new InvalidDataException($"Invalid original child layout for {node.Box.TypeName}.");

      _copyRange(
        source,
        destination,
        originalNode.Box.End - tailSize,
        tailSize);
    }
  }

  private static void _writeLeaf(
    Stream source,
    Stream destination,
    IsoBmffBoxNode node,
    IsoBmffLayoutBox box,
    List<IsoBmffBoxNode> originalBoxes,
    IsoBmffLayout layout) {

    var originalNode = _findOriginalNode(originalBoxes, node);

    if (node.Box.Type is IsoBmffTypes.Stco or IsoBmffTypes.Co64) {
      _writeChunkOffsetTable(source, destination, node, box, originalNode, originalBoxes, layout);
      return;
    }

    var dataSize = box.Size - box.HeaderSize;

    if (node.Data is { } data) {
      if (data.LongLength != dataSize)
        throw new InvalidDataException($"Replacement data size does not match {node.Box.TypeName}.");

      destination.Write(data);
      return;
    }

    if (node.Box.Type == IsoBmffTypes.Free) {
      _writeZeros(destination, dataSize);
      return;
    }

    if (originalNode is null)
      throw new InvalidDataException($"New leaf box {node.Box.TypeName} has no data to write.");

    if (originalNode.Box.DataSize != dataSize)
      throw new InvalidDataException($"Changed leaf box {node.Box.TypeName} requires replacement data.");

    _copyRange(source, destination, originalNode.Box.DataOffset, dataSize);
  }

  private static void _writeChunkOffsetTable(
    Stream source,
    Stream destination,
    IsoBmffBoxNode node,
    IsoBmffLayoutBox box,
    IsoBmffBoxNode? originalNode,
    List<IsoBmffBoxNode> originalBoxes,
    IsoBmffLayout layout) {

    var data = node.Data;
    var dataSize = box.Size - box.HeaderSize;

    if (data is null && originalNode is null)
      throw new InvalidDataException($"New {node.Box.TypeName} box has no table data.");

    var sourceDataSize = data?.LongLength ?? originalNode!.Box.DataSize;

    if (sourceDataSize != dataSize || dataSize < 8)
      throw new InvalidDataException($"Invalid data size for {node.Box.TypeName}.");

    var prefix = new byte[8];
    _readTableBytes(source, originalNode?.Box, data, 0, prefix);

    var entryCount = BinaryPrimitives.ReadUInt32BigEndian(prefix.AsSpan(4));
    var entrySize = node.Box.Type == IsoBmffTypes.Stco ? 4 : 8;
    var entriesEnd = 8UL + (ulong)entryCount * (uint)entrySize;

    if (entriesEnd > (ulong)dataSize)
      throw new InvalidDataException($"Invalid entry count in {node.Box.TypeName}.");

    destination.Write(prefix);

    const int bufferSize = 64 * 1024;
    var buffer = new byte[bufferSize];
    var entriesPerBuffer = buffer.Length / entrySize;
    var entryIndex = 0U;
    var dataOffset = 8L;

    while (entryIndex < entryCount) {
      var count = (int)Math.Min((uint)entriesPerBuffer, entryCount - entryIndex);
      var byteCount = count * entrySize;
      var chunk = buffer.AsSpan(0, byteCount);

      _readTableBytes(source, originalNode?.Box, data, dataOffset, chunk);

      for (var i = 0; i < count; i++) {
        var entry = chunk.Slice(i * entrySize, entrySize);

        if (entrySize == 4) {
          var oldOffset = BinaryPrimitives.ReadUInt32BigEndian(entry);
          var newOffset = _relocateChunkOffset(oldOffset, originalBoxes, layout);

          if (newOffset > uint.MaxValue)
            throw new InvalidDataException("Relocated stco offset exceeds its 32-bit range.");

          BinaryPrimitives.WriteUInt32BigEndian(entry, (uint)newOffset);
        }
        else {
          var oldOffset = BinaryPrimitives.ReadUInt64BigEndian(entry);
          var newOffset = _relocateChunkOffset(oldOffset, originalBoxes, layout);

          BinaryPrimitives.WriteUInt64BigEndian(entry, newOffset);
        }
      }

      destination.Write(chunk);
      entryIndex += (uint)count;
      dataOffset += byteCount;
    }

    var trailingSize = dataSize - checked((long)entriesEnd);

    while (trailingSize > 0) {
      var count = (int)Math.Min(buffer.Length, trailingSize);
      var chunk = buffer.AsSpan(0, count);

      _readTableBytes(source, originalNode?.Box, data, dataOffset, chunk);
      destination.Write(chunk);

      dataOffset += count;
      trailingSize -= count;
    }
  }

  private static ulong _relocateChunkOffset(ulong sourceOffset, List<IsoBmffBoxNode> originalBoxes, IsoBmffLayout layout) {
    foreach (var originalBox in originalBoxes) {
      if (originalBox.Parent >= 0 || originalBox.Box.Offset < 0)
        continue;

      var boxStart = (ulong)originalBox.Box.Offset;
      var boxEnd = (ulong)originalBox.Box.End;

      if (sourceOffset < boxStart || sourceOffset >= boxEnd)
        continue;

      if (!layout.TryGetValue(originalBox, out var editedBox))
        throw new InvalidDataException("A chunk offset points into a removed top-level box.");

      var relativeOffset = sourceOffset - boxStart;

      if (relativeOffset >= (ulong)editedBox!.Size)
        throw new InvalidDataException("A chunk offset falls outside its relocated box.");

      return checked((ulong)editedBox.Offset + relativeOffset);
    }

    // The offset is not inside a top-level box in this file; preserve it.
    // This permits offsets associated with external data references.
    return sourceOffset;
  }

  private static void _readTableBytes(Stream source, IsoBmffBox? sourceBox, byte[]? replacementData, long offset, Span<byte> destination) {
    if (replacementData is not null) {
      replacementData.AsSpan(checked((int)offset), destination.Length).CopyTo(destination);
      return;
    }

    source.Position = sourceBox!.Value.DataOffset + offset;
    source.ReadExactly(destination);
  }

  private static IsoBmffBoxNode? _findOriginalNode(List<IsoBmffBoxNode> originalBoxes, IsoBmffBoxNode node) =>
    originalBoxes.FirstOrDefault(x => ReferenceEquals(x.Identity, node.Identity));

  private static void _copyRange(Stream source, Stream destination, long sourceOffset, long length) {
    if (length < 0)
      throw new InvalidDataException("Cannot copy a negative byte range.");

    const int bufferSize = 64 * 1024;
    var buffer = new byte[bufferSize];
    var copied = 0L;

    while (copied < length) {
      var count = (int)Math.Min(buffer.Length, length - copied);

      source.Position = sourceOffset + copied;
      var read = 0;

      while (read < count) {
        var readCount = source.Read(buffer, read, count - read);

        if (readCount == 0)
          throw new EndOfStreamException();

        read += readCount;
      }

      destination.Write(buffer, 0, count);
      copied += count;
    }
  }

  private static void _writeZeros(Stream destination, long length) {
    if (length < 0)
      throw new InvalidDataException("Cannot write a negative byte range.");

    var buffer = new byte[64 * 1024];

    while (length > 0) {
      var count = (int)Math.Min(buffer.Length, length);
      destination.Write(buffer, 0, count);
      length -= count;
    }
  }
}