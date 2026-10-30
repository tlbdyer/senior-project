using Core;

namespace Tests;

public class FuzzySearchTests
{
    static Track T(string title, string artist = "X", string album = "Y") =>
        new($"{title}.mp3", title, artist, album, 1, TimeSpan.FromSeconds(180));

    static string[] Titles(IEnumerable<Track> tracks) => tracks.Select(t => t.Title).ToArray();

    [Fact]
    public void EmptyQueryGivesEverything()
    {
        List<Track> tracks = [T("a"), T("b")];

        Assert.Equal(["a", "b"], Titles(FuzzySearch.Search("", tracks)));
        Assert.Equal(["a", "b"], Titles(FuzzySearch.Search("   ", tracks)));
    }

    [Fact]
    public void LettersInOrderMatchEvenWithGaps()
    {
        List<Track> tracks = [T("Money"), T("The Dark Side of the Moon")];

        Assert.Equal(["The Dark Side of the Moon"], Titles(FuzzySearch.Search("dsotm", tracks)));
    }

    [Fact]
    public void LettersOutOfOrderDontMatch()
    {
        Assert.Null(FuzzySearch.Score("ba", "ab"));
        Assert.Null(FuzzySearch.Score("abc", "ab"));
    }

    [Fact]
    public void ArtistAndAlbumAreSearchedToo()
    {
        List<Track> tracks = [T("Time", "Pink Floyd", "Dark Side"), T("Time", "Hans Zimmer", "Inception")];

        Assert.Equal(["Time"], Titles(FuzzySearch.Search("floyd", tracks)));
        Assert.Single(FuzzySearch.Search("inception", tracks));
    }

    [Fact]
    public void EveryWordHasToMatch()
    {
        List<Track> tracks = [T("Dark Star"), T("Dark Side")];

        Assert.Equal(["Dark Side"], Titles(FuzzySearch.Search("dark side", tracks)));
        Assert.Empty(FuzzySearch.Search("dark zzz", tracks));
    }

    [Fact]
    public void CaseDoesNotMatter()
    {
        Assert.Single(FuzzySearch.Search("DARK", [T("dark side")]));
        Assert.Single(FuzzySearch.Search("dark", [T("DARK SIDE")]));
    }

    [Fact]
    public void StartOfAWordRanksHigher()
    {
        // both match "moon" with no gaps, only one at a word start
        List<Track> tracks = [T("Harmoony"), T("Harvest Moon")];

        Assert.Equal(["Harvest Moon", "Harmoony"], Titles(FuzzySearch.Search("moon", tracks)));
    }

    [Fact]
    public void FewerGapsRankHigher()
    {
        // "beat" has a gap, "better" doesn't
        List<Track> tracks = [T("Beat It"), T("Better")];

        Assert.Equal(["Better", "Beat It"], Titles(FuzzySearch.Search("bet", tracks)));
    }

    [Fact]
    public void ScoreUsesTheShortestWindow()
    {
        // forward only would take the first a (2 gaps), going back finds "ab"
        // 1 = consecutive
        Assert.Equal(1, FuzzySearch.Score("ab", "a xab"));
        // 3 word start + 1 consecutive
        Assert.Equal(4, FuzzySearch.Score("ab", "a ab"));
    }

    [Fact]
    public void PunctuationStartsAWord()
    {
        Assert.Equal(4, FuzzySearch.Score("dc", "ac/dc"));
    }

    [Fact]
    public void AMatchInOnePieceBeatsWordStartsSpreadOut()
    {
        // "moon neil young" has 3 word starts, but "money" is in one piece
        List<Track> tracks = [T("Harvest Moon", "Neil Young", "Harvest Moon"), T("Money", "Pink Floyd", "The Dark Side of the Moon")];

        Assert.Equal("Money", FuzzySearch.Search("money", tracks)[0].Title);
    }
}
