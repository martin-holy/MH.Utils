using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace MH.Utils.Imaging;

internal sealed class TiffWriter {
  private readonly MemoryStream _stream;
  private readonly bool _littleEndian;
  private readonly List<DeferredWrite> _deferred = [];

  public TiffWriter(MemoryStream stream, bool littleEndian = false) {
    _littleEndian = littleEndian;
    _stream = stream;
  }

  public void WriteHeader() {
    if (_littleEndian) {
      _stream.WriteByte((byte)'I');
      _stream.WriteByte((byte)'I');
    }
    else {
      _stream.WriteByte((byte)'M');
      _stream.WriteByte((byte)'M');
    }

    WriteUInt16(42);
    WriteUInt32(8);
  }

  public void WriteEntry(ExifEntry entry, ReadOnlySpan<byte> data) {
    WriteUInt16(entry.Tag);
    WriteUInt16(entry.Type);
    WriteUInt32(entry.Count);

    if (data.Length <= 4) {
      Span<byte> value = stackalloc byte[4];
      data.CopyTo(value);
      _stream.Write(value);
      return;
    }

    byte[] bytes = data.ToArray();

    Defer(w => w.WriteBytes(bytes));
  }

  public void Defer(Action<TiffWriter> write) {
    _deferred.Add(new(_stream.Position, write));
    WriteUInt32(0);
  }

  public void FlushDeferred() {
    for (int i = 0; i < _deferred.Count; i++) {
      var item = _deferred[i];
      PatchUInt32((uint)item.PatchPosition, (uint)_stream.Position);
      item.Write(this);
    }

    _deferred.Clear();
  }

  public void WriteBytes(ReadOnlySpan<byte> bytes) =>
    _stream.Write(bytes);

  public void WriteUInt16(ushort value) {
    Span<byte> buffer = stackalloc byte[2];

    if (_littleEndian)
      BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
    else
      BinaryPrimitives.WriteUInt16BigEndian(buffer, value);

    _stream.Write(buffer);
  }

  public void WriteUInt32(uint value) {
    Span<byte> buffer = stackalloc byte[4];

    if (_littleEndian)
      BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
    else
      BinaryPrimitives.WriteUInt32BigEndian(buffer, value);

    _stream.Write(buffer);
  }

  public void PatchUInt32(uint position, uint value) {
    long current = _stream.Position;

    _stream.Position = position;
    WriteUInt32(value);

    _stream.Position = current;
  }

  private sealed class DeferredWrite(long patchPosition, Action<TiffWriter> write) {
    public long PatchPosition = patchPosition;
    public Action<TiffWriter> Write = write;
  }
}