using System.Security.Cryptography;
using System.Text;

namespace Core;

// what has been played and what got skipped, for recommendations
// saved as a log with one line per play ("1<tab>id" finished, "0<tab>id" skipped),
// so recording a play is one append instead of rewriting the whole file
public class History
{
    const int RecentCount = 20;

    readonly string? path;

    // follows[a][b] = how many times b was played right after a
    readonly Dictionary<string, Dictionary<string, int>> follows = [];
    readonly Dictionary<string, int> followTotals = [];
    readonly Dictionary<string, int> plays = [];
    readonly Dictionary<string, int> skips = [];

    // the last RecentCount plays, oldest first
    readonly List<string> recent = [];

    string? last;

    // path null = only in memory (tests)
    public History(string? path = null)
    {
        this.path = path;
        if (path != null)
            Load(path);
    }

    // one file per source: a folder path can't be a file name, so the name is a hash of it
    public static string FileFor(string sourceName)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sourceName)))[..16];
        var data = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        return Path.Combine(data, "senior-project", $"history-{hash}.log");
    }

    // O(1)
    public void Record(string id, bool finished)
    {
        Add(id, finished);

        // a newline in the id would split it over two lines, so that play just isn't saved
        if (path == null || id.Contains('\n'))
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"{(finished ? '1' : '0')}\t{id}\n");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // the music shouldn't stop because the history can't be saved
        }
    }

    // the chance that b is played after a, from what has been played after a so far
    public double Chance(string a, string b)
    {
        if (!followTotals.TryGetValue(a, out var total) || !follows[a].TryGetValue(b, out var count))
            return 0;
        return (double)count / total;
    }

    // the fraction of id's plays that were skipped, 0 if never played
    public double SkipRate(string id) =>
        plays.TryGetValue(id, out var count) ? (double)skips.GetValueOrDefault(id) / count : 0;

    public bool PlayedRecently(string id) => recent.Contains(id);

    void Add(string id, bool finished)
    {
        if (last != null)
        {
            if (!follows.TryGetValue(last, out var after))
                follows[last] = after = [];
            after[id] = after.GetValueOrDefault(id) + 1;
            followTotals[last] = followTotals.GetValueOrDefault(last) + 1;
        }

        plays[id] = plays.GetValueOrDefault(id) + 1;
        if (!finished)
            skips[id] = skips.GetValueOrDefault(id) + 1;

        recent.Add(id);
        if (recent.Count > RecentCount)
            recent.RemoveAt(0);

        last = id;
    }

    // replays the log; lines that don't look right are skipped
    void Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;
            foreach (var line in File.ReadLines(path))
            {
                if (line.Length > 2 && line[1] == '\t' && line[0] is '0' or '1')
                    Add(line[2..], line[0] == '1');
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // an unreadable history starts empty
        }
    }
}
