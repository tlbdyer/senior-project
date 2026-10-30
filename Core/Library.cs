namespace Core;

public record Album(string Title, List<Track> Tracks);

public record Artist(string Name, List<Album> Albums);

public class Library
{
    static readonly string[] AudioExtensions = [".mp3", ".flac", ".ogg", ".m4a", ".wav"];

    // artist -> album -> track
    public List<Artist> Artists { get; }

    // every track, in the same order as Artists
    public List<Track> Tracks { get; }

    public Library(IEnumerable<Track> tracks)
    {
        Artists = tracks
            .GroupBy(t => t.Artist)
            .OrderBy(artist => artist.Key, StringComparer.OrdinalIgnoreCase)
            .Select(artist => new Artist(artist.Key, artist
                .GroupBy(t => t.Album)
                .OrderBy(album => album.Key, StringComparer.OrdinalIgnoreCase)
                .Select(album => new Album(album.Key, album
                    .OrderBy(t => t.TrackNumber)
                    .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
                .ToList()))
            .ToList();

        Tracks = Artists
            .SelectMany(artist => artist.Albums)
            .SelectMany(album => album.Tracks)
            .ToList();
    }

    public static Library Scan(string folder)
    {
        // hidden files and folders are skipped by default (.Trash, ._song.mp3 from macOS)
        // IgnoreInaccessible: a subfolder we can't open shouldn't stop the whole scan
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };

        var tracks = new List<Track>();
        foreach (var file in Directory.EnumerateFiles(folder, "*", options))
        {
            if (!AudioExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                continue;

            var track = ReadTrack(file);
            if (track != null)
                tracks.Add(track);
        }
        return new Library(tracks);
    }

    static Track? ReadTrack(string file)
    {
        try
        {
            using var tagFile = TagLib.File.Create(file);
            var tag = tagFile.Tag;

            return new Track(
                file,
                OrDefault(tag.Title, Path.GetFileNameWithoutExtension(file)),
                OrDefault(tag.FirstPerformer, "Unknown Artist"),
                OrDefault(tag.Album, "Unknown Album"),
                (int)tag.Track,
                tagFile.Properties?.Duration ?? TimeSpan.Zero,
                string.IsNullOrWhiteSpace(tag.FirstGenre) ? null : tag.FirstGenre.Trim(),
                tag.Year == 0 ? null : (int)tag.Year);
        }
        catch (Exception)
        {
            // taglib doesn't stick to its own CorruptFileException on a broken file
            // (a bad .ogg throws ArgumentOutOfRangeException), so catch everything
            return null;
        }
    }

    // a tag that is only spaces counts as missing
    internal static string OrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
