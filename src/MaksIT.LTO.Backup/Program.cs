namespace MaksIT.LTO.Backup;

class Program {

  public static void Main() {

    var app = new Application();

    Console.OutputEncoding = System.Text.Encoding.UTF8;

    while (true) {
      // Console.Clear();
      Console.WriteLine("MaksIT.LTO.Backup v0.0.1");
      Console.WriteLine("© Maksym Sadovnychyy (MAKS-IT) 2024");

      Console.WriteLine("\nSelect an action:");
      Console.WriteLine("1. Load tape");
      Console.WriteLine("2. Backup");
      Console.WriteLine("3. Restore");
      Console.WriteLine("4. Eject tape");
      Console.WriteLine("5. Reload configurations");
      Console.WriteLine("6. Exit");
      Console.Write("Enter your choice: ");

      var choice = Console.ReadLine();

      try {
        switch (choice) {
          case "1":
            app.LoadTape();
            break;
          case "2":
            app.Backup();
            break;
          case "3":
            app.Restore();
            break;
          case "4":
            app.EjectTape();
            break;

          case "5":
            app.LoadConfiguration();
            break;
          case "6":
            Console.WriteLine("Exiting...");
            return;
          default:
            Console.WriteLine("Invalid choice. Please try again.");
            break;
        }
      }
      catch (Exception ex) {
        Console.WriteLine($"An error occurred: {ex.Message}");
      }
    }
  }
}
