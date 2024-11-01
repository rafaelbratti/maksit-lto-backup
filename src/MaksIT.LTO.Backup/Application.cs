using System.Text;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

using MaksIT.LTO.Core;
using MaksIT.LTO.Backup.Entities;
using System.Net;

namespace MaksIT.LTO.Backup;
public class Application {

  private const string _descriptoFileName = "descriptor.json";
  private const string _configurationFileName = "configuration.json";

  private readonly string appPath = AppDomain.CurrentDomain.BaseDirectory;
  private readonly string _tapePath;
  private readonly string _descriptorFilePath;

  private Configuration _configuration;

  public Application() {
    _descriptorFilePath = Path.Combine(appPath, _descriptoFileName);
    LoadConfiguration();

    _tapePath = _configuration.TapePath;
  }

  [MemberNotNull(nameof(_configuration))]
  public void LoadConfiguration() {
    var configFilePath = Path.Combine(appPath, _configurationFileName);
    var configuration = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(configFilePath));
    if (configuration == null)
      throw new InvalidOperationException("Failed to deserialize configuration.");

    _configuration = configuration;
  }

  public void LoadTape() {
    using var handler = new TapeDeviceHandler(_tapePath);
    LoadTape(handler);
  }

  public void LoadTape(TapeDeviceHandler handler) {
    handler.Prepare(TapeDeviceHandler.TAPE_LOAD);
    Thread.Sleep(2000);

    Console.WriteLine("Tape loaded.");
  }

  public void EjectTape() {
    using var handler = new TapeDeviceHandler(_tapePath);
    EjectTape(handler);
  }

  public void EjectTape(TapeDeviceHandler handler) {
    handler.Prepare(TapeDeviceHandler.TAPE_UNLOAD);
    Thread.Sleep(2000);

    Console.WriteLine("Tape ejected.");
  }


  public void PathAccessWrapper(WorkingFolder workingFolder, Action<string> myAction) {

    if (workingFolder.LocalPath != null) {
      var localPath = workingFolder.LocalPath.Path;
      var path = workingFolder.LocalPath.Path;

      myAction(path);
    }
    else if (workingFolder.RemotePath != null) {
      var remotePath = workingFolder.RemotePath;

      if (remotePath.Protocol == "SMB") {
        NetworkCredential? networkCredential = default;

        if (remotePath.PasswordCredentials != null) {
          var username = remotePath.PasswordCredentials.Username;
          var password = remotePath.PasswordCredentials.Password;

          networkCredential = new NetworkCredential(username, password);
        }

        var smbPath = remotePath.Path;

        if (networkCredential == null) {
          throw new InvalidOperationException("Network credentials are required for remote paths.");
        }
          
        using (new NetworkConnection(smbPath, networkCredential)) {
           myAction(smbPath);
        }
      }
    }
  }

  public void CreateDescriptor(WorkingFolder workingFolder, string descriptorFilePath, uint blockSize) {

    PathAccessWrapper(workingFolder, (directoryPath) => {
      var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);

      // Define list to hold file descriptors
      var descriptor = new List<FileDescriptor>();
      uint currentTapeBlock = 0;

      foreach (var filePath in files) {
        var fileInfo = new FileInfo(filePath);
        var relativePath = Path.GetRelativePath(directoryPath, filePath);
        var numberOfBlocks = (uint)((fileInfo.Length + blockSize - 1) / blockSize);

        // Optional: Calculate a simple hash for file integrity (e.g., MD5)
        using var md5 = System.Security.Cryptography.MD5.Create();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var bufferedStream = new BufferedStream(fileStream, (int)blockSize);

        byte[] buffer = new byte[blockSize];
        int bytesRead;
        while ((bytesRead = bufferedStream.Read(buffer, 0, buffer.Length)) > 0) {
          md5.TransformBlock(buffer, 0, bytesRead, null, 0);
        }
        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        string fileHash = BitConverter.ToString(md5.Hash).Replace("-", "").ToLower();

        descriptor.Add(new FileDescriptor {
          StartBlock = currentTapeBlock, // Position of the file on the tape
          NumberOfBlocks = numberOfBlocks, // Number of blocks used by the file
          FilePath = relativePath,
          FileSize = fileInfo.Length,
          CreationTime = fileInfo.CreationTime,
          LastModifiedTime = fileInfo.LastWriteTime,
          FileHash = fileHash
        });

        currentTapeBlock += numberOfBlocks;
      }

      // Convert descriptor list to JSON and include BlockSize
      string descriptorJson = JsonSerializer.Serialize(new BackupDescriptor {
        BlockSize = blockSize,
        Files = descriptor
      });

      File.WriteAllText(descriptorFilePath, descriptorJson);
    });
  }

  private void ZeroFillBlocks(TapeDeviceHandler handler, int blocks, uint blockSize) {
    Console.WriteLine($"Writing {blocks} zero-filled blocks to tape.");
    Console.WriteLine($"Block Size: {blockSize}.");

    for (int i = 0; i < blocks; i++) {
      handler.WriteData(new byte[blockSize]);
      Thread.Sleep(100);
    }
  }

  public void WriteFilesToTape(WorkingFolder workingFolder, string descriptorFilePath, uint blockSize) {
    PathAccessWrapper(workingFolder, (directoryPath) => {
      Console.WriteLine($"Writing files to tape from: {directoryPath}.");
      Console.WriteLine($"Block Size: {blockSize}.");

      using var handler = new TapeDeviceHandler(_tapePath);

      LoadTape(handler);

      handler.SetMediaParams(blockSize);

      handler.SetPosition(TapeDeviceHandler.TAPE_REWIND);
      Thread.Sleep(2000);

      handler.Prepare(TapeDeviceHandler.TAPE_TENSION);
      Thread.Sleep(2000);

      handler.Prepare(TapeDeviceHandler.TAPE_LOCK);
      Thread.Sleep(2000);

      handler.WaitForTapeReady();

      // Read descriptor from file system
      string descriptorJson = File.ReadAllText(descriptorFilePath);
      var descriptor = JsonSerializer.Deserialize<BackupDescriptor>(descriptorJson);

      if (descriptor == null) {
        throw new InvalidOperationException("Failed to deserialize descriptor.");
      }

      var currentTapeBlock = (descriptorJson.Length + blockSize - 1) / blockSize;

      foreach (var file in descriptor.Files) {
        var filePath = Path.Combine(directoryPath, file.FilePath);
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var bufferedStream = new BufferedStream(fileStream, (int)blockSize);

        byte[] buffer = new byte[blockSize];
        int bytesRead;
        for (var i = 0; i < file.NumberOfBlocks; i++) {
          bytesRead = bufferedStream.Read(buffer, 0, buffer.Length);
          if (bytesRead < buffer.Length) {
            // Zero-fill the remaining part of the buffer if the last block is smaller than blockSize
            Array.Clear(buffer, bytesRead, buffer.Length - bytesRead);
          }
          handler.WriteData(buffer);
          currentTapeBlock++;
          Thread.Sleep(100); // Small delay between blocks
        }
      }


      // write mark to indicate end of files
      handler.WriteMarks(TapeDeviceHandler.TAPE_FILEMARKS, 1);

      // write descriptor to tape
      var descriptorData = Encoding.UTF8.GetBytes(descriptorJson);
      var descriptorBlocks = (descriptorData.Length + blockSize - 1) / blockSize;
      for (int i = 0; i < descriptorBlocks; i++) {
        var startIndex = i * blockSize;
        var length = Math.Min(blockSize, descriptorData.Length - startIndex);
        byte[] block = new byte[blockSize]; // Initialized with zeros by default
        Array.Copy(descriptorData, startIndex, block, 0, length);
        handler.WriteData(block);
        currentTapeBlock++;
        Thread.Sleep(100); // Small delay between blocks
      }

      // write 3 0 filled blocks to indicate end of backup
      ZeroFillBlocks(handler, 3, blockSize);

      handler.Prepare(TapeDeviceHandler.TAPE_UNLOCK);
      Thread.Sleep(2000);
      handler.SetPosition(TapeDeviceHandler.TAPE_REWIND);
      Thread.Sleep(2000);

    });
  }

  public BackupDescriptor? FindDescriptor(uint blockSize) {
    Console.WriteLine("Searching for descriptor on tape...");
    Console.WriteLine($"Block Size: {blockSize}.");

    using var handler = new TapeDeviceHandler(_tapePath);

    LoadTape(handler);

    handler.SetMediaParams(blockSize);

    handler.SetPosition(TapeDeviceHandler.TAPE_REWIND);
    Thread.Sleep(2000);

    handler.SetPosition(TapeDeviceHandler.TAPE_SPACE_FILEMARKS, 0, 1);
    Thread.Sleep(2000);

    handler.WaitForTapeReady();

    // Read data from tape until 3 zero-filled blocks are found
    var buffer = new List<byte>();
    byte[] data;
    var zeroBlocks = 0;
    do {
      data = handler.ReadData(blockSize);
      buffer.AddRange(data);
      if (data.All(b => b == 0)) {
        zeroBlocks++;
      }
      else {
        zeroBlocks = 0;
      }
    } while (zeroBlocks < 3);

    // Remove the last 3 zero-filled blocks from the buffer
    var totalZeroBlocksSize = (int)(3 * blockSize);
    if (buffer.Count >= totalZeroBlocksSize) {
      buffer.RemoveRange(buffer.Count - totalZeroBlocksSize, totalZeroBlocksSize);
    }

    // Convert buffer to byte array
    var byteArray = buffer.ToArray();

    // Convert byte array to string and trim ending zeros
    var json = Encoding.UTF8.GetString(byteArray).TrimEnd('\0');

    try {
      var descriptor = JsonSerializer.Deserialize<BackupDescriptor>(json);
      if (descriptor != null) {
        Console.WriteLine("Descriptor read successfully.");
        return descriptor;
      }
    }
    catch (JsonException ex) {
      Console.WriteLine($"Failed to parse descriptor JSON: {ex.Message}");
    }


    handler.Prepare(TapeDeviceHandler.TAPE_UNLOCK);
    Thread.Sleep(2000);
    handler.SetPosition(TapeDeviceHandler.TAPE_REWIND);
    Thread.Sleep(2000);

    return null;
  }

  public void RestoreDirectory(BackupDescriptor descriptor, WorkingFolder workingFolder) {

    PathAccessWrapper(workingFolder, (restoreDirectoryPath) => {
      Console.WriteLine("Restoring files to directory: " + restoreDirectoryPath);
      Console.WriteLine("Block Size: " + descriptor.BlockSize);

      using var handler = new TapeDeviceHandler(_tapePath);

      LoadTape(handler);

      handler.SetMediaParams(descriptor.BlockSize);

      handler.SetPosition(TapeDeviceHandler.TAPE_REWIND);
      Thread.Sleep(2000);

      handler.WaitForTapeReady();

      foreach (var file in descriptor.Files) {
        // Set position to the start block of the file
        handler.SetPosition(TapeDeviceHandler.TAPE_ABSOLUTE_BLOCK, 0, file.StartBlock);
        Thread.Sleep(2000);

        var filePath = Path.Combine(restoreDirectoryPath, file.FilePath);
        var directoryPath = Path.GetDirectoryName(filePath);
        if (directoryPath != null) {
          Directory.CreateDirectory(directoryPath);
        }

        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write)) {
          var buffer = new byte[descriptor.BlockSize];

          for (var i = 0; i < file.NumberOfBlocks; i++) {
            var bytesRead = handler.ReadData(buffer, 0, buffer.Length);
            if (bytesRead < buffer.Length) {
              // Zero-fill the remaining part of the buffer if the last block is smaller than blockSize
              Array.Clear(buffer, bytesRead, buffer.Length - bytesRead);
            }

            var bytesToWrite = (i == file.NumberOfBlocks - 1) ? (int)(file.FileSize % descriptor.BlockSize) : buffer.Length;
            fileStream.Write(buffer, 0, bytesToWrite);
          }
        }

        // check md5 checksum of restored file with the one in descriptor
        using (var md5 = System.Security.Cryptography.MD5.Create()) {
          using (var fileStreamRead = new FileStream(filePath, FileMode.Open, FileAccess.Read)) {
            var fileHash = md5.ComputeHash(fileStreamRead);
            var fileHashString = BitConverter.ToString(fileHash).Replace("-", "").ToLower();

            if (fileHashString != file.FileHash) {
              Console.WriteLine($"Checksum mismatch for file: {filePath}");
            }
            else {
              Console.WriteLine($"Restored file: {filePath}");
            }
          }
        }
      }

      handler.SetPosition(TapeDeviceHandler.TAPE_REWIND);
      Thread.Sleep(2000);
    });
  }

  public int CheckMediaSize(string ltoGen) {
    var descriptor = JsonSerializer.Deserialize<BackupDescriptor>(File.ReadAllText(_descriptorFilePath));
    if (descriptor == null) {
      Console.WriteLine("Failed to read descriptor.");
      return 1;
    }

    var totalBlocks = (ulong)descriptor.Files.Sum(f => f.NumberOfBlocks);

    const ulong fileMarkBlocks = 1;
    const ulong terminalBlocks = 3;

    var descriptorSize = new FileInfo(_descriptoFileName).Length;
    ulong descriptorSizeBlocks = (ulong)Math.Ceiling((double)descriptorSize / descriptor.BlockSize);

    totalBlocks += fileMarkBlocks + descriptorSizeBlocks + terminalBlocks;

    var maxBlocks = LTOBlockSizes.GetMaxBlocks(ltoGen);
    if (totalBlocks > maxBlocks) {
      Console.WriteLine("Backup will not fit on tape. Please use a larger tape.");
      return 1;
    }
    else {
      Console.WriteLine("Backup will fit on tape.");
    }

    return 0;
  }

  public void Backup() {
    while (true) {
      Console.WriteLine("\nSelect a backup to perform:");
      for (int i = 0; i < _configuration.Backups.Count; i++) {
        var backupInt = _configuration.Backups[i];
        Console.WriteLine($"{i + 1}. Backup Name: {backupInt.Name}, Bar code {backupInt.Barcode}, Source: {backupInt.Source}, Destination: {backupInt.Destination}");
      }

      Console.Write("Enter your choice (or '0' to go back): ");
      var choice = Console.ReadLine();

      if (choice == "0") {
        return; // Go back to the main menu
      }

      if (!int.TryParse(choice, out int index) || index < 1 || index > _configuration.Backups.Count) {
        Console.WriteLine("Invalid choice. Please try again.");
        continue;
      }

      var backup = _configuration.Backups[index - 1];

      uint blockSize = LTOBlockSizes.GetBlockSize(backup.LTOGen);

      // Step 1: Create Descriptor and Write to File System
      CreateDescriptor(backup.Source, _descriptorFilePath, blockSize);

      // Step 2: calculate if files in descriptor will fit on tape
      var checkMediaSizeResult = CheckMediaSize(backup.LTOGen);
      if (checkMediaSizeResult != 0)
        return;

      // Step 3: Write Files to Tape
      WriteFilesToTape(backup.Source, _descriptorFilePath, blockSize);

      File.Delete(_descriptorFilePath);
      Console.WriteLine("Backup completed.");
      return; // Go back to the main menu after completing the backup
    }
  }

  public void Restore() {
    while (true) {
      Console.WriteLine("\nSelect a backup to restore:");
      for (int i = 0; i < _configuration.Backups.Count; i++) {
        var backupInt = _configuration.Backups[i];
        Console.WriteLine($"{i + 1}. Backup Name: {backupInt.Name}, Bar code {backupInt.Barcode}, Source: {backupInt.Source}, Destination: {backupInt.Destination}");
      }

      Console.Write("Enter your choice (or '0' to go back): ");
      var choice = Console.ReadLine();

      if (choice == "0") {
        return; // Go back to the main menu
      }

      if (!int.TryParse(choice, out int index) || index < 1 || index > _configuration.Backups.Count) {
        Console.WriteLine("Invalid choice. Please try again.");
        continue;
      }

      var backup = _configuration.Backups[index - 1];
      
      uint blockSize = LTOBlockSizes.GetBlockSize(backup.LTOGen);

      var descriptor = FindDescriptor(blockSize);
      if (descriptor != null) {
        var json = JsonSerializer.Serialize(descriptor, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
      }

      if (descriptor == null) {
        Console.WriteLine("Descriptor not found on tape.");
        return;
      }

      // Step 3: Test restore from tape
      RestoreDirectory(descriptor, backup.Destination);
      Console.WriteLine("Restore completed.");
      return; // Go back to the main menu after completing the restore
    }
  }
}

