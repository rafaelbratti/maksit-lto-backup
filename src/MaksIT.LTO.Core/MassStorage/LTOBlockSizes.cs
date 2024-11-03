namespace MaksIT.LTO.Core.MassStorage;

public static class LTOBlockSizes
{
    public const uint LTO1 = 65536;    // 64 KB
    public const uint LTO2 = 65536;    // 64 KB
    public const uint LTO3 = 131072;   // 128 KB
    public const uint LTO4 = 131072;   // 128 KB
    public const uint LTO5 = 262144;   // 256 KB
    public const uint LTO6 = 262144;   // 256 KB
    public const uint LTO7 = 524288;   // 512 KB
    public const uint LTO8 = 524288;   // 512 KB
    public const uint LTO9 = 1048576;  // 1 MB

    // Dictionary to store the total capacity for each LTO generation (in bytes)
    private static readonly Dictionary<string, ulong> TapeCapacities = new Dictionary<string, ulong>
  {
    { "LTO1", 100UL * 1024 * 1024 * 1024 },  // 100 GB
    { "LTO2", 200UL * 1024 * 1024 * 1024 },  // 200 GB
    { "LTO3", 400UL * 1024 * 1024 * 1024 },  // 400 GB
    { "LTO4", 800UL * 1024 * 1024 * 1024 },  // 800 GB
    { "LTO5", 1500UL * 1024 * 1024 * 1024 }, // 1.5 TB
    { "LTO6", 2500UL * 1024 * 1024 * 1024 }, // 2.5 TB
    { "LTO7", 6000UL * 1024 * 1024 * 1024 }, // 6 TB
    { "LTO8", 12000UL * 1024 * 1024 * 1024 },// 12 TB
    { "LTO9", 18000UL * 1024 * 1024 * 1024 } // 18 TB
  };

    // Method to get the block size for a given LTO generation
    // Method to get the block size for a given LTO generation
    public static uint GetBlockSize(string ltoGen)
    {
        return ltoGen switch
        {
            "LTO1" => LTO1,
            "LTO2" => LTO2,
            "LTO3" => LTO3,
            "LTO4" => LTO4,
            "LTO5" => LTO5,
            "LTO6" => LTO6,
            "LTO7" => LTO7,
            "LTO8" => LTO8,
            "LTO9" => LTO9,
            _ => throw new ArgumentException("Invalid LTO generation")
        };
    }

    // Method to get the total capacity for a given LTO generation
    public static ulong GetTapeCapacity(string ltoGen)
    {
        if (TapeCapacities.TryGetValue(ltoGen, out var capacity))
        {
            return capacity;
        }
        throw new ArgumentException("Invalid LTO generation");
    }

    // Method to calculate the maximum number of blocks that can be written on the tape
    public static ulong GetMaxBlocks(string ltoGen)
    {
        var blockSize = GetBlockSize(ltoGen);
        var tapeCapacity = GetTapeCapacity(ltoGen);
        return tapeCapacity / blockSize;
    }
}
