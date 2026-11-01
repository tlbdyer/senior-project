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
    const string KeysLine = " Enter add   Space pause   n/p next/prev   x stop   ,/. seek   +/- volume   Del remove   Shift+Up/Down move   c clear   s shuffle   a autoplay   / search   Tab switch side   ? help   q quit";

    const string HelpText =
        "Up / Down     move\n" +
        "Left / Right  close / open an artist or album\n" +
        "Enter         add the selected track, album or artist to the queue\n" +
        "Space         pause / resume\n" +
        "n / p         next / previous track\n" +
        ", / .         seek back / forward 5 seconds\n" +
        "+ / -         volume up / down\n" +
        "x             stop\n" +
        "Delete        remove the selected track from the queue\n" +
        "Shift+Up/Down move the selected track up / down the queue\n" +
        "c             clear the queue\n" +
        "s             shuffle / unshuffle the songs not played yet\n" +
        "a             autoplay on / off: when the queue runs out, play a recommended song\n" +
        "/             search, Up / Down pick, Enter add to the queue, Esc close\n" +
        "Tab           switch between library and queue\n" +
        "?             this help\n" +
        "q / Esc       quit";

    const int BarWidth = 30;

    readonly Library library;
    readonly Playback playback;
    readonly Queue queue;
    readonly Random random = new();
    readonly TreeView<object> libraryTree = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    readonly ListView queueList = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    readonly Label nowPlayingLabel = new() { Width = Dim.Fill() };
    readonly FrameView queueFrame;

    // while searching, typed letters go to the search box, not the player
    bool searching;

    public MainWindow(IApplication app, string sourceName, Library library, Playback playback)
    {
        Title = $"Music Player - {sourceName}";
        this.library = library;
        this.playback = playback;
        queue = playback.Queue;

        // 4 rows at the bottom are for now playing (3) and the keys line (1)
        var libraryFrame = new FrameView
        {
            Title = $"Library ({library.Tracks.Count} tracks)",
            Width = Dim.Percent(50),
            Height = Dim.Fill(4),
        };
        queueFrame = new FrameView
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
            if (searching)
                return;

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
            // kitty sends shifted keys like + and ? with Shift held, so compare without it
            else if (key.NoShift == '+')
                playback.ChangeVolume(5);
            else if (key == '-')
                playback.ChangeVolume(-5);
            else if (key == Key.X)
                playback.Stop();
            else if (key == Key.A)
                playback.ToggleAutoplay();
            else if (key == Key.C)
            {
                playback.Stop();
                queue.Clear();
                ShowQueue();
            }
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
                RemoveSelected();
            // KeyDown comes before the list's own Shift+Up / Shift+Down (extend selection)
            else if (key == Key.CursorUp.WithShift)
                MoveSelected(-1);
            else if (key == Key.CursorDown.WithShift)
                MoveSelected(1);
            else
                return;
            key.Handled = true;
        };

        // keys that work on either side
        KeyDown += (_, key) =>
        {
            if (key == Key.Q)
            {
                App?.RequestStop();
                key.Handled = true;
            }
            else if (key.NoShift == '?')
            {
                ShowHelp();
                key.Handled = true;
            }
            else if (key == Key.S)
            {
                queue.ToggleShuffle(random);
                ShowQueue();
                key.Handled = true;
            }
            else if (key == '/')
            {
                ShowSearch();
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

    // focus stays in the text box, Up / Down move through the results
    void ShowSearch()
    {
        using var dialog = new Dialog { Title = "Search", Width = Dim.Percent(80), Height = Dim.Percent(80) };
        var field = new TextField { Width = Dim.Fill() };
        var list = new ListView { Y = 1, Width = Dim.Fill(), Height = Dim.Fill() };
        List<Track> results = [];

        void Update()
        {
            results = FuzzySearch.Search(field.Text, library.Tracks);
            var rows = results.Select(t => $"{t.Title} - {t.Artist} ({t.Album})");
            list.SetSource(new ObservableCollection<string>(rows));
            if (results.Count > 0)
                list.SelectedItem = 0;
        }

        field.TextChanged += (_, _) => Update();
        field.KeyDown += (_, key) =>
        {
            if (key == Key.CursorDown)
                list.MoveDown();
            else if (key == Key.CursorUp)
                list.MoveUp();
            else if (key == Key.Enter)
            {
                if (list.SelectedItem is int i && i < results.Count)
                    AddToQueue([results[i]]);
                App!.RequestStop();
            }
            else
                return;
            key.Handled = true;
        };

        dialog.Add(field, list);
        Update();

        searching = true;
        try
        {
            App!.Run(dialog);
        }
        finally
        {
            searching = false;
        }
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

        AddToQueue(tracks);
    }

    void AddToQueue(IEnumerable<Track> tracks)
    {
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

    // by = -1 up, 1 down; the cursor follows the track
    void MoveSelected(int by)
    {
        if (queueList.SelectedItem is not int i || i >= queue.Tracks.Count)
            return;
        var j = i + by;
        if (j < 0 || j >= queue.Tracks.Count)
            return;

        queue.Move(i, j);
        queueList.SelectedItem = j;
        ShowQueue();
    }

    // redraws the list from the queue, keeping the cursor where it was
    void ShowQueue()
    {
        queueFrame.Title = queue.Shuffled ? "Queue (shuffled)" : "Queue";

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
            nowPlayingLabel.Text = $"nothing playing   vol {playback.Volume}%   {AutoplayText()}";
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
            $"{state}  {track.Title} - {track.Artist}   {Time(position)} / {Time(duration)}  [{bar}]  vol {playback.Volume}%   {AutoplayText()}";
    }

    string AutoplayText() => playback.Autoplay ? "autoplay on" : "autoplay off";

    static string Time(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";
}
