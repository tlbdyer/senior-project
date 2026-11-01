using Core;

namespace Tests;

// stands in for VlcPlayer: no sound, just remembers what it was told
class FakePlayer : IPlayer
{
    // null when stopped
    public string? Playing { get; private set; }
    public bool Paused { get; private set; }
    public TimeSpan Position { get; private set; }
    public TimeSpan Duration { get; set; }
    public int Volume { get; set; }

    public event Action<bool>? TrackEnded;

    public void Play(string path)
    {
        Playing = path;
        Paused = false;
        Position = TimeSpan.Zero;
    }

    public void Pause() => Paused = true;

    public void Resume() => Paused = false;

    public void Stop() => Playing = null;

    public void Seek(TimeSpan position) => Position = position;

    // what libvlc does when it reaches the end of the file
    public void EndTrack() => TrackEnded?.Invoke(true);

    // what libvlc does when the file or stream can't be played
    public void FailTrack() => TrackEnded?.Invoke(false);
}
