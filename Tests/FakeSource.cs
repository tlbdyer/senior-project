using Core;

namespace Tests;

// opens a track as "stream/<id>", so tests can see that Playback asked the source
class FakeSource : IMusicSource
{
    public string Name => "fake";

    public Library Load() => new([]);

    public string Open(Track track) => $"stream/{track.Id}";
}
