using Core;

namespace Tests;

public class QueueTests
{
    static Track T(string title) =>
        new($"{title}.mp3", title, "Artist", "Album", 1, TimeSpan.FromSeconds(180));

    static Queue QueueOf(params string[] titles)
    {
        var queue = new Queue();
        foreach (var title in titles)
            queue.Add(T(title));
        return queue;
    }

    static string[] Titles(Queue queue) => queue.Tracks.Select(t => t.Title).ToArray();

    [Fact]
    public void EmptyQueueHasNothingToPlay()
    {
        var queue = new Queue();

        Assert.Null(queue.Next());
        Assert.Null(queue.Previous());
        Assert.Equal(-1, queue.Current);
    }

    [Fact]
    public void NextPlaysInOrderAndStopsAtTheEnd()
    {
        var queue = QueueOf("a", "b");

        Assert.Equal("a", queue.Next()?.Title);
        Assert.Equal("b", queue.Next()?.Title);
        Assert.Null(queue.Next());
        Assert.Equal(1, queue.Current);
    }

    [Fact]
    public void PreviousGoesBackAndStopsAtTheStart()
    {
        var queue = QueueOf("a", "b");
        queue.Next();
        queue.Next();

        Assert.Equal("a", queue.Previous()?.Title);
        Assert.Null(queue.Previous());
        Assert.Equal(0, queue.Current);
    }

    [Fact]
    public void RemoveBeforeCurrentKeepsTheSameTrackPlaying()
    {
        var queue = QueueOf("a", "b", "c");
        queue.Next();
        queue.Next();   // playing b

        queue.Remove(0);

        Assert.Equal(["b", "c"], Titles(queue));
        Assert.Equal("b", queue.Tracks[queue.Current].Title);
    }

    [Fact]
    public void RemoveCurrentMakesNextPlayTheOneAfterIt()
    {
        var queue = QueueOf("a", "b", "c");
        queue.Next();
        queue.Next();   // playing b

        queue.Remove(1);

        Assert.Equal("c", queue.Next()?.Title);
    }

    [Fact]
    public void RemoveAfterCurrentChangesNothing()
    {
        var queue = QueueOf("a", "b", "c");
        queue.Next();   // playing a

        queue.Remove(2);

        Assert.Equal(["a", "b"], Titles(queue));
        Assert.Equal(0, queue.Current);
    }

    [Fact]
    public void MovePutsTheTrackAtTheNewPosition()
    {
        var queue = QueueOf("a", "b", "c", "d");

        queue.Move(0, 2);

        Assert.Equal(["b", "c", "a", "d"], Titles(queue));
    }

    // tries every move with every track playing
    [Fact]
    public void MoveKeepsCurrentOnTheSameTrack()
    {
        const int n = 5;
        for (var current = 0; current < n; current++)
        for (var i = 0; i < n; i++)
        for (var j = 0; j < n; j++)
        {
            var queue = QueueOf("a", "b", "c", "d", "e");
            for (var k = 0; k <= current; k++)
                queue.Next();
            var playing = queue.Tracks[queue.Current];

            queue.Move(i, j);

            Assert.Equal(playing, queue.Tracks[queue.Current]);
        }
    }

    [Fact]
    public void MoveToABadPositionLeavesTheQueueAlone()
    {
        var queue = QueueOf("a", "b", "c");

        Assert.Throws<ArgumentOutOfRangeException>(() => queue.Move(0, 3));
        Assert.Equal(["a", "b", "c"], Titles(queue));
    }

    [Fact]
    public void ClearEmptiesTheQueue()
    {
        var queue = QueueOf("a", "b");
        queue.Next();

        queue.Clear();

        Assert.Empty(queue.Tracks);
        Assert.Equal(-1, queue.Current);
    }

    [Fact]
    public void ShuffleOnlyMovesTracksNotPlayedYet()
    {
        var queue = QueueOf("a", "b", "c", "d", "e", "f");
        queue.Next();
        queue.Next();

        queue.ToggleShuffle(new Random(1));

        Assert.True(queue.Shuffled);
        Assert.Equal(1, queue.Current);
        Assert.Equal(["a", "b"], Titles(queue)[..2]);
        Assert.Equal(["c", "d", "e", "f"], Titles(queue)[2..].Order());
    }

    [Fact]
    public void UnshuffleGoesBackAndKeepsThePlayingTrack()
    {
        var queue = QueueOf("a", "b", "c", "d", "e", "f");
        queue.Next();
        queue.ToggleShuffle(new Random(1));
        queue.Next();
        queue.Next();
        var playing = queue.Tracks[queue.Current];

        queue.ToggleShuffle(new Random(1));

        Assert.False(queue.Shuffled);
        Assert.Equal(["a", "b", "c", "d", "e", "f"], Titles(queue));
        Assert.Equal(playing, queue.Tracks[queue.Current]);
    }

    [Fact]
    public void UnshuffleBeforeAnythingPlayedStaysAtTheStart()
    {
        var queue = QueueOf("a", "b", "c");

        queue.ToggleShuffle(new Random(1));
        queue.ToggleShuffle(new Random(1));

        Assert.Equal(["a", "b", "c"], Titles(queue));
        Assert.Equal(-1, queue.Current);
    }

    [Fact]
    public void AddAndRemoveWhileShuffledAreKeptAfterUnshuffle()
    {
        var queue = QueueOf("a", "b", "c");
        queue.ToggleShuffle(new Random(1));

        queue.Add(T("d"));
        queue.Remove(Array.IndexOf(Titles(queue), "b"));
        queue.ToggleShuffle(new Random(1));

        Assert.Equal(["a", "c", "d"], Titles(queue));
    }

    [Fact]
    public void ClearEndsShuffle()
    {
        var queue = QueueOf("a", "b");
        queue.ToggleShuffle(new Random(1));

        queue.Clear();

        Assert.False(queue.Shuffled);
    }
}
