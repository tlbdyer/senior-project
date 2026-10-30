# Pseudocode

- everything except App goes in the Core project (no UI code)
- App is the Tui project
- will probably change once I start coding

## Track

```
Track
    id, title, artist, album, trackNumber, duration   // id: the file path, or the song id on a server
    genre, year   // can be missing, recommendations use them
```

## Library

```
scanLibrary(folder):
    tracks = []
    for each file in folder (and subfolders):
        if file is audio (mp3, flac, ogg, m4a, wav):
            tags = readTags(file)
            // use defaults if a tag is missing
            add Track(file,
                tags.title or file name,
                tags.artist or "Unknown Artist",
                tags.album or "Unknown Album",
                tags.trackNumber, tags.duration,
                tags.genre, tags.year) to tracks
    group tracks by artist, then by album
    sort each album by trackNumber
    return the groups
```

## Sources

- the library comes from a folder or from a Subsonic server; the rest of the app doesn't care which

```
IMusicSource
    name          // shown in the title
    load()        // returns a Library
    open(track)   // what the player opens: a file path or a stream url

FolderSource(folder)
    load(): scanLibrary(folder)
    open(track): track.id   // a local track's id is its path

SubsonicSource(client)
    load(): Library(client.allSongs())
    open(track): client.streamUrl(track.id)   // signed when played, never stored
```

## Subsonic client

```
SubsonicClient(server, user, password)
    refuse http unless the server is this machine   // the token below is easy to attack if seen

    url(endpoint, params):
        salt = 16 random hex characters, new for every request
        token = md5(password + salt)
        return server/rest/endpoint?u=user&t=token&s=salt&v=1.16.1&c=senior-project&f=json&params

    get(endpoint, params):
        answer = http get url(endpoint, params)   // 15s timeout
        if it failed, isn't 200, isn't json, or status != "ok": error with a readable message
        return answer["subsonic-response"]

    ping(): get("ping")   // checks address and login before the app starts

    allSongs():
        tracks = []
        repeat:
            page = get("search3", query = "", songCount = 500, songOffset = length(tracks)).songs
            add each song to tracks as a Track (same defaults as missing tags)
        until length(page) < 500
        return tracks

    streamUrl(id): url("stream", id, format = raw)   // raw: the server doesn't convert the file
```

## Queue

```
Queue
    tracks = []
    current = -1   // -1 means nothing played yet

    add(track):
        append track to tracks

    remove(i):
        delete tracks[i]
        if i <= current: current = current - 1

    move(i, j):
        move tracks[i] to position j
        // current still has to point at the same track after this

    clear():
        tracks = []
        current = -1

    next():
        if current + 1 < length(tracks):
            current = current + 1
            return tracks[current]
        return none

    previous():
        if current > 0:
            current = current - 1
            return tracks[current]
        return none
```

## Player

```
IPlayer
    play(location)   // a file path or an http(s) url
    pause()
    resume()
    stop()
    seek(position)
    setVolume(0-100)
    position, duration
    event TrackEnded

VlcPlayer implements IPlayer using libvlc
    // libvlc sends TrackEnded on its own thread, so it is passed to the app's thread first
    // a stream or file that fails to play counts as ended, so it gets skipped
    // seek goes by fraction of the track (position / duration),
    // seeking by time jumps to the end of some ogg files
```

- interface so tests can use a fake player (no sound)

## Playback

- plays the queue; this was in the App, moved to Core so it can be tested with the fake player

```
Playback(queue, player, source)
    nowPlaying = none   // none = stopped
    paused = false
    volume = 100
    on player.TrackEnded: playNext()

    playNext():
        track = queue.next()
        if track: play(track)
        else: stop()

    playPrevious():
        track = queue.previous()
        if track: play(track)

    togglePause():
        if nowPlaying is none: return
        if paused: player.resume() else: player.pause()
        paused = not paused

    seek(by):
        if nowPlaying is none: return
        player.seek(position + by, kept between 0 and duration)

    changeVolume(by):
        volume = volume + by, kept between 0 and 100
        player.setVolume(volume)

    // duration comes from the tags until the player has opened the file
    duration: player.duration if known, else nowPlaying.duration

    play(track):
        player.play(source.open(track))
        nowPlaying = track
        paused = false
```

## Algorithms

- each one takes a `random` so tests can pass a seeded one and get the same result

### 1. Smart Shuffle

- normal shuffle often plays the same artist twice in a row
- this spreads each artist's songs out evenly instead
- O(n log n) because of the sort

```
smartShuffle(tracks, random):
    placed = []   // list of (position, track)
    for each group in tracks grouped by artist:
        n = length(group)
        fisherYates(group, random)   // shuffle inside the artist first
        start = random number in [0, 1/n)
        for k = 0 to n - 1:
            jitter = random number in [-0.1/n, 0.1/n]
            add (start + k/n + jitter, group[k]) to placed
    sort placed by position
    return just the tracks from placed

fisherYates(list, random):
    for i = length(list) - 1 down to 1:
        j = random integer in [0, i]
        swap list[i] and list[j]
```

