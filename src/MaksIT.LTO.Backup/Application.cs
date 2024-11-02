using System.Text;
using System.Text.Json;
using System.Net;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MaksIT.LTO.Core;
using MaksIT.LTO.Backup.Entities;
using MaksIT.LTO.Core.MassStorage;
using MaksIT.LTO.Core.Networking;


namespace MaksIT.LTO.Backup;

public class Application {

  private const string _descriptoFileName = "descriptor.json";

  private readonly string appPath = AppDomain.CurrentDomain.BaseDirectory;
  private readonly string _tapePath;
  private readonly string _descriptorFilePath;

  private readonly ILogger<Application> _logger;
  private readonly ILogger<TapeDeviceHandler> _tapeDeviceLogger;
  private readonly ILogger<NetworkConnection> _networkConnectionLogger;

  private readonly Configuration _configuration;

  public Application(
    ILogger<Application> logger,
    ILoggerFactory loggerFactory,
    IOptions<Configuration> configuration
  ) {
    _logger = logger;
    _tapeDeviceLogger = loggerFactory.CreateLogger<TapeDeviceHandler>();
    _networkConnectionLogger = loggerFactory.CreateLogger<NetworkConnection>();

    _descriptorFilePath = Path.Combine(appPath, _descriptoFileName);

    _configuration = configuration.Value;
    _tapePath = _configuration.TapePath;
  }

  public void Run() {
    Console.OutputEncoding = Encoding.UTF8;

    while (true) {
      Console.WriteLine("MaksIT.LTO.Backup v0.0.1");
      Console.WriteLine("© Maksym Sadovnychyy (MAKS-IT) 2024");

      Console.WriteLine("\nSelect an action:");
      Console.WriteLine("1. Load tape");
      Console.WriteLine("2. Backup");
      Console.WriteLine("3. Restore");
      Console.WriteLine("4. Eject tape");
      Console.WriteLine("5. Get device status");
      Console.WriteLine("6. Tape Erase (Short)");
      Console.WriteLine("7. Exit");
      Console.Write("Enter your choice: ");

      var choice = Console.ReadLine();

      try {
        switch (choice) {
          case "1":
            LoadTape();
            break;
          case "2":
            Backup();
            break;
          case "3":
            Restore();
            break;
          case "4":
            EjectTape();
            break;
          case "5":
            GetDeviceStatus();
            break;
          case "6":
            TapeErase();
            break;
          case "7":
            Console.WriteLine("Exiting...");
            return;
          default:
            Console.WriteLine("Invalid choice. Please try again.");
            break;
        }
      }
      catch (Exception ex) {
        _logger.LogError(ex, $"An error occurred: {ex.Message}");
      }
    }
  }

  public void LoadTape() {
    using var handler = new TapeDeviceHandler(_tapeDeviceLogger, _tapePath);
    LoadTape(handler);
  }

  public void LoadTape(TapeDeviceHandler handler) {
    handler.Prepare(TapeDeviceHandler.TAPE_LOAD);
    Thread.Sleep(2000);

    _logger.LogInformation("Tape loaded.");
  }

  public void EjectTape() {
    using var handler = new TapeDeviceHandler(_tapeDeviceLogger, _tapePath);
    EjectTape(handler);
  }

  public void EjectTape(TapeDeviceHandler handler) {
    handler.Prepare(TapeDeviceHandler.TAPE_UNLOAD);
    Thread.Sleep(2000);

    _logger.LogInformation("Tape ejected.");
  }

  public void TapeErase() {
    using var handler = new TapeDeviceHandler(_tapeDeviceLogger, _tapePath);
    LoadTape(handler);

    handler.SetMediaParams(LTOBlockSizes.LTO5);

    handler.SetPosition(TapeDeviceHandler.TAPE_REWIND);
    Thread.Sleep(2000);

    handler.Prepare(TapeDeviceHandler.TAPE_TENSION);
    Thread.Sleep(2000);

    handler.Prepare(TapeDeviceHandler.TAPE_LOCK);
    Thread.Sleep(2000);

    handler.Erase(TapeDeviceHandler.TAPE_ERASE_SHORT);
    Thread.Sleep(2000);

    handler.SetPosition(TapeDeviceHandler.TAPE_REWIND);
    Thread.Sleep(2000);

    _logger.LogInformation("Tape erased.");
  }

