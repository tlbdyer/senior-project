# Pseudocode

- everything except App goes in the Core project (no UI code)
- App is the Tui project
- will probably change once I start coding

## Track

```
Track
    path, title, artist, album, trackNumber, duration
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
                tags.trackNumber, tags.duration) to tracks
    group tracks by artist, then by album
    sort each album by trackNumber
    return the groups
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
    play(path)
    pause()
    resume()
    stop()
    seek(seconds)
    setVolume(0-100)
    position, duration, isPlaying
    event TrackEnded

VlcPlayer implements IPlayer using libvlc
```

- interface so tests can use a fake player (no sound)

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

    // note: while shuffled, add and remove have to update original too
```

### 2. Recommendations

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
- letters at the start of words and fewer gaps rank higher
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
    for each letter c in word, starting from the last one:
        move j back until text[j] == c
        if j == 0 or text[j - 1] == ' ':
            score += 3   // letter is at the start of a word
        j -= 1
    start = j + 1

    gaps = (end - start + 1) - length(word)   // letters in the window that didn't match
    return score - gaps
```

- `/` opens the search box, results update on every keypress

## App (Tui project)

```
main(args):
    library = scanLibrary(args[0] or ~/Music)
    queue = new Queue
    player = new VlcPlayer
    on player.TrackEnded: playNext()   // have to switch to the UI thread first

    draw: library tree | queue list
          now playing bar underneath
    every 0.5s: updateNowPlaying()
    run until quit

on key press:
    Enter  -> add selected track or album to queue
              if nothing is playing: playNext()
    Space  -> pause if playing, otherwise resume
    n      -> playNext()
    p      -> playPrevious()
    Left / Right -> seek -5s / +5s
    + / -  -> volume up / down
    Delete -> remove selected track from queue
    ?      -> show help screen
    q      -> stop and quit

playNext():
    track = queue.next()
    if track: player.play(track.path)
    else: player.stop()

playPrevious():
    track = queue.previous()
    if track: player.play(track.path)

updateNowPlaying():
    show title - artist, position / duration, and a progress bar
```
