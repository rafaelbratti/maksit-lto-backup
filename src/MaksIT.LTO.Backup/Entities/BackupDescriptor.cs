

namespace MaksIT.LTO.Backup.Entities;
public class BackupDescriptor {
  public uint ReservedBlocks { get; set; }
  public uint BlockSize { get; set; }
  public List<FileDescriptor> Files { get; set; } = new List<FileDescriptor>();
}
