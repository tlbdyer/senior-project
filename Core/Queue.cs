namespace Core;

public class Queue
{
    readonly List<Track> tracks = [];

    public IReadOnlyList<Track> Tracks => tracks;

    // -1 means nothing played yet
    public int Current { get; private set; } = -1;

    public void Add(Track track) => tracks.Add(track);

    public void Remove(int i)
    {
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
