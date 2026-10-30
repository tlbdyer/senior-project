using Core;
using Terminal.Gui.App;
using Tui;

var folder = args.Length > 0
    ? args[0]
    : Environment.GetFolderPath(Environment.SpecialFolder.MyMusic, Environment.SpecialFolderOption.DoNotVerify);

if (!Directory.Exists(folder))
{
    Console.WriteLine($"folder not found: {folder}");
    return;
}

Console.WriteLine($"scanning {folder}...");
var library = Library.Scan(folder);

if (library.Tracks.Count == 0)
{
    Console.WriteLine($"no music found in {folder}");
    return;
}

using IApplication app = Application.Create();
app.Init();

using var window = new MainWindow(library);
app.Run(window);
