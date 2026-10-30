using Core;

namespace Tests;

public class LibraryTests : IDisposable
{
    // each test gets its own empty folder to scan
    readonly string folder = Directory.CreateTempSubdirectory("library-tests-").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    static Track T(string artist, string album, int number, string title) =>
        new($"{title}.mp3", title, artist, album, number, TimeSpan.FromSeconds(180));

    [Fact]
    public void GroupsByArtistThenAlbum()
    {
        var library = new Library([
            T("Pink Floyd", "The Wall", 1, "In the Flesh?"),
            T("Daft Punk", "Discovery", 1, "One More Time"),
            T("Pink Floyd", "Animals", 1, "Pigs on the Wing"),
            T("Pink Floyd", "The Wall", 2, "The Thin Ice"),
        ]);

        Assert.Equal(["Daft Punk", "Pink Floyd"], library.Artists.Select(a => a.Name));
        Assert.Equal(["Animals", "The Wall"], library.Artists[1].Albums.Select(a => a.Title));
        Assert.Equal(2, library.Artists[1].Albums[1].Tracks.Count);
    }

    [Fact]
    public void SortsEachAlbumByTrackNumber()
    {
        var library = new Library([
            T("Daft Punk", "Discovery", 3, "Digital Love"),
            T("Daft Punk", "Discovery", 1, "One More Time"),
            T("Daft Punk", "Discovery", 2, "Aerodynamic"),
        ]);

        var album = library.Artists[0].Albums[0];

        Assert.Equal(["One More Time", "Aerodynamic", "Digital Love"], album.Tracks.Select(t => t.Title));
    }

    [Fact]
    public void TracksIsEveryTrackInBrowseOrder()
    {
        var library = new Library([
            T("Pink Floyd", "Animals", 1, "Pigs on the Wing"),
            T("Daft Punk", "Discovery", 2, "Aerodynamic"),
            T("Daft Punk", "Discovery", 1, "One More Time"),
        ]);

        Assert.Equal(["One More Time", "Aerodynamic", "Pigs on the Wing"], library.Tracks.Select(t => t.Title));
    }

    [Fact]
    public void ScanUsesDefaultsWhenTagsAreMissing()
    {
        var path = WriteWav("untitled.wav");

        var track = Assert.Single(Library.Scan(folder).Tracks);

        Assert.Equal(path, track.Id);
        Assert.Equal("untitled", track.Title);
        Assert.Equal("Unknown Artist", track.Artist);
        Assert.Equal("Unknown Album", track.Album);
        Assert.Null(track.Genre);
        Assert.Null(track.Year);
        Assert.Equal(1, track.Duration.TotalSeconds, precision: 1);
    }

    [Fact]
    public void ScanReadsTags()
    {
        var path = WriteWav("song.wav");
        using (var file = TagLib.File.Create(path))
        {
            file.Tag.Title = "Time";
            file.Tag.Performers = ["Pink Floyd"];
            file.Tag.Album = "The Dark Side of the Moon";
            file.Tag.Track = 4;
            file.Tag.Genres = ["Rock"];
            file.Tag.Year = 1973;
            file.Save();
        }

        var track = Assert.Single(Library.Scan(folder).Tracks);

        Assert.Equal("Time", track.Title);
        Assert.Equal("Pink Floyd", track.Artist);
        Assert.Equal("The Dark Side of the Moon", track.Album);
        Assert.Equal(4, track.TrackNumber);
        Assert.Equal("Rock", track.Genre);
        Assert.Equal(1973, track.Year);
    }

    [Fact]
    public void ScanIgnoresFilesThatAreNotAudio()
    {
        WriteWav("song.wav");
        File.WriteAllText(Path.Combine(folder, "cover.jpg"), "not audio");
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "not audio");

        Assert.Single(Library.Scan(folder).Tracks);
    }

    [Fact]
    public void ScanLooksInSubfolders()
    {
        WriteWav("a.wav");
        WriteWav(Path.Combine("Artist", "Album", "b.WAV"));

        Assert.Equal(2, Library.Scan(folder).Tracks.Count);
    }

    [Fact]
    public void ScanSkipsHiddenFilesAndFolders()
    {
        WriteWav("song.wav");
        WriteWav("._song.wav");
        WriteWav(Path.Combine(".Trash", "deleted.wav"));

        var track = Assert.Single(Library.Scan(folder).Tracks);

        Assert.Equal("song", track.Title);
    }

    [Fact]
    public void ScanSkipsFilesItCannotRead()
    {
        WriteWav("song.wav");
        foreach (var extension in new[] { ".mp3", ".flac", ".ogg", ".m4a", ".wav" })
        {
            File.WriteAllText(Path.Combine(folder, "broken" + extension), "this is text, not audio");
            File.WriteAllText(Path.Combine(folder, "empty" + extension), "");
        }

        var track = Assert.Single(Library.Scan(folder).Tracks);

        Assert.Equal("song", track.Title);
    }

    // one second of silence: 8 kHz, mono, 16-bit
    string WriteWav(string name)
    {
        const int sampleRate = 8000;
        const int dataSize = sampleRate * 2;

        var path = Path.Combine(folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var w = new BinaryWriter(File.Create(path));
        w.Write("RIFF"u8);
        w.Write(36 + dataSize);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);               // size of the fmt chunk
        w.Write((short)1);         // PCM
        w.Write((short)1);         // channels
        w.Write(sampleRate);
        w.Write(sampleRate * 2);   // bytes per second
        w.Write((short)2);         // bytes per sample
        w.Write((short)16);        // bits per sample
        w.Write("data"u8);
        w.Write(dataSize);
        w.Write(new byte[dataSize]);
        return path;
    }
}