  public void GetDeviceStatus() {
    using var handler = new TapeDeviceHandler(_tapeDeviceLogger, _tapePath);
    handler.GetStatus();
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
          
        using (new NetworkConnection(_networkConnectionLogger, smbPath, networkCredential)) {
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
    _logger.LogInformation($"Writing {blocks} zero-filled blocks to tape.");
    _logger.LogInformation($"Block Size: {blockSize}.");

    var writeError = 0;

    for (int i = 0; i < blocks; i++) {
      writeError = handler.WriteData(new byte[blockSize]);
      if (writeError != 0)
        return;

      Thread.Sleep(_configuration.WriteDelay);
    }
  }

  public void WriteFilesToTape(WorkingFolder workingFolder, string descriptorFilePath, uint blockSize) {
    PathAccessWrapper(workingFolder, (directoryPath) => {
      _logger.LogInformation($"Writing files to tape from: {directoryPath}.");
      _logger.LogInformation($"Block Size: {blockSize}.");

      using var handler = new TapeDeviceHandler(_tapeDeviceLogger, _tapePath);

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


      int writeError = 0;

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
          
          writeError = handler.WriteData(buffer);
          if (writeError != 0) {
            _logger.LogInformation($"Failed to write file: {filePath}");
            return;
          }

          currentTapeBlock++;
          Thread.Sleep(_configuration.WriteDelay); // Small delay between blocks
        }
      }


      // write mark to indicate end of files
      handler.WriteMarks(TapeDeviceHandler.TAPE_FILEMARKS, 1);
      Thread.Sleep(_configuration.WriteDelay);

      // write descriptor to tape
      var descriptorData = Encoding.UTF8.GetBytes(descriptorJson);
      var descriptorBlocks = (descriptorData.Length + blockSize - 1) / blockSize;
      for (int i = 0; i < descriptorBlocks; i++) {
        var startIndex = i * blockSize;
        var length = Math.Min(blockSize, descriptorData.Length - startIndex);
        byte[] block = new byte[blockSize]; // Initialized with zeros by default
        Array.Copy(descriptorData, startIndex, block, 0, length);
        
        writeError = handler.WriteData(block);
        if (writeError != 0)
          return;

        currentTapeBlock++;
        Thread.Sleep(_configuration.WriteDelay); // Small delay between blocks
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
    _logger.LogInformation("Searching for descriptor on tape...");
    _logger.LogInformation($"Block Size: {blockSize}.");

    using var handler = new TapeDeviceHandler(_tapeDeviceLogger,_tapePath);

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
        _logger.LogInformation("Descriptor read successfully.");
        return descriptor;
      }
    }
    catch (JsonException ex) {
      _logger.LogInformation($"Failed to parse descriptor JSON: {ex.Message}");
    }


    handler.Prepare(TapeDeviceHandler.TAPE_UNLOCK);
    Thread.Sleep(2000);

    handler.SetPosition(TapeDeviceHandler.TAPE_REWIND);
    Thread.Sleep(2000);

    return null;
  }

  public void RestoreDirectory(BackupDescriptor descriptor, WorkingFolder workingFolder) {

    PathAccessWrapper(workingFolder, (restoreDirectoryPath) => {
      _logger.LogInformation("Restoring files to directory: " + restoreDirectoryPath);
      _logger.LogInformation("Block Size: " + descriptor.BlockSize);

      using var handler = new TapeDeviceHandler(_tapeDeviceLogger,_tapePath);

      LoadTape(handler);

      handler.SetMediaParams(descriptor.BlockSize);

      handler.SetPosition(TapeDeviceHandler.TAPE_REWIND);
      Thread.Sleep(2000);

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
              _logger.LogInformation($"Checksum mismatch for file: {filePath}");
            }
            else {
              _logger.LogInformation($"Restored file: {filePath}");
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
      _logger.LogInformation("Failed to read descriptor.");
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
      _logger.LogInformation("Backup will not fit on tape. Please use a larger tape.");
      return 1;
    }
    else {
      _logger.LogInformation("Backup will fit on tape.");
    }

    return 0;
  }

  public void Backup() {
    while (true) {
      _logger.LogInformation("\nSelect a backup to perform:");
      for (int i = 0; i < _configuration.Backups.Count; i++) {
        var backupInt = _configuration.Backups[i];
        _logger.LogInformation($"{i + 1}. Backup Name: {backupInt.Name}, Bar code {(string.IsNullOrEmpty(backupInt.Barcode) ? "None" : backupInt.Barcode)}, Source: {backupInt.Source}, Destination: {backupInt.Destination}");
      }

      Console.Write("Enter your choice (or '0' to go back): ");
      var choice = Console.ReadLine();

      if (choice == "0") {
        return;
      }

      if (!int.TryParse(choice, out int index) || index < 1 || index > _configuration.Backups.Count) {
        _logger.LogInformation("Invalid choice. Please try again.");
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
      _logger.LogInformation("Backup completed.");
      return;
    }
  }

  public void Restore() {
    while (true) {
      _logger.LogInformation("\nSelect a backup to restore:");
      for (int i = 0; i < _configuration.Backups.Count; i++) {
        var backupInt = _configuration.Backups[i];
        _logger.LogInformation($"{i + 1}. Backup Name: {backupInt.Name}, Bar code {(string.IsNullOrEmpty(backupInt.Barcode) ? "None" : backupInt.Barcode)}, Source: {backupInt.Source}, Destination: {backupInt.Destination}");
      }

      Console.Write("Enter your choice (or '0' to go back): ");
      var choice = Console.ReadLine();

      if (choice == "0") {
        return;
      }

      if (!int.TryParse(choice, out int index) || index < 1 || index > _configuration.Backups.Count) {
        _logger.LogInformation("Invalid choice. Please try again.");
        continue;
      }

      var backup = _configuration.Backups[index - 1];
      
      uint blockSize = LTOBlockSizes.GetBlockSize(backup.LTOGen);

      // Step 1: Find Descriptor on Tape
      var descriptor = FindDescriptor(blockSize);
      if (descriptor != null) {
        var json = JsonSerializer.Serialize(descriptor, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation(json);
      }

      if (descriptor == null) {
        _logger.LogInformation("Descriptor not found on tape.");
        return;
      }

      // Step 2: Restore Files to Directory
      RestoreDirectory(descriptor, backup.Destination);
      _logger.LogInformation("Restore completed.");
      return;
    }
  }
}