- each artist's songs end up about 1/n apart between 0 and 1
- jitter so it's not perfectly regular
- the queue uses it when you press `s`:

```
Queue
    shuffled = false
    original = []

    toggleShuffle(random):
        if shuffled:
            // turning it off, go back to the original order
            tracks = original
            current = index of the playing track in original
        else:
            original = copy of tracks
            // only shuffle the songs that haven't played yet
            replace the tracks after current with smartShuffle(those tracks, random)
        shuffled = not shuffled

    add(track): also append to original if shuffled
    remove(i): also remove tracks[i] from original if shuffled
        // if the track is in the queue twice this can take out the other copy,
        // so after unshuffling the one left may sit where the other was
    clear(): also turns shuffle off
```

### 2. Recommendations

- not built yet, only declared in the code (`IRecommender`): `record(track, finished)` and `recommend(last, library, random)`

- picks the next song when the queue runs out and autoplay is on
- score = listening history (Markov chain of what gets played after what) + tag similarity
- O(n) per pick (one pass over the library)

```
History   // saved to disk so it carries over between runs
    plays = []   // (track, finished) in the order they were played
    follows = map a -> map b -> count   // how many times b got played right after a

    record(track, finished):
        if plays is not empty:
            follows[last track in plays][track] += 1
        add (track, finished) to plays

    chance(a, b):
        if a has no follows: return 0
        return follows[a][b] / total of follows[a]

    skipRate(t):
        return fraction of t's plays that didn't finish

    playedRecently(t):
        return true if t is in the last 20 plays

recommend(last, library, history, random):
    candidates = []
    for each track t in library (skip last):
        score = 3 * history.chance(last, t)
        if t.artist == last.artist:
            score += 1
        if t.genre is known and t.genre == last.genre:
            score += 1
        if both years are known:
            score += 1 - min(abs(t.year - last.year), 10) / 10
        score -= history.skipRate(t)
        if history.playedRecently(t):
            score -= 2
        add (score, t) to candidates
    return a random one of the top 5 candidates
```

- no history yet (first run): chance is 0 for everything, so it goes off the tags (cold start)
- called from:
  - `TrackEnded` -> `history.record(current, finished = true)`
  - `n` (skip) -> `history.record(current, finished = false)`
  - `playNext()`: queue empty + autoplay on -> queue `recommend(last played track, ...)` and play it

### 3. Fuzzy Search

- search as you type
- letters have to be in order but not next to each other (`dsotm` finds Dark Side of the Moon)
- letters at the start of words, letters next to each other and fewer gaps rank higher
- O(n * text length) per keypress, n = number of tracks

```
search(query, library):
    if query is empty: return the whole library
    results = []
    for each track t in library:
        text = lowercase(t.title + " " + t.artist + " " + t.album)
        total = 0
        for each word in lowercase(query) split on spaces:
            s = fuzzyScore(word, text)
            if s is none: skip this track   // every word has to match
            total += s
        add (total, t) to results
    return results sorted by total (highest first)

fuzzyScore(word, text):
    // step 1: go forward to find where the first full match ends
    i = 0
    for each letter c in word:
        move i forward until text[i] == c   // if text runs out, return none
        i += 1
    end = i - 1

    // step 2: go back from end to get the shortest window, scoring along the way
    score = 0
    j = end
    after = -1   // where the letter after this one matched
    for each letter c in word, starting from the last one:
        move j back until text[j] == c
        if j == 0 or text[j - 1] is not a letter or digit:
            score += 3   // letter is at the start of a word ("dc" in "ac/dc" too)
        if j + 1 == after:
            score += 1   // right before the letter after it
        after = j
        j -= 1
    start = j + 1

    gaps = (end - start + 1) - length(word)   // letters in the window that didn't match
    return score - gaps
```

- `/` opens the search box, results update on every keypress
- the +1 for letters next to each other came from testing: without it "money" put
  Harvest Moon (Neil Young) above Money, the word starts in "moon neil young" outscored the gaps

## App (Tui project)

```
main(args):
    source = SubsonicSource(server, user, password from file) if --server
             else FolderSource(args[0] or ~/Music)
    library = source.load()   // errors are printed before the screen opens
    playback = new Playback(new Queue, new VlcPlayer, source)

    draw: library tree | queue list
          now playing bar underneath
    every 0.5s: updateNowPlaying()
    run until quit

on key press:
    Enter  -> add selected track, album or artist to queue
              if nothing is playing: playback.playNext()
    Tab    -> switch between library and queue
    Space  -> playback.togglePause()
    n / p  -> playback.playNext() / playPrevious()
    , / .  -> playback.seek(-5s / +5s)   // Left / Right open and close the tree
    + / -  -> playback.changeVolume(+5 / -5)
    Delete -> remove selected track from queue
    s      -> queue.toggleShuffle(random), queue title says "(shuffled)"
    /      -> search box: search(text, library) on every keypress,
              Up / Down pick a result, Enter adds it, Esc closes
              // the player keys are off while it's open, or typing "n" would skip the song
    ?      -> show help screen
    q      -> stop and quit

updateNowPlaying():
    show playing / paused, title - artist, position / duration, a progress bar and the volume
```
