namespace Core;

// the sound side of the player, so tests can swap in a fake one with no sound
public interface IPlayer
{
    // a file path or an http(s) url
    void Play(string location);
    void Pause();
    void Resume();
    void Stop();
    void Seek(TimeSpan position);

    TimeSpan Position { get; }

    // zero until the file has been opened
    TimeSpan Duration { get; }

    // 0 - 100
    int Volume { set; }

    event Action? TrackEnded;
}
