namespace Core;

public class SubsonicSource(SubsonicClient client) : IMusicSource
{
    public string Name => client.Server.Host;

    public Library Load() => new(client.AllSongs());

    // signed when it is played, so no url with a token in it is ever kept on a track
    public string Open(Track track) => client.StreamUrl(track.Id);
}
