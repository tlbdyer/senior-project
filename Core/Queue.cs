namespace Core;

public class Queue
{
    readonly List<Track> tracks = [];

    // order before shuffling, null when not shuffled
    List<Track>? original;

    public IReadOnlyList<Track> Tracks => tracks;

    // -1 means nothing played yet
    public int Current { get; private set; } = -1;

    public bool Shuffled => original != null;

    public void Add(Track track)
    {
        tracks.Add(track);
        original?.Add(track);
    }

    public void Remove(int i)
    {
        // with duplicates this may remove the other copy from original
        original?.Remove(tracks[i]);
        tracks.RemoveAt(i);
        if (i <= Current)
            Current--;
    }

    // j is where the track ends up
    public void Move(int i, int j)
    {
        // check before removing, otherwise a bad j would lose the track
        if (j < 0 || j >= tracks.Count)
            throw new ArgumentOutOfRangeException(nameof(j));

        var track = tracks[i];
        tracks.RemoveAt(i);
        tracks.Insert(j, track);

        // current still has to point at the same track
        if (i == Current)
            Current = j;
        else if (i < Current && j >= Current)
            Current--;
        else if (i > Current && j <= Current)
            Current++;
    }

    public void Clear()
    {
        tracks.Clear();
        Current = -1;
        original = null;
    }

    // shuffles only the tracks not played yet, unshuffle restores the old order
    // Move while shuffled only changes the shuffled order
    public void ToggleShuffle(Random random)
    {
        if (original != null)
        {
            var playing = Current >= 0 ? tracks[Current] : null;
            tracks.Clear();
            tracks.AddRange(original);
            Current = playing == null ? -1 : tracks.IndexOf(playing);
            original = null;
        }
        else
        {
            original = [.. tracks];
            var upcoming = tracks.Skip(Current + 1).ToList();
            tracks.RemoveRange(Current + 1, upcoming.Count);
            tracks.AddRange(Shuffle.Smart(upcoming, random));
        }
    }

    public Track? Next()
    {
        if (Current + 1 >= tracks.Count)
            return null;

        Current++;
        return tracks[Current];
    }

    public Track? Previous()
    {
        if (Current <= 0)
            return null;

        Current--;
        return tracks[Current];
    }
}
