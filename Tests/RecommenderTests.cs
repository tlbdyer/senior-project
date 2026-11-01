using Core;

namespace Tests;

public class RecommenderTests
{
    static Track T(string id, string artist, string? genre = null, int? year = null) =>
        new(id, id, artist, "Album", 1, TimeSpan.FromSeconds(180), genre, year);

    static readonly Track Last = T("last", "Pink Floyd", "Rock", 1973);

    [Fact]
    public void WithNoHistoryTheClosestTagsWin()
    {
        var recommender = new Recommender(new History());
        var close = T("close", "Pink Floyd", "Rock", 1973);
        var far = T("far", "Miles Davis", "Jazz", 1959);

        // 1 artist + 1 genre + 1 year against 0
        Assert.Equal(3, recommender.Score(Last, close));
        Assert.Equal(0, recommender.Score(Last, far));
    }

    [Fact]
    public void YearsCountLessFurtherApart()
    {
        var recommender = new Recommender(new History());

        Assert.Equal(0.5, recommender.Score(Last, T("a", "X", year: 1978)), 6);
        Assert.Equal(0, recommender.Score(Last, T("b", "X", year: 1990)));
        Assert.Equal(0, recommender.Score(Last, T("c", "X")));
    }

    [Fact]
    public void HistoryOutweighsTags()
    {
        var history = new History();
        var recommender = new Recommender(history);
        var usual = T("usual", "Miles Davis", "Jazz", 1959);
        var alike = T("alike", "Pink Floyd", "Rock", 1990);

        // usual has always come after last, but was played long enough ago not to count as recent
        for (var i = 0; i < 2; i++)
        {
            history.Record("last", finished: true);
            history.Record("usual", finished: true);
        }
        for (var i = 0; i < 20; i++)
            history.Record($"other{i}", finished: true);

        // 3 * chance 1 against same artist + same genre
        Assert.Equal(3, recommender.Score(Last, usual));
        Assert.Equal(2, recommender.Score(Last, alike));
    }

    [Fact]
    public void SkippedAndRecentTracksScoreLower()
    {
        var history = new History();
        var recommender = new Recommender(history);
        history.Record("skipped", finished: false);
        for (var i = 0; i < 20; i++)
            history.Record($"other{i}", finished: true);
        history.Record("recent", finished: true);

        Assert.Equal(-1, recommender.Score(Last, T("skipped", "X")));
        Assert.Equal(-2, recommender.Score(Last, T("recent", "X")));
    }

    [Fact]
    public void PicksFromTheTopFive()
    {
        var recommender = new Recommender(new History());
        List<Track> good = [.. Enumerable.Range(0, 5).Select(i => T($"good{i}", "Pink Floyd", "Rock", 1973))];
        List<Track> bad = [.. Enumerable.Range(0, 20).Select(i => T($"bad{i}", "Someone Else"))];
        var library = new Library([.. bad, Last, .. good]);

        var picked = new HashSet<string>();
        for (var seed = 0; seed < 100; seed++)
            picked.Add(recommender.Recommend(Last, library, new Random(seed))!.Id);

        Assert.Equal(good.Select(t => t.Id).Order(), picked.Order());
    }

    [Fact]
    public void NeverPicksTheLastTrack()
    {
        var recommender = new Recommender(new History());

        Assert.Null(recommender.Recommend(Last, new Library([Last]), new Random(1)));
        Assert.Equal("only", recommender.Recommend(Last, new Library([Last, T("only", "X")]), new Random(1))?.Id);
    }

    [Fact]
    public void SameSeedGivesTheSamePick()
    {
        var recommender = new Recommender(new History());
        var library = new Library([.. Enumerable.Range(0, 30).Select(i => T($"t{i}", i % 3 == 0 ? "Pink Floyd" : "X"))]);

        Assert.Equal(
            recommender.Recommend(Last, library, new Random(7))?.Id,
            recommender.Recommend(Last, library, new Random(7))?.Id);
    }
}
