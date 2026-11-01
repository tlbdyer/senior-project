using Core;
using LibVLCSharp.Shared;
using Terminal.Gui.App;
using Tui;

const string Usage =
    "usage: Tui [folder]\n" +
    "       Tui --server <url> --user <name> --password-file <file>";

IMusicSource source;
Library library;
try
{
    source = ChooseSource(args);
    Console.WriteLine($"loading {source.Name}...");
    library = source.Load();
}
catch (SubsonicException e)
{
    Console.WriteLine(e.Message);
    return;
}
catch (ArgumentException e)
{
    Console.WriteLine($"{e.Message}\n{Usage}");
    return;
}

if (library.Tracks.Count == 0)
{
    Console.WriteLine($"no music found in {source.Name}");
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
    var recommender = new Recommender(new History(History.FileFor(source.Name)));
    var playback = new Playback(new Queue(), player, source, library, recommender);
    using var window = new MainWindow(app, source.Name, library, playback);
    app.Run(window);
}

static IMusicSource ChooseSource(string[] args)
{
    if (args.Length > 0 && args[0].StartsWith("--"))
        return ServerSource(args);

    var folder = args.Length > 0
        ? args[0]
        : Environment.GetFolderPath(Environment.SpecialFolder.MyMusic, Environment.SpecialFolderOption.DoNotVerify);
    if (!Directory.Exists(folder))
        throw new ArgumentException($"folder not found: {folder}");
    // full path: the track ids are paths, and the history has to match from any working directory
    return new FolderSource(Path.GetFullPath(folder));
}

static IMusicSource ServerSource(string[] args)
{
    var options = new Dictionary<string, string>();
    for (var i = 0; i < args.Length; i += 2)
    {
        if (i + 1 >= args.Length || args[i] is not ("--server" or "--user" or "--password-file"))
            throw new ArgumentException($"don't understand {args[i]}");
        options[args[i]] = args[i + 1];
    }

    string Option(string name) =>
        options.TryGetValue(name, out var value) ? value : throw new ArgumentException($"missing {name}");

    // a file and not an argument: arguments can be read by anyone on the machine (ps, /proc)
    var passwordFile = Option("--password-file");
    string password;
    try
    {
        password = File.ReadAllText(passwordFile).Trim();
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
        throw new ArgumentException($"can't read the password file {passwordFile}: {e.Message}");
    }
    if (password.Length == 0)
        throw new ArgumentException($"the password file {passwordFile} is empty");

    var client = new SubsonicClient(Option("--server"), Option("--user"), password);
    client.Ping();
    return new SubsonicSource(client);
}
