using Core;

// placeholder until the real UI, just checks that Tui can use Core
var track = new Track("song.mp3", "Song", "Artist", "Album", 1, TimeSpan.FromSeconds(180));
Console.WriteLine($"{track.Artist} - {track.Title}");
