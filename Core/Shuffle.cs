namespace Core;

// algorithm 1 from pseudocode.md
public static class Shuffle
{
    // spreads each artist's songs out so the same artist rarely plays twice in a row
    // O(n log n), from the sort
    public static List<Track> Smart(IEnumerable<Track> tracks, Random random)
    {
        var placed = new List<(double Position, Track Track)>();

        foreach (var artist in tracks.GroupBy(t => t.Artist))
        {
            var songs = artist.ToList();
            FisherYates(songs, random);

            // about 1/n apart, from a random start
            var n = songs.Count;
            var start = random.NextDouble() / n;
            for (var k = 0; k < n; k++)
            {
                // up to 1/10 of the gap either way, so it isn't perfectly regular
                var jitter = (random.NextDouble() * 2 - 1) * 0.1 / n;
                placed.Add((start + (double)k / n + jitter, songs[k]));
            }
        }

        return placed.OrderBy(p => p.Position).Select(p => p.Track).ToList();
    }

    // every order equally likely, O(n)
    public static void FisherYates<T>(IList<T> list, Random random)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            // j can be i, so an item can stay put
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
