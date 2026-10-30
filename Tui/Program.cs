using Core;
using LibVLCSharp.Shared;
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

// before Init, so the error shows in the normal terminal and not under the app's screen
VlcPlayer player;
try
{
    player = new VlcPlayer(action => app.Invoke(action));
}
catch (VLCException e)
{
    Console.WriteLine($"could not start libvlc, is VLC installed? ({e.Message})");
    return;
}

using (player)
{
    app.Init();
    using var window = new MainWindow(app, library, new Playback(new Queue(), player));
    app.Run(window);
}
