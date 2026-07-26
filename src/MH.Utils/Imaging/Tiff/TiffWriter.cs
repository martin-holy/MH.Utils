using MH.Utils.IO;
using System;
using System.Collections.Generic;
using System.IO;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffWriter(Stream stream, bool littleEndian = false) : BinaryStreamWriter(stream, littleEndian) {
  private readonly List<DeferredReference> _deferred = [];

  public void WriteHeader() {
    if (IsLittleEndian) {
      WriteByte((byte)'I');
      WriteByte((byte)'I');
    }
    else {
      WriteByte((byte)'M');
      WriteByte((byte)'M');
    }

    WriteUInt16(42);
    WriteUInt32(8);
  }

  public void WriteReference(ITiffWritable target) {
    if (target.WriteOffset != 0) {
      WriteUInt32(target.WriteOffset);
      return;
    }

    _deferred.Add(new(Position, target));
    WriteUInt32(0);
  }

  public void FlushDeferred() {
    foreach (var item in _deferred) {
      if (item.Target.WriteOffset == 0)
        throw new InvalidOperationException(
          $"Target '{item.Target.GetType().Name}' has not been written.");

      PatchUInt32((uint)item.PatchPosition, item.Target.WriteOffset);
    }

    _deferred.Clear();
  }

  public void WriteInlineValue(ReadOnlySpan<byte> data) {
    Span<byte> value = stackalloc byte[4];
    data.CopyTo(value);
    WriteBytes(value);
  }

  private sealed class DeferredReference(long patchPosition, ITiffWritable target) {
    public long PatchPosition { get; } = patchPosition;
    public ITiffWritable Target { get; } = target;
  }
}