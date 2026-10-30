using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Core;

public class SubsonicException(string message) : Exception(message);

// a client for the Subsonic API (Navidrome, Gonic, ...), just the calls the player needs
public class SubsonicClient
{
    const string ApiVersion = "1.16.1";
    const string ClientName = "senior-project";
    const int PageSize = 500;

    readonly HttpClient http;
    readonly string user;
    readonly string password;

    public Uri Server { get; }

    // handler is for tests, so they can answer without a real server
    public SubsonicClient(string server, string user, string password, HttpMessageHandler? handler = null)
    {
        if (!Uri.TryCreate(server.TrimEnd('/'), UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
            throw new SubsonicException($"not a server address: {server}");

        // the token is only md5(password + salt), anyone who sees a request can try passwords against it
        if (url.Scheme == Uri.UriSchemeHttp && !url.IsLoopback)
            throw new SubsonicException($"{server} has to use https, the login would be readable over http");

        Server = url;
        this.user = user;
        this.password = password;
        http = handler == null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromSeconds(15);
    }

    // checks the address and the login before anything else is asked
    public void Ping() => Get("ping");

    // an empty search3 returns every song, PageSize at a time
    public List<Track> AllSongs()
    {
        var tracks = new List<Track>();
        while (true)
        {
            var result = Get("search3",
                ("query", ""), ("artistCount", "0"), ("albumCount", "0"),
                ("songCount", PageSize.ToString()), ("songOffset", tracks.Count.ToString()));

            var page = 0;
            if (result.TryGetProperty("searchResult3", out var search) && search.TryGetProperty("song", out var songs))
            {
                foreach (var song in songs.EnumerateArray())
                {
                    tracks.Add(ToTrack(song));
                    page++;
                }
            }

            if (page < PageSize)
                return tracks;
        }
    }

    // format=raw: the server sends the file as it is instead of converting it
    public string StreamUrl(string id) => Url("stream", ("id", id), ("format", "raw"));

    static Track ToTrack(JsonElement song)
    {
        string? Text(string name) =>
            song.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        int Number(string name) =>
            song.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

        var genre = Text("genre");
        var year = Number("year");

        return new Track(
            Text("id") ?? throw new SubsonicException("the server sent a song without an id"),
            Library.OrDefault(Text("title"), "Unknown Title"),
            Library.OrDefault(Text("artist"), "Unknown Artist"),
            Library.OrDefault(Text("album"), "Unknown Album"),
            Number("track"),
            TimeSpan.FromSeconds(Number("duration")),
            string.IsNullOrWhiteSpace(genre) ? null : genre.Trim(),
            year == 0 ? null : year);
    }

    // every request carries the login: u, a fresh salt s, and t = md5(password + s)
    string Url(string endpoint, params (string Key, string Value)[] parameters)
    {
        var salt = RandomNumberGenerator.GetHexString(16, lowercase: true);
        var token = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(password + salt)));

        (string, string)[] login = [("u", user), ("t", token), ("s", salt), ("v", ApiVersion), ("c", ClientName), ("f", "json")];
        var query = string.Join("&", login.Concat(parameters)
            .Select(p => $"{Uri.EscapeDataString(p.Item1)}={Uri.EscapeDataString(p.Item2)}"));

        return $"{Server.AbsoluteUri.TrimEnd('/')}/rest/{endpoint}?{query}";
    }

    // returns the "subsonic-response" object, or throws with something a person can read
    JsonElement Get(string endpoint, params (string Key, string Value)[] parameters)
    {
        HttpResponseMessage response;
        try
        {
            response = http.GetAsync(Url(endpoint, parameters)).GetAwaiter().GetResult();
        }
        catch (HttpRequestException e)
        {
            throw new SubsonicException($"can't reach {Server.Host}: {e.Message}");
        }
        catch (TaskCanceledException)
        {
            throw new SubsonicException($"{Server.Host} didn't answer within {http.Timeout.TotalSeconds}s");
        }

        using (response)
        {
            if (response.StatusCode != HttpStatusCode.OK)
                throw new SubsonicException($"{Server.Host} answered {(int)response.StatusCode} {response.ReasonPhrase}");

            JsonElement body;
            try
            {
                using var json = JsonDocument.Parse(response.Content.ReadAsStream());
                body = json.RootElement.GetProperty("subsonic-response").Clone();
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                throw new SubsonicException($"{Server.Host} isn't a Subsonic server (its answer wasn't Subsonic JSON)");
            }

            if (!body.TryGetProperty("status", out var status) || status.GetString() != "ok")
            {
                var message = body.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var m)
                    ? m.GetString()
                    : "unknown error";
                throw new SubsonicException($"{Server.Host}: {message}");
            }
            return body;
        }
    }
}
