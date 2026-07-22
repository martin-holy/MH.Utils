namespace MH.Utils.Imaging.Tiff;

public enum ExifTag : ushort {
  Orientation = 0x0112,

  ExifIfd = 0x8769,
  GpsIfd = 0x8825,
  InteropIfd = 0xA005,

  UserComment = 0x9286,
  XpComment = 0x9C9C,

  GpsLatitudeRef = 0x0001,
  GpsLatitude = 0x0002,
  GpsLongitudeRef = 0x0003,
  GpsLongitude = 0x0004,

  ThumbnailOffset = 0x0201,
  ThumbnailLength = 0x0202,

  Padding = 0xEA1C
}