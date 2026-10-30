using System.Collections.ObjectModel;
using Core;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Tui;

// the main screen: library | queue, now playing bar underneath
public class MainWindow : Window
{
    const string KeysLine = " Enter add   Space pause   n/p next/prev   ,/. seek   +/- volume   Del remove   Tab switch side   ? help   q quit";

    const string HelpText =
        "Up / Down     move\n" +
        "Left / Right  close / open an artist or album\n" +
        "Enter         add the selected track, album or artist to the queue\n" +
        "Space         pause / resume\n" +
        "n / p         next / previous track\n" +
        ", / .         seek back / forward 5 seconds\n" +
        "+ / -         volume up / down\n" +
        "Delete        remove the selected track from the queue\n" +
        "Tab           switch between library and queue\n" +
        "?             this help\n" +
        "q / Esc       quit";

    const int BarWidth = 30;

    readonly Playback playback;
    readonly Queue queue;
    readonly TreeView<object> libraryTree = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    readonly ListView queueList = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    readonly Label nowPlayingLabel = new() { Width = Dim.Fill() };

    public MainWindow(IApplication app, Library library, Playback playback)
    {
        Title = "Music Player";
        this.playback = playback;
        queue = playback.Queue;

        // 4 rows at the bottom are for now playing (3) and the keys line (1)
        var libraryFrame = new FrameView
        {
            Title = $"Library ({library.Tracks.Count} tracks)",
            Width = Dim.Percent(50),
            Height = Dim.Fill(4),
        };
        var queueFrame = new FrameView
        {
            Title = "Queue",
            X = Pos.Right(libraryFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill(4),
        };
        var nowPlayingFrame = new FrameView
        {
            Title = "Now Playing",
            Y = Pos.Bottom(libraryFrame),
            Width = Dim.Fill(),
            Height = 3,
        };
        var keysLine = new Label { Text = KeysLine, Y = Pos.Bottom(nowPlayingFrame) };

        // the tree holds Artist, Album and Track objects, so it is a tree of object
        libraryTree.TreeBuilder = new DelegateTreeBuilder<object>(Children, node => node is not Track);
        libraryTree.AspectGetter = Describe;
        libraryTree.AddObjects(library.Artists);

        // both of these jump to an item when you type its first letter, which would eat q, n, p...
        libraryTree.AllowLetterBasedNavigation = false;
        queueList.KeystrokeNavigator = null;

        libraryFrame.Add(libraryTree);
        queueFrame.Add(queueList);
        nowPlayingFrame.Add(nowPlayingLabel);
        Add(libraryFrame, queueFrame, nowPlayingFrame, keysLine);

        playback.TrackChanged += () =>
        {
            ShowQueue();
            ShowNowPlaying();
        };
        ShowNowPlaying();

        // the position moves on its own, so redraw it twice a second
        app.AddTimeout(TimeSpan.FromMilliseconds(500), () =>
        {
            ShowNowPlaying();
            return true;
        });

        // the tree and the list both use Space, so these go to the app, which sees keys before any view
        app.Keyboard.KeyDown += (_, key) =>
        {
            if (key == Key.Space)
                playback.TogglePause();
            else if (key == Key.N)
                playback.PlayNext();
            else if (key == Key.P)
                playback.PlayPrevious();
            else if (key == ',')
                playback.Seek(TimeSpan.FromSeconds(-5));
            else if (key == '.')
                playback.Seek(TimeSpan.FromSeconds(5));
            else if (key == '+')
                playback.ChangeVolume(5);
            else if (key == '-')
                playback.ChangeVolume(-5);
            else
                return;

            ShowNowPlaying();
            key.Handled = true;
        };

        libraryTree.KeyDown += (_, key) =>
        {
            if (key == Key.Enter)
            {
                AddSelected();
                key.Handled = true;
            }
        };

        queueList.KeyDown += (_, key) =>
        {
            if (key == Key.Delete)
            {
                RemoveSelected();
                key.Handled = true;
            }
        };

        // keys that work on either side
        KeyDown += (_, key) =>
        {
            if (key == Key.Q)
            {
                App?.RequestStop();
                key.Handled = true;
            }
            else if (key == '?')
            {
                ShowHelp();
                key.Handled = true;
            }
            else if (key == Key.Tab)
            {
                // Tab only moves inside one frame by default, so switch sides by hand
                if (libraryTree.HasFocus)
                    queueList.SetFocus();
                else
                    libraryTree.SetFocus();
                key.Handled = true;
            }
        };
    }

    // a MessageBox would centre every line, a Label keeps the two columns lined up
    void ShowHelp()
    {
        using var dialog = new Dialog { Title = "Keys" };
        dialog.Add(new Label { Text = HelpText });
        dialog.AddButton(new Button { Title = "OK" });
        App!.Run(dialog);
    }

    static IEnumerable<object> Children(object node) => node switch
    {
        Artist artist => artist.Albums,
        Album album => album.Tracks,
        _ => [],
    };

    static string Describe(object node) => node switch
    {
        Artist artist => artist.Name,
        Album album => album.Title,
        Track track => track.TrackNumber > 0 ? $"{track.TrackNumber}. {track.Title}" : track.Title,
        _ => "",
    };

    void AddSelected()
    {
        IEnumerable<Track> tracks = libraryTree.SelectedObject switch
        {
            Track track => [track],
            Album album => album.Tracks,
            Artist artist => artist.Albums.SelectMany(album => album.Tracks),
            _ => [],
        };

        foreach (var track in tracks)
            queue.Add(track);

        if (playback.NowPlaying == null)
            playback.PlayNext();
        ShowQueue();
    }

    void RemoveSelected()
    {
        if (queueList.SelectedItem is not int i || i >= queue.Tracks.Count)
            return;

        queue.Remove(i);
        ShowQueue();
    }

    // redraws the list from the queue, keeping the cursor where it was
    void ShowQueue()
    {
        var selected = queueList.SelectedItem ?? 0;
        var playing = playback.NowPlaying == null ? -1 : queue.Current;
        var rows = queue.Tracks.Select((track, i) =>
            $"{(i == playing ? "▶" : " ")} {i + 1}. {track.Title} - {track.Artist}");

        queueList.SetSource(new ObservableCollection<string>(rows));
        if (queue.Tracks.Count > 0)
            queueList.SelectedItem = Math.Min(selected, queue.Tracks.Count - 1);
    }

    void ShowNowPlaying()
    {
        if (playback.NowPlaying is not Track track)
        {
            nowPlayingLabel.Text = $"nothing playing   vol {playback.Volume}%";
            return;
        }

        var position = playback.Position;
        var duration = playback.Duration;
        var filled = duration > TimeSpan.Zero
            ? (int)(BarWidth * Math.Min(position / duration, 1))
            : 0;
        var bar = new string('#', filled) + new string('-', BarWidth - filled);
        var state = playback.Paused ? "paused " : "playing";

        nowPlayingLabel.Text =
            $"{state}  {track.Title} - {track.Artist}   {Time(position)} / {Time(duration)}  [{bar}]  vol {playback.Volume}%";
    }

    static string Time(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";
}
