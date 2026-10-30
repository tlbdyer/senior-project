using LibVLCSharp.Shared;

namespace Core;

public sealed class VlcPlayer : IPlayer, IDisposable
{
    readonly LibVLC libvlc;
    readonly MediaPlayer player;
    int volume = 100;

    // counts Play calls, so an end that arrives after the user already skipped is ignored
    int playCount;

    public event Action? TrackEnded;

    // libvlc raises its events on its own thread and deadlocks if you call back into it from there,
    // so the end of a track is handed to dispatch, which runs it on the app's thread
    public VlcPlayer(Action<Action> dispatch)
    {
        LibVLCSharp.Shared.Core.Initialize();
        libvlc = new LibVLC("--no-video");
        player = new MediaPlayer(libvlc);

        // a stream that fails (server gone, broken file) is skipped like one that ended
        player.EncounteredError += (_, _) => Ended(dispatch);
        player.EndReached += (_, _) => Ended(dispatch);
    }

    void Ended(Action<Action> dispatch)
    {
        var ended = playCount;
        dispatch(() =>
        {
            if (ended == playCount)
                TrackEnded?.Invoke();
        });
    }

    public void Play(string location)
    {
        playCount++;
        // the player keeps its own reference to the media, so this one can be disposed
        using var media = Uri.TryCreate(location, UriKind.Absolute, out var url) && !url.IsFile
            ? new Media(libvlc, url)
            : new Media(libvlc, location, FromType.FromPath);
        player.Play(media);
        player.Volume = volume;
    }

    public void Pause() => player.SetPause(true);

    public void Resume() => player.SetPause(false);

    public void Stop()
    {
        playCount++;
        player.Stop();
    }

    // seeking by time jumps to the end of some ogg files (libvlc 3.0.23), seeking by fraction doesn't
    public void Seek(TimeSpan position)
    {
        if (player.Length > 0)
            player.Position = (float)(position.TotalMilliseconds / player.Length);
        else
            player.Time = (long)position.TotalMilliseconds;
    }

    // libvlc gives -1 when there is nothing to report
    public TimeSpan Position => player.Time > 0 ? TimeSpan.FromMilliseconds(player.Time) : TimeSpan.Zero;

    public TimeSpan Duration => player.Length > 0 ? TimeSpan.FromMilliseconds(player.Length) : TimeSpan.Zero;

    public int Volume
    {
        set
        {
            volume = value;
            player.Volume = value;
        }
    }

    public void Dispose()
    {
        player.Stop();
        player.Dispose();
        libvlc.Dispose();
    }
}
