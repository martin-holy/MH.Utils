namespace MH.Utils.Imaging.Jpeg;

public sealed class JpegSegment {
  public byte Marker { get; }
  public long Position { get; }
  public int Length { get; }
  public long PayloadPosition { get; }
  public int PayloadLength { get; }
  internal byte[]? Payload { get; set; }

  internal JpegSegment(byte marker, long position, int length, long payloadPosition, int payloadLength) {
    Marker = marker;
    Position = position;
    Length = length;
    PayloadPosition = payloadPosition;
    PayloadLength = payloadLength;
  }
}