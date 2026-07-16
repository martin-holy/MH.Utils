using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffWriter {
  private readonly MemoryStream _stream;
  private readonly bool _littleEndian;
  private readonly List<DeferredReference> _deferred = [];

  public long Position => _stream.Position;

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

  public void WriteReference(ITiffWritable target) {
    if (target.WriteOffset != 0) {
      WriteUInt32(target.WriteOffset);
      return;
    }

    _deferred.Add(new(_stream.Position, target));
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

  public void WriteBytes(ReadOnlySpan<byte> bytes) =>
    _stream.Write(bytes);

  public void WriteZeros(int count) {
    while (count-- > 0)
      _stream.WriteByte(0);
  }

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

  private sealed class DeferredReference(long patchPosition, ITiffWritable target) {
    public long PatchPosition { get; } = patchPosition;
    public ITiffWritable Target { get; } = target;
  }

  public static void WriteExifToJpeg(Stream input, Stream output, byte[] tiff) {
    using var br = new BinaryReader(input, Encoding.ASCII, leaveOpen: true);

    // SOI
    ByteU.CopyBytes(input, output, 2);

    bool exifWritten = false;
    bool insertAfterAppSegments = true;
    Span<byte> header = stackalloc byte[ExifU.ExifHeader.Length];

    while (input.Position + 4 <= input.Length) {
      if (br.ReadByte() != 0xFF)
        continue;

      byte marker = br.ReadByte();

      while (marker == 0xFF)
        marker = br.ReadByte();

      // Start of Scan
      if (marker == 0xDA) {
        if (!exifWritten) {
          _writeExifSegment(output, tiff);
          exifWritten = true;
        }

        output.WriteByte(0xFF);
        output.WriteByte(0xDA);

        input.CopyTo(output);
        return;
      }

      ushort segLen = ByteU.ReadBigEndianUInt16(br);

      if (segLen < 2)
        throw new InvalidDataException("Invalid JPEG segment.");

      int payloadLen = segLen - 2;

      // APP1 - check whether it contains EXIF
      if (marker == 0xE1) {
        if (payloadLen >= header.Length) {
          input.ReadExactly(header);

          if (header.SequenceEqual(ExifU.ExifHeader)) {
            // Skip old EXIF
            input.Seek(payloadLen - header.Length, SeekOrigin.Current);
            continue;
          }

          // Not EXIF, restore consumed header
          output.WriteByte(0xFF);
          output.WriteByte(marker);
          ByteU.WriteBigEndianUInt16(output, segLen);

          output.Write(header);

          ByteU.CopyBytes(input, output, payloadLen - header.Length);

          insertAfterAppSegments = false;

          continue;
        }
      }

      if (!exifWritten && insertAfterAppSegments && marker != 0xE0 && marker != 0xE1) {
        _writeExifSegment(output, tiff);
        exifWritten = true;
        insertAfterAppSegments = false;
      }

      output.WriteByte(0xFF);
      output.WriteByte(marker);
      ByteU.WriteBigEndianUInt16(output, segLen);

      ByteU.CopyBytes(input, output, payloadLen);
    }

    if (!exifWritten)
      throw new InvalidDataException("Failed to write EXIF.");
  }

  private static void _writeExifSegment(Stream stream, byte[] tiff) {
    stream.WriteByte(0xFF);
    stream.WriteByte(0xE1);

    const int MaxAppPayload = 65533;

    int payloadLength = ExifU.ExifHeader.Length + tiff.Length;

    if (payloadLength > MaxAppPayload)
      throw new InvalidOperationException("EXIF is too large for a JPEG APP1 segment.");

    ByteU.WriteBigEndianUInt16(stream, (ushort)(payloadLength + 2));

    stream.Write(ExifU.ExifHeader);
    stream.Write(tiff);
  }
}