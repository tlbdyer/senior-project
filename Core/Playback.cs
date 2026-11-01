namespace Core;

// plays the queue: what is playing, pause, seek, volume, and moving on when a track ends
public class Playback
{
    readonly IPlayer player;
    readonly IMusicSource source;

    // for autoplay, all null = no autoplay
    readonly Library? library;
    readonly IRecommender? recommender;
    readonly Random random;

    // the last track played, still set after stopping, so autoplay knows what to follow
    Track? last;

    public Queue Queue { get; }

    // null when stopped
    public Track? NowPlaying { get; private set; }

    public bool Paused { get; private set; }

    public int Volume { get; private set; } = 100;

    // when the queue runs out, add a recommended track and keep playing
    public bool Autoplay { get; private set; } = true;

    // the playing track changed or playback stopped, so the screen should redraw
    public event Action? TrackChanged;

    public Playback(Queue queue, IPlayer player, IMusicSource source,
        Library? library = null, IRecommender? recommender = null, Random? random = null)
    {
        Queue = queue;
        this.player = player;
        this.source = source;
        this.library = library;
        this.recommender = recommender;
        this.random = random ?? new Random();
        player.Volume = Volume;
        player.TrackEnded += OnTrackEnded;
    }

    public TimeSpan Position => NowPlaying == null ? TimeSpan.Zero : player.Position;

    // the tags give a duration before the player has opened the file
    public TimeSpan Duration =>
        NowPlaying == null ? TimeSpan.Zero
        : player.Duration > TimeSpan.Zero ? player.Duration
        : NowPlaying.Duration;

    // the n key: whatever was playing counts as skipped
    public void PlayNext()
    {
        if (NowPlaying != null)
            recommender?.Record(NowPlaying, finished: false);
        Advance(mayRecommend: true);
    }

    public void ToggleAutoplay() => Autoplay = !Autoplay;

    // played = false: the track failed to play
    void OnTrackEnded(bool played)
    {
        if (played && NowPlaying != null)
            recommender?.Record(NowPlaying, finished: true);

        // after a failure don't recommend, or a dead server would make autoplay try track after track
        Advance(mayRecommend: played);
    }

    void Advance(bool mayRecommend)
    {
        var track = Queue.Next();
        if (track == null && mayRecommend)
            track = QueueRecommended();

        if (track != null)
            Play(track);
        else
            Stop();
    }

    // adds a recommended track to the queue and moves to it, null if autoplay can't pick one
    Track? QueueRecommended()
    {
        if (!Autoplay || recommender == null || library == null || last == null)
            return null;

        var pick = recommender.Recommend(last, library, random);
        if (pick == null)
            return null;

        Queue.Add(pick);
        return Queue.Next();
    }

    // at the first track this does nothing
    public void PlayPrevious()
    {
        var track = Queue.Previous();
        if (track != null)
            Play(track);
    }

    public void TogglePause()
    {
        if (NowPlaying == null)
            return;

        if (Paused)
            player.Resume();
        else
            player.Pause();
        Paused = !Paused;
    }

    public void Stop()
    {
        player.Stop();
        NowPlaying = null;
        Paused = false;
        TrackChanged?.Invoke();
    }

    public void Seek(TimeSpan by)
    {
        if (NowPlaying == null)
            return;

        var to = Position + by;
        if (to < TimeSpan.Zero)
            to = TimeSpan.Zero;
        if (to > Duration)
            to = Duration;
        player.Seek(to);
    }

    public void ChangeVolume(int by)
    {
        Volume = Math.Clamp(Volume + by, 0, 100);
        player.Volume = Volume;
    }

    void Play(Track track)
    {
        player.Play(source.Open(track));
        NowPlaying = track;
        last = track;
        Paused = false;
        TrackChanged?.Invoke();
    }
}
