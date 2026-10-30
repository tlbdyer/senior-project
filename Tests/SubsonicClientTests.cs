using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Core;

namespace Tests;

public class SubsonicClientTests
{
    // answers every request with whatever the test hands it, and remembers what was asked
    class FakeServer(Func<Uri, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(answer(request.RequestUri!));
        }
    }

    static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new Dictionary<string, object> { ["subsonic-response"] = body })),
    };

    static HttpResponseMessage Ok() => Json(new { status = "ok" });

    static HttpResponseMessage Songs(IEnumerable<object> songs) =>
        Json(new { status = "ok", searchResult3 = new { song = songs } });

    static object Song(int n) => new { id = $"id{n}", title = $"Song {n}", artist = "Artist", album = "Album", track = n, duration = 200 };

    static SubsonicClient Client(FakeServer server) => new("https://music.example", "me", "secret", server);

    static string? Param(Uri url, string name) => HttpUtility.ParseQueryString(url.Query)[name];

    [Fact]
    public void SendsATokenNotThePassword()
    {
        var server = new FakeServer(_ => Ok());

        Client(server).Ping();

        var url = server.Requests.Single();
        var salt = Param(url, "s")!;
        var expected = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes("secret" + salt)));
        Assert.Equal("/rest/ping", url.AbsolutePath);
        Assert.Equal("me", Param(url, "u"));
        Assert.Equal(expected, Param(url, "t"));
        Assert.Equal("json", Param(url, "f"));
        Assert.Null(Param(url, "p"));
        Assert.DoesNotContain("secret", url.ToString());
    }

    [Fact]
    public void SaltIsNewEveryRequest()
    {
        var server = new FakeServer(_ => Ok());
        var client = Client(server);

        client.Ping();
        client.Ping();

        Assert.NotEqual(Param(server.Requests[0], "s"), Param(server.Requests[1], "s"));
        Assert.NotEqual(Param(server.Requests[0], "t"), Param(server.Requests[1], "t"));
    }

    [Fact]
    public void WrongPasswordGivesTheServersMessage()
    {
        var server = new FakeServer(_ => Json(new { status = "failed", error = new { code = 40, message = "Wrong username or password" } }));

        var e = Assert.Throws<SubsonicException>(() => Client(server).Ping());

        Assert.Contains("Wrong username or password", e.Message);
    }

    [Fact]
    public void HttpErrorIsReported()
    {
        var server = new FakeServer(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));

        var e = Assert.Throws<SubsonicException>(() => Client(server).Ping());

        Assert.Contains("502", e.Message);
    }

    [Fact]
    public void SomethingThatIsntSubsonicIsReported()
    {
        var server = new FakeServer(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>login</html>") });

        var e = Assert.Throws<SubsonicException>(() => Client(server).Ping());

        Assert.Contains("isn't a Subsonic server", e.Message);
    }

    [Fact]
    public void UnreachableServerIsReported()
    {
        var server = new FakeServer(_ => throw new HttpRequestException("Connection refused"));

        var e = Assert.Throws<SubsonicException>(() => Client(server).Ping());

        Assert.Contains("can't reach music.example", e.Message);
    }

    [Fact]
    public void TimeoutIsReported()
    {
        var server = new FakeServer(_ => throw new TaskCanceledException());

        var e = Assert.Throws<SubsonicException>(() => Client(server).Ping());

        Assert.Contains("didn't answer", e.Message);
    }

    [Fact]
    public void PlainHttpIsRefused()
    {
        var e = Assert.Throws<SubsonicException>(() => new SubsonicClient("http://music.example", "me", "secret"));

        Assert.Contains("https", e.Message);
    }

    [Fact]
    public void PlainHttpIsAllowedOnThisMachine()
    {
        var client = new SubsonicClient("http://127.0.0.1:4533", "me", "secret");

        Assert.Equal("127.0.0.1", client.Server.Host);
    }

    [Fact]
    public void AllSongsReadsEveryPage()
    {
        var server = new FakeServer(url =>
            Param(url, "songOffset") == "0"
                ? Songs(Enumerable.Range(0, 500).Select(Song))
                : Songs(Enumerable.Range(500, 3).Select(Song)));

        var tracks = Client(server).AllSongs();

        Assert.Equal(503, tracks.Count);
        Assert.Equal(["0", "500"], server.Requests.Select(url => Param(url, "songOffset")));
        Assert.All(server.Requests, url => Assert.Equal("", Param(url, "query")));
    }

    [Fact]
    public void AllSongsOnAnEmptyServer()
    {
        var server = new FakeServer(_ => Json(new { status = "ok", searchResult3 = new { } }));

        Assert.Empty(Client(server).AllSongs());
    }

    [Fact]
    public void ReadsASong()
    {
        var server = new FakeServer(_ => Songs([new
        {
            id = "abc", title = "Roygbiv", artist = "Boards of Canada", album = "Music Has the Right to Children",
            track = 5, duration = 151, genre = "Electronic", year = 1998,
        }]));

        var track = Client(server).AllSongs().Single();

        Assert.Equal(new Track("abc", "Roygbiv", "Boards of Canada", "Music Has the Right to Children",
            5, TimeSpan.FromSeconds(151), "Electronic", 1998), track);
    }

    [Fact]
    public void MissingTagsGetTheSameDefaultsAsFiles()
    {
        var server = new FakeServer(_ => Songs([new { id = "abc", title = "  ", genre = "" }]));

        var track = Client(server).AllSongs().Single();

        Assert.Equal(new Track("abc", "Unknown Title", "Unknown Artist", "Unknown Album", 0, TimeSpan.Zero), track);
    }

    [Fact]
    public void StreamUrlIsSignedAndEscaped()
    {
        var url = new Uri(Client(new FakeServer(_ => Ok())).StreamUrl("a b&c"));

        Assert.Equal("/rest/stream", url.AbsolutePath);
        Assert.Equal("a b&c", Param(url, "id"));
        Assert.Equal("raw", Param(url, "format"));
        Assert.NotNull(Param(url, "t"));
    }
}
