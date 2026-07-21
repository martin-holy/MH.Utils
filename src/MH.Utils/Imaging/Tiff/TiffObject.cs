namespace MH.Utils.Imaging.Tiff;

public abstract class TiffObject(uint? originalOffset) : ITiffWritable {
  public uint? OriginalOffset { get; } = originalOffset;
  public uint WriteOffset { get; set; }
  public TiffLayoutHole? HoleAfter { get; set; }

  public virtual int OriginalSize => 0;
  public virtual int CurrentSize => OriginalSize;

  public abstract void Write(TiffWriter writer);
}