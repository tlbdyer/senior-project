namespace Core;

public record Track(
    string Path,
    string Title,
    string Artist,
    string Album,
    int TrackNumber,
    TimeSpan Duration);
