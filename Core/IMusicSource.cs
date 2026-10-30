namespace Core;

// where the music comes from: a folder on disk or a Subsonic server
public interface IMusicSource
{
    // shown in the window title
    string Name { get; }

    Library Load();

    // what the player should open for this track
    string Open(Track track);
}
