using Core;

// placeholder until the real UI, scans a folder and prints what it found
var folder = args.Length > 0
    ? args[0]
    : Environment.GetFolderPath(Environment.SpecialFolder.MyMusic, Environment.SpecialFolderOption.DoNotVerify);

if (!Directory.Exists(folder))
{
    Console.WriteLine($"folder not found: {folder}");
    return;
}

var library = Library.Scan(folder);

foreach (var artist in library.Artists)
{
    Console.WriteLine(artist.Name);
    foreach (var album in artist.Albums)
    {
        Console.WriteLine($"  {album.Title}");
        foreach (var track in album.Tracks)
            Console.WriteLine($"    {track.TrackNumber}. {track.Title} ({track.Duration:m\\:ss})");
    }
}

Console.WriteLine($"{library.Tracks.Count} tracks");
