using Core;

namespace Tests;

public class TrackTests
{
    // history is saved to disk, so a track loaded laterr has to match the one played before
    [Fact]
    public void SameValuesAreEqual()
    {
        var a = new Track("song.mp3", "Song", "Artist", "Album", 1, TimeSpan.FromSeconds(180));
        var b = new Track("song.mp3", "Song", "Artist", "Album", 1, TimeSpan.FromSeconds(180));

        Assert.Equal(a, b);
    }
}
