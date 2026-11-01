namespace Core;

// algorithm 2: picks the next song when the queue runs out
// score = what usually gets played after the last song + how alike their tags are
public class Recommender(History history) : IRecommender
{
    const int TopCount = 5;

    public void Record(Track track, bool finished) => history.Record(track.Id, finished);

    // O(n * TopCount): one pass over the library, keeping the best TopCount in a small sorted list
    public Track? Recommend(Track last, Library library, Random random)
    {
        var best = new List<(double Score, Track Track)>();   // highest first
        foreach (var track in library.Tracks)
        {
            if (track.Id == last.Id)
                continue;

            // one step of insertion sort; strict <, so on a tie the earlier track stays ahead
            var score = Score(last, track);
            var i = best.Count;
            while (i > 0 && best[i - 1].Score < score)
                i--;
            if (i == TopCount)
                continue;
            best.Insert(i, (score, track));
            if (best.Count > TopCount)
                best.RemoveAt(TopCount);
        }

        // random among the best, so autoplay doesn't always pick the same song
        return best.Count == 0 ? null : best[random.Next(best.Count)].Track;
    }

    public double Score(Track last, Track track)
    {
        // history counts most: what the user actually plays next
        var score = 3 * history.Chance(last.Id, track.Id);

        if (track.Artist == last.Artist)
            score += 1;
        if (track.Genre != null && track.Genre == last.Genre)
            score += 1;
        // 1 for the same year, down to 0 at 10 years apart
        if (track.Year is int year && last.Year is int lastYear)
            score += 1 - Math.Min(Math.Abs(year - lastYear), 10) / 10.0;

        score -= history.SkipRate(track.Id);
        if (history.PlayedRecently(track.Id))
            score -= 2;

        return score;
    }
}
