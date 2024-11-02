# MaksIT.LTO.Backup

**⚠️ Warning: This program is currently under fine-tuning and some features are still being added. Extensive real-world testing is still in progress. Use it at your own risk until a stable version is released.**

A C# application designed to facilitate backup and restore operations to an LTO tape drive. This application enables seamless management of backups by handling file organization, descriptor creation, and efficient tape handling processes for loading, writing, and restoring data.

## Version History

**v0.0.1 - Initial Release (01/11/2024)**
- Initial implementation of backup and restore operations.
- Support for loading and ejecting tape.
- Basic file descriptor management.
- Customizable block sizes for different LTO generations.
- File checksum verification during restore

## Features

- **Load and Eject Tape**: Safely loads and unloads the tape using `TapeDeviceHandler`.
- **Backup Operation**: Allows users to create a descriptor file for organizing file metadata and backs up files to the LTO tape in structured blocks.
- **File Descriptor Management**: Metadata is organized for each file, including file paths, sizes, creation, modification times and checksum.
- **Restore Operation**: Reads data from the tape and restores files to a specified directory, reconstructing original file structure with checksum verifications.
- **Customizable Block Sizes**: Supports multiple LTO generations, allowing customization of block sizes based on the tape generation (e.g., LTO-6).

## Requirements

- .NET8 or higher
- JSON configuration files: `configuration.json` and `descriptor.json` (auto-generated)
- LTO Tape Drive and compatible drivers

## Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/MAKS-IT-COM/maksit-lto-backup
   ```
2. Ensure `.NET8 SDK` is installed on your system.
3. Prepare a `configuration.json` file in the application directory with the following structure:

```json
{
  "TapePath": "\\\\.\\Tape0",
  "WriteDelay": 100,
  "Backups": [
    {
      "Name": "Normal test",
      "Barcode": "",
      "LTOGen": "LTO5",
      "Source": {
        "LocalPath": {
          "Path": "F:\\LTO\\Backup"
        }
      },
      "Destination": {
        "LocalPath": {
          "Path": "F:\\LTO\\Restore"
        }
      }
    },
    {
      "Name": "Network test",
      "Barcode": "",
      "LTOGen": "LTO5",
      "Source": {
        "RemotePath": {
          "Path": "\\\\nassrv0001.corp.maks-it.com\\data-1\\Users",
          "PasswordCredentials": {
            "Username": "",
            "Password": ""
          },
          "Protocol": "SMB"
        }
      },
      "Destination": {
        "LocalPath": {
          "Path": "F:\\LTO\\Restore"
        }
      }
    }
  ]
}
```

## Usage

### Running the Application

Execute the application by navigating to the project directory `src\MaksIT.LTO.Backup` and running:
```bash
dotnet build && dotnet run
```

or use `dotnet_build_script.bat` to generate executables in `src\build_outputs`

### Application Menu

Upon running, the following options will be presented:

1. **Load Tape**: Loads the tape into the drive.
2. **Backup**: Prompts the user to select a backup task from the configured list and initiates the backup process.
3. **Restore**: Restores a previously backed-up directory from the tape.
4. **Eject Tape**: Ejects the tape from the drive.
5. **Get device status**: Mostly used for debugging to understand if device is able to write
6. **Tape Erase (Short)**: Short tape erase
7. **Reload configurations**: Works outside development evironment, used to reload configurations in case of `configuration.json` is changed
8. **Exit**: Exits the application.

### Code Overview

- **Application Class**: Main class handling backup and restore functionalities. Provides methods like `LoadTape`, `EjectTape`, `Backup`, `Restore`, `CreateDescriptor`, `WriteFilesToTape`, and `FindDescriptor`.
- **TapeDeviceHandler**: Controls tape device operations such as setting positions, writing data, and reading data.
- **BackupDescriptor**: Organizes metadata for backed-up files. Used to log details about each file block on tape.

### Sample Configuration

Below is an example configuration setup for an LTO-6 tape generation backup operation:

```json
{
  "TapePath": "\\\\.\\Tape0",
  "WriteDelay": 100,
  "Backups": [
    {
      "Name": "Normal test",
      "Barcode": "",
      "LTOGen": "LTO5",
      "Source": {
        "LocalPath": {
          "Path": "F:\\LTO\\Backup"
        }
      },
      "Destination": {
        "LocalPath": {
          "Path": "F:\\LTO\\Restore"
        }
      }
    },
    {
      "Name": "Network test",
      "Barcode": "",
      "LTOGen": "LTO5",
      "Source": {
        "RemotePath": {
          "Path": "\\\\nassrv0001.corp.maks-it.com\\data-1\\Users",
          "PasswordCredentials": {
            "Username": "",
            "Password": ""
          },
          "Protocol": "SMB"
        }
      },
      "Destination": {
        "LocalPath": {
          "Path": "F:\\LTO\\Restore\\Users"
        }
      }
    }
  ]
}
```

### Error Handling

Errors during backup or restore are caught and logged for the moment only to the console. Ensure that your `TapeDeviceHandler` is correctly configured and that the tape drive is accessible.

## Contact

If you have any questions or need further assistance, feel free to reach out:

- **Email**: [maksym.sadovnychyy@gmail.com](mailto:maksym.sadovnychyy@gmail.com)
- **Reddit**: [MaksIT.LTO.Backup: A Simplified CLI Tool for Windows LTO Tape Backups](https://www.reddit.com/r/MaksIT/comments/1ghgbx5/maksitltobackup_a_simplified_cli_tool_for_windows/?utm_source=share&utm_medium=web3x&utm_name=web3xcss&utm_term=1&utm_content=share_button)

## License

This project is licensed under the terms of the GPLv2. See the [LICENSE](./LICENSE) file for full details.

© Maksym Sadovnychyy (MAKS-IT) 2024