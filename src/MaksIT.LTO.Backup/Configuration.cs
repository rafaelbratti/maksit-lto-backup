using System.Security;

namespace MaksIT.LTO.Backup;

public abstract class PathBase {
  public required string Path { get; set; }
}

public class LocalPath : PathBase {
  // Additional properties specific to local paths can be added here
}

public class PasswordCredentials {
  public required string Username { get; set; }
  public required SecureString Password { get; set; }
}

public class RemotePath : PathBase {
  public PasswordCredentials? PasswordCredentials { get; set; }
  public required string Protocol { get; set; } // e.g., SMB, FTP, etc.
}

public class WorkingFolder {
  public LocalPath? LocalPath { get; set; }
  public RemotePath? RemotePath { get; set; }
}

public class BackupItem {
  public required string Name { get; set; }
  public required string Barcode { get; set; }
  public required WorkingFolder Source { get; set; }
  public required WorkingFolder Destination { get; set; }
  public required string LTOGen { get; set; }
}

public class Configuration {
  public required string TapePath { get; set; }
  public required int WriteDelay { get; set; }
  public required List<BackupItem> Backups { get; set; }
}
