namespace MaksIT.LTO.Core.Utilities;

public static class PaddingUtility {

  private const byte _specialByte = 0x80;

  public static byte[] AddPadding(byte[] data, int blockSize) {
    var paddingSize = blockSize - (data.Length % blockSize);
    if (paddingSize == blockSize) {
      paddingSize = 0;  // no padding needed if already aligned
    }

    var paddedData = new byte[data.Length + paddingSize];
    Array.Copy(data, paddedData, data.Length);

    // fill the padding bytes with specialBytes
    for (var i = data.Length; i < paddedData.Length; i++) {
      paddedData[i] = _specialByte;
    }

    return paddedData;
  }

  public static byte[] RemovePadding(byte[] paddedData, int blockSize) {
    var originalLength = paddedData.Length;

    // find the original length by checking for the padding byte 0x80
    while (originalLength > 0 && paddedData[originalLength - 1] == _specialByte) {
      originalLength--;
    }

    // create a new array to hold the unpadded data
    var unpaddedData = new byte[originalLength];
    Array.Copy(paddedData, unpaddedData, originalLength);

    return unpaddedData;
  }
}