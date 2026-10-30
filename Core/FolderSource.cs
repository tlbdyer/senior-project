namespace Core;

public class FolderSource(string folder) : IMusicSource
{
    public string Name => folder;

    public Library Load() => Library.Scan(folder);

    // a local track's id is its path
    public string Open(Track track) => track.Id;
}
