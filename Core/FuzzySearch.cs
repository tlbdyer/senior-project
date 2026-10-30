namespace Core;

// algorithm 3
public static class FuzzySearch
{
    const int WordStartBonus = 3;
    const int ConsecutiveBonus = 1;

    // tracks matching every query word, best first
    // O(n * text length)
    public static List<Track> Search(string query, IEnumerable<Track> tracks)
    {
        var words = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return tracks.ToList();

        var results = new List<(int Score, Track Track)>();
        foreach (var track in tracks)
        {
            var text = $"{track.Title} {track.Artist} {track.Album}".ToLowerInvariant();

            var total = 0;
            var matched = true;
            foreach (var word in words)
            {
                if (Score(word, text) is not int score)
                {
                    matched = false;
                    break;
                }
                total += score;
            }

            if (matched)
                results.Add((total, track));
        }

        // stable sort: equal scores keep library order
        return results.OrderByDescending(r => r.Score).Select(r => r.Track).ToList();
    }

    // null if word's letters aren't all in text, in order
    // word starts and adjacent letters add, skipped letters subtract
    public static int? Score(string word, string text)
    {
        // step 1: forward, find where the first full match ends
        var i = 0;
        foreach (var c in word)
        {
            while (i < text.Length && text[i] != c)
                i++;
            if (i == text.Length)
                return null;
            i++;
        }
        var end = i - 1;

        // step 2: back from end, shortest window, scoring as we go
        var score = 0;
        var j = end;
        var after = -1;   // where the next letter matched, -1 for the last one
        for (var k = word.Length - 1; k >= 0; k--)
        {
            // step 1 found every letter, so j can't run off the start
            while (text[j] != word[k])
                j--;
            if (j == 0 || !char.IsLetterOrDigit(text[j - 1]))
                score += WordStartBonus;
            // otherwise spread-out word starts beat a match in one piece
            if (j + 1 == after)
                score += ConsecutiveBonus;
            after = j;
            j--;
        }
        var start = j + 1;

        var gaps = end - start + 1 - word.Length;
        return score - gaps;
    }
}
