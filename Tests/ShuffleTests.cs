using Core;

namespace Tests;

public class ShuffleTests
{
    static Track T(string artist, int n) =>
        new($"{artist}{n}.mp3", $"{artist}{n}", artist, "Album", n, TimeSpan.FromSeconds(180));

    // 4 artists with 5 songs each
    static List<Track> Library() =>
        (from artist in new[] { "A", "B", "C", "D" }
         from n in Enumerable.Range(1, 5)
         select T(artist, n)).ToList();

    // how many times the next song is by the same artist
    static int BackToBack(List<Track> tracks) =>
        tracks.Zip(tracks.Skip(1)).Count(pair => pair.First.Artist == pair.Second.Artist);

    static string[] Ids(IEnumerable<Track> tracks) => tracks.Select(t => t.Id).ToArray();

    [Fact]
    public void SmartShuffleKeepsEveryTrack()
    {
        List<Track> tracks = [T("A", 1), T("A", 2), T("A", 3), T("B", 1), T("B", 2), T("C", 1)];

        var shuffled = Shuffle.Smart(tracks, new Random(1));

        Assert.Equal(Ids(tracks).Order(), Ids(shuffled).Order());
    }

    [Fact]
    public void SameSeedGivesTheSameOrder()
    {
        var first = Shuffle.Smart(Library(), new Random(42));
        var second = Shuffle.Smart(Library(), new Random(42));

        Assert.Equal(Ids(first), Ids(second));
    }

    [Fact]
    public void EmptyAndSingleTrackAreFine()
    {
        Assert.Empty(Shuffle.Smart([], new Random(1)));
        Assert.Equal(["A1.mp3"], Ids(Shuffle.Smart([T("A", 1)], new Random(1))));
    }

    [Fact]
    public void SmartShuffleSplitsUpArtistsBetterThanNormalShuffle()
    {
        int smart = 0, normal = 0;
        for (var seed = 0; seed < 200; seed++)
        {
            smart += BackToBack(Shuffle.Smart(Library(), new Random(seed)));

            var plain = Library();
            Shuffle.FisherYates(plain, new Random(seed));
            normal += BackToBack(plain);
        }

        // normal shuffle: about 4 per shuffle (19 pairs * 4/19)
        Assert.InRange(normal, 600, 1000);
        Assert.True(smart * 10 < normal, $"smart {smart}, normal {normal}");
    }

    [Fact]
    public void FisherYatesGivesEveryOrderEqually()
    {
        var random = new Random(7);
        var counts = new Dictionary<string, int>();
        for (var i = 0; i < 6000; i++)
        {
            var list = new List<char> { 'a', 'b', 'c' };
            Shuffle.FisherYates(list, random);
            var order = new string(list.ToArray());
            counts[order] = counts.GetValueOrDefault(order) + 1;
        }

        // 6 orders, about 1000 each
        Assert.Equal(6, counts.Count);
        Assert.All(counts.Values, count => Assert.InRange(count, 900, 1100));
    }
}
