namespace Core;

public record Track(
    string Id,   // the file path, or the song's id on the server
    string Title,
    string Artist,
    string Album,
    int TrackNumber,
    TimeSpan Duration,
    string? Genre = null,   // null = no tag
    int? Year = null);
