namespace MH.Utils.Imaging.Tiff;

public sealed class JpegValue(uint originalOffset, byte[] data) : DataValue(originalOffset, data) { }