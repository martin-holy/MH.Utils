namespace MH.Utils.Imaging.Exif;

public enum ExifOrientation : ushort {
  Normal = 1,
  FlipHorizontal = 2,
  Rotate180 = 3,
  FlipVertical = 4,
  Transpose = 5,
  Rotate90 = 6,
  Transverse = 7,
  Rotate270 = 8
}

public static class ExifOrientationExtensions {
  // Orientation from "System.Photo.Orientation" have Rotate90 and Rotate270 swapped
  public static Orientation? ToMsOrientation(this ExifOrientation? value) =>
    value switch {
      ExifOrientation.Normal => Orientation.Normal,
      ExifOrientation.Rotate90 => Orientation.Rotate90,
      ExifOrientation.Rotate180 => Orientation.Rotate180,
      ExifOrientation.Rotate270 => Orientation.Rotate270,
      ExifOrientation.FlipHorizontal => Orientation.FlipHorizontal,
      ExifOrientation.FlipVertical => Orientation.FlipVertical,
      ExifOrientation.Transpose => Orientation.Transpose,
      ExifOrientation.Transverse => Orientation.Transverse,
      _ => null
    };

  // Orientation from "System.Photo.Orientation" have Rotate90 and Rotate270 swapped
  public static ExifOrientation? ToExifOrientation(this Orientation value) =>
    value switch {
      Orientation.Normal => ExifOrientation.Normal,
      Orientation.Rotate90 => ExifOrientation.Rotate90,
      Orientation.Rotate180 => ExifOrientation.Rotate180,
      Orientation.Rotate270 => ExifOrientation.Rotate270,
      Orientation.FlipHorizontal => ExifOrientation.FlipHorizontal,
      Orientation.FlipVertical => ExifOrientation.FlipVertical,
      Orientation.Transpose => ExifOrientation.Transpose,
      Orientation.Transverse => ExifOrientation.Transverse,
      _ => null
    };
}