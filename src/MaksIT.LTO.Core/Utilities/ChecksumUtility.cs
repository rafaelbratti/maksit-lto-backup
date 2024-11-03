namespace MaksIT.LTO.Core.Helpers;

public static class ChecksumUtility {
  public static string CalculateCRC32Checksum(byte[] data) {
    using var crc32 = new Crc32();
    var hashBytes = crc32.ComputeHash(data);
    return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
  }

  public static string CalculateCRC32ChecksumFromFile(string filePath) {
    using var crc32 = new Crc32();
    using var stream = File.OpenRead(filePath);
    var hashBytes = crc32.ComputeHash(stream);
    return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
  }

  public static string CalculateCRC32ChecksumFromFileInChunks(string filePath, int chunkSize = 8192) {
    using var crc32 = new Crc32();
    using var stream = File.OpenRead(filePath);
    var buffer = new byte[chunkSize];
    int bytesRead;
    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0) {
      crc32.TransformBlock(buffer, 0, bytesRead, null, 0);
    }
    crc32.TransformFinalBlock(buffer, 0, 0);
    var hashBytes = crc32.Hash;
    return BitConverter.ToString(hashBytes ?? Array.Empty<byte>()).Replace("-", "").ToLower();
  }

  public static bool VerifyCRC32Checksum(byte[] data, string expectedChecksum) {
    var calculatedChecksum = CalculateCRC32Checksum(data);
    return string.Equals(calculatedChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);
  }

  public static bool VerifyCRC32ChecksumFromFile(string filePath, string expectedChecksum) {
    var calculatedChecksum = CalculateCRC32ChecksumFromFile(filePath);
    return string.Equals(calculatedChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);
  }

  public static bool VerifyCRC32ChecksumFromFileInChunks(string filePath, string expectedChecksum, int chunkSize = 8192) {
    var calculatedChecksum = CalculateCRC32ChecksumFromFileInChunks(filePath, chunkSize);
    return string.Equals(calculatedChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);
  }
}

