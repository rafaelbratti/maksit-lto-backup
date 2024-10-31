using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaksIT.LTO.Backup.Entities;
public class FileDescriptor {
  public required string FilePath { get; set; }
  public long StartBlock { get; set; }
  public uint NumberOfBlocks { get; set; }
  public long FileSize { get; set; }
  public DateTime CreationTime { get; set; }
  public DateTime LastModifiedTime { get; set; }
  public required string FileHash { get; set; }
}
