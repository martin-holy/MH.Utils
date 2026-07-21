namespace MH.Utils.Imaging.Tiff;

public class DataValue(uint? originalOffset, byte[] data) : TiffObject(originalOffset) {
  public byte[] Data { get; set; } = data;

  public override int OriginalSize { get; } = originalOffset != null ? data.Length : 0;
  public override int CurrentSize => Data.Length;

  public override void Write(TiffWriter writer) {
    WriteOffset = (uint)writer.Position;
    writer.WriteBytes(Data);
  }
}