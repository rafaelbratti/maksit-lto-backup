using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaksIT.LTO.Core;
public class DriverManager {
  public static void RestartDriver(string deviceName) {
    string script = $@"
            $device = Get-PnpDevice -FriendlyName '{deviceName}'
            Disable-PnpDevice -InstanceId $device.InstanceId -Confirm:$false
            Start-Sleep -Seconds 5
            Enable-PnpDevice -InstanceId $device.InstanceId -Confirm:$false
        ";

    ProcessStartInfo psi = new ProcessStartInfo {
      FileName = "powershell.exe",
      Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };

    using (Process process = Process.Start(psi)) {
      string output = process.StandardOutput.ReadToEnd();
      string error = process.StandardError.ReadToEnd();
      process.WaitForExit();

      Console.WriteLine(output);
      if (!string.IsNullOrEmpty(error)) {
        Console.WriteLine($"Error: {error}");
      }
    }
  }
}
