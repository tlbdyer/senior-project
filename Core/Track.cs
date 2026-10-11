namespace Core;

public record Track(
    string Path,
    string Title,
    string Artist,
    string Album,
    int TrackNumber,
    TimeSpan Duration,
    string? Genre = null,   // null = no tag
    int? Year = null);
