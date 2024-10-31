namespace MaksIT.LTO.Backup;

public class BackupItem {
  public required string Name { get; set; }
  public required string Barcode { get; set; }
  public required string Source { get; set; }
  public required string Destination { get; set; }
  public required string LTOGen { get; set; }
}

public class Configuration {
  public required string TapePath { get; set; }
  public required List<BackupItem> Backups { get; set; }
}
