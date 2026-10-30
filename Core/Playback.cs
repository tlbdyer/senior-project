namespace Core;

// plays the queue: what is playing, pause, seek, volume, and moving on when a track ends
public class Playback
{
    readonly IPlayer player;

    public Queue Queue { get; }

    // null when stopped
    public Track? NowPlaying { get; private set; }

    public bool Paused { get; private set; }

    public int Volume { get; private set; } = 100;

    // the playing track changed or playback stopped, so the screen should redraw
    public event Action? TrackChanged;

    public Playback(Queue queue, IPlayer player)
    {
        Queue = queue;
        this.player = player;
        player.Volume = Volume;
        player.TrackEnded += PlayNext;
    }

    public TimeSpan Position => NowPlaying == null ? TimeSpan.Zero : player.Position;

    // the tags give a duration before the player has opened the file
    public TimeSpan Duration =>
        NowPlaying == null ? TimeSpan.Zero
        : player.Duration > TimeSpan.Zero ? player.Duration
        : NowPlaying.Duration;

    public void PlayNext()
    {
        var track = Queue.Next();
        if (track != null)
            Play(track);
        else
            Stop();
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
        player.Play(track.Path);
        NowPlaying = track;
        Paused = false;
        TrackChanged?.Invoke();
    }
}
