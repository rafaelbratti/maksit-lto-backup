using System.Security.Cryptography;


namespace MaksIT.LTO.Core;

public class Crc32 : HashAlgorithm {
  public const uint DefaultPolynomial = 0xedb88320;
  public const uint DefaultSeed = 0xffffffff;

  private static uint[]? defaultTable;

  private readonly uint seed;
  private readonly uint[] table;
  private uint hash;

  public Crc32()
      : this(DefaultPolynomial, DefaultSeed) {
  }

  public Crc32(uint polynomial, uint seed) {
    table = InitializeTable(polynomial);
    this.seed = hash = seed;
  }

  public override void Initialize() {
    hash = seed;
  }

  protected override void HashCore(byte[] buffer, int start, int length) {
    hash = CalculateHash(table, hash, buffer, start, length);
  }

  protected override byte[] HashFinal() {
    var hashBuffer = UInt32ToBigEndianBytes(~hash);
    HashValue = hashBuffer;
    return hashBuffer;
  }

  public override int HashSize => 32;

  public static uint Compute(byte[] buffer) {
    return Compute(DefaultPolynomial, DefaultSeed, buffer);
  }

  public static uint Compute(uint seed, byte[] buffer) {
    return Compute(DefaultPolynomial, seed, buffer);
  }

  public static uint Compute(uint polynomial, uint seed, byte[] buffer) {
    return ~CalculateHash(InitializeTable(polynomial), seed, buffer, 0, buffer.Length);
  }

  private static uint[] InitializeTable(uint polynomial) {
    if (polynomial == DefaultPolynomial && defaultTable != null)
      return defaultTable;

    var createTable = new uint[256];
    for (var i = 0; i < 256; i++) {
      var entry = (uint)i;
      for (var j = 0; j < 8; j++)
        if ((entry & 1) == 1)
          entry = (entry >> 1) ^ polynomial;
        else
          entry >>= 1;
      createTable[i] = entry;
    }

    if (polynomial == DefaultPolynomial)
      defaultTable = createTable;

    return createTable;
  }

  private static uint CalculateHash(uint[] table, uint seed, byte[] buffer, int start, int size) {
    var crc = seed;
    for (var i = start; i < size - start; i++)
      crc = (crc >> 8) ^ table[buffer[i] ^ crc & 0xff];
    return crc;
  }

  private static byte[] UInt32ToBigEndianBytes(uint x) => [
    (byte)((x >> 24) & 0xff),
    (byte)((x >> 16) & 0xff),
    (byte)((x >> 8) & 0xff),
    (byte)(x & 0xff)
  ];
}
