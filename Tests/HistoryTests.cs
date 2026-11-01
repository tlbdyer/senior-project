using Core;

namespace Tests;

public class HistoryTests
{
    [Fact]
    public void ChanceCountsWhatPlayedNext()
    {
        var history = new History();
        foreach (var id in new[] { "a", "b", "a", "b", "a", "c" })
            history.Record(id, finished: true);

        // after a: b twice, c once
        Assert.Equal(2.0 / 3, history.Chance("a", "b"), 6);
        Assert.Equal(1.0 / 3, history.Chance("a", "c"), 6);
        Assert.Equal(0, history.Chance("a", "a"));
        Assert.Equal(0, history.Chance("never played", "a"));
    }

    [Fact]
    public void SkipRateIsTheFractionSkipped()
    {
        var history = new History();
        history.Record("a", finished: true);
        history.Record("a", finished: false);
        history.Record("a", finished: false);
        history.Record("b", finished: true);

        Assert.Equal(2.0 / 3, history.SkipRate("a"), 6);
        Assert.Equal(0, history.SkipRate("b"));
        Assert.Equal(0, history.SkipRate("never played"));
    }

    [Fact]
    public void OnlyTheLast20CountAsRecent()
    {
        var history = new History();
        for (var i = 0; i < 21; i++)
            history.Record($"t{i}", finished: true);

        Assert.False(history.PlayedRecently("t0"));
        Assert.True(history.PlayedRecently("t1"));
        Assert.True(history.PlayedRecently("t20"));
    }

    [Fact]
    public void HistoryIsReadBackFromTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"history-test-{Guid.NewGuid()}", "history.log");
        try
        {
            var first = new History(path);
            first.Record("a", finished: true);
            first.Record("b", finished: false);
            first.Record("a", finished: true);

            var second = new History(path);

            Assert.Equal(1, second.Chance("a", "b"));
            Assert.Equal(1, second.SkipRate("b"));
            Assert.True(second.PlayedRecently("a"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void BrokenLinesAreSkipped()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "1\ta\ngarbage\n2\tx\n\n0\tb\n1\t/music/with\ttab.mp3\n");

            var history = new History(path);

            Assert.Equal(1, history.Chance("a", "b"));
            Assert.Equal(1, history.SkipRate("b"));
            // only the first tab splits, so a tab inside the id is kept
            Assert.True(history.PlayedRecently("/music/with\ttab.mp3"));
            Assert.False(history.PlayedRecently("x"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FileNamesDifferPerSource()
    {
        Assert.NotEqual(History.FileFor("/home/me/Music"), History.FileFor("music.example.com"));
        Assert.Equal(History.FileFor("/home/me/Music"), History.FileFor("/home/me/Music"));
    }
}
