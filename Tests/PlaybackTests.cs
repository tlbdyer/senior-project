using Core;

namespace Tests;

public class PlaybackTests
{
    readonly FakePlayer player = new();

    static Track T(string title) =>
        new($"{title}.mp3", title, "Artist", "Album", 1, TimeSpan.FromSeconds(180));

    Playback PlaybackOf(params string[] titles)
    {
        var queue = new Queue();
        foreach (var title in titles)
            queue.Add(T(title));
        return new Playback(queue, player);
    }

    [Fact]
    public void NothingPlaysAtTheStart()
    {
        var playback = PlaybackOf("A", "B");

        Assert.Null(playback.NowPlaying);
        Assert.Null(player.Playing);
        Assert.Equal(TimeSpan.Zero, playback.Position);
    }

    [Fact]
    public void NextPlaysTheQueueInOrder()
    {
        var playback = PlaybackOf("A", "B");

        playback.PlayNext();
        Assert.Equal("A", playback.NowPlaying?.Title);
        Assert.Equal("A.mp3", player.Playing);

        playback.PlayNext();
        Assert.Equal("B", playback.NowPlaying?.Title);
        Assert.Equal("B.mp3", player.Playing);
    }

    [Fact]
    public void NextAtTheEndStops()
    {
        var playback = PlaybackOf("A");

        playback.PlayNext();
        playback.PlayNext();

        Assert.Null(playback.NowPlaying);
        Assert.Null(player.Playing);
    }

    [Fact]
    public void NextOnAnEmptyQueueStaysStopped()
    {
        var playback = PlaybackOf();

        playback.PlayNext();

        Assert.Null(playback.NowPlaying);
    }

    [Fact]
    public void TracksAddedAfterTheEndPlayNext()
    {
        var playback = PlaybackOf("A");
        playback.PlayNext();
        playback.PlayNext();

        playback.Queue.Add(T("B"));
        playback.PlayNext();

        Assert.Equal("B", playback.NowPlaying?.Title);
    }

    [Fact]
    public void PreviousGoesBack()
    {
        var playback = PlaybackOf("A", "B");
        playback.PlayNext();
        playback.PlayNext();

        playback.PlayPrevious();

        Assert.Equal("A", playback.NowPlaying?.Title);
        Assert.Equal("A.mp3", player.Playing);
    }

    [Fact]
    public void PreviousAtTheFirstTrackKeepsPlayingIt()
    {
        var playback = PlaybackOf("A", "B");
        playback.PlayNext();

        playback.PlayPrevious();

        Assert.Equal("A", playback.NowPlaying?.Title);
    }

    [Fact]
    public void EndOfATrackPlaysTheNextOne()
    {
        var playback = PlaybackOf("A", "B");
        playback.PlayNext();

        player.EndTrack();

        Assert.Equal("B", playback.NowPlaying?.Title);
        Assert.Equal("B.mp3", player.Playing);
    }

    [Fact]
    public void EndOfTheLastTrackStops()
    {
        var playback = PlaybackOf("A");
        playback.PlayNext();

        player.EndTrack();

        Assert.Null(playback.NowPlaying);
        Assert.Null(player.Playing);
    }

    [Fact]
    public void RemovingThePlayingTrackStillPlaysTheOneAfterIt()
    {
        var playback = PlaybackOf("A", "B", "C");
        playback.PlayNext();
        playback.PlayNext();

        playback.Queue.Remove(1);
        player.EndTrack();

        Assert.Equal("C", playback.NowPlaying?.Title);
    }

    [Fact]
    public void TogglePausePausesThenResumes()
    {
        var playback = PlaybackOf("A");
        playback.PlayNext();

        playback.TogglePause();
        Assert.True(playback.Paused);
        Assert.True(player.Paused);

        playback.TogglePause();
        Assert.False(playback.Paused);
        Assert.False(player.Paused);
    }

    [Fact]
    public void TogglePauseDoesNothingWhenStopped()
    {
        var playback = PlaybackOf("A");

        playback.TogglePause();

        Assert.False(playback.Paused);
        Assert.False(player.Paused);
    }

    [Fact]
    public void PlayingANewTrackUnpauses()
    {
        var playback = PlaybackOf("A", "B");
        playback.PlayNext();
        playback.TogglePause();

        playback.PlayNext();

        Assert.False(playback.Paused);
    }

    [Fact]
    public void SeekMovesFromThePosition()
    {
        var playback = PlaybackOf("A");
        playback.PlayNext();

        playback.Seek(TimeSpan.FromSeconds(5));
        playback.Seek(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(10), player.Position);
    }

    [Fact]
    public void SeekStopsAtTheStart()
    {
        var playback = PlaybackOf("A");
        playback.PlayNext();

        playback.Seek(TimeSpan.FromSeconds(-5));

        Assert.Equal(TimeSpan.Zero, player.Position);
    }

    [Fact]
    public void SeekStopsAtTheEnd()
    {
        var playback = PlaybackOf("A");
        playback.PlayNext();
        player.Duration = TimeSpan.FromSeconds(60);

        playback.Seek(TimeSpan.FromSeconds(100));

        Assert.Equal(TimeSpan.FromSeconds(60), player.Position);
    }

    [Fact]
    public void DurationComesFromTheTagsUntilThePlayerKnowsIt()
    {
        var playback = PlaybackOf("A");
        playback.PlayNext();
        Assert.Equal(TimeSpan.FromSeconds(180), playback.Duration);

        player.Duration = TimeSpan.FromSeconds(181);
        Assert.Equal(TimeSpan.FromSeconds(181), playback.Duration);
    }

    [Fact]
    public void VolumeStaysBetween0And100()
    {
        var playback = PlaybackOf("A");
        Assert.Equal(100, player.Volume);

        playback.ChangeVolume(10);
        Assert.Equal(100, playback.Volume);

        playback.ChangeVolume(-30);
        Assert.Equal(70, player.Volume);

        playback.ChangeVolume(-100);
        Assert.Equal(0, player.Volume);
    }

    [Fact]
    public void TrackChangedFiresOnPlayAndStop()
    {
        var playback = PlaybackOf("A");
        var changes = 0;
        playback.TrackChanged += () => changes++;

        playback.PlayNext();
        playback.PlayNext();

        Assert.Equal(2, changes);
    }
}
