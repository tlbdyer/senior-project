# Terminal Music Player

- music player for the terminal, written in C# (.NET 10)
- plays music from a local folder, or streams it from a Subsonic server (Navidrome, Gonic, ...)
- keyboard only

## Features

- scan a folder and read tags (title, artist, album, track no., genre, year, duration)
- or load the library from a Subsonic server and stream the songs, nothing downloaded first
- browse artist -> album -> track
- play / pause / stop / next / previous / seek / volume
- queue: add, remove, reorder, clear
- now playing bar (track, progress, time)
- help screen with the keys
- album art (maybe)

## Keys

- `Enter` add the selected track, album or artist to the queue (starts playing if nothing is)
- `Space` pause / resume
- `n` / `p` next / previous track
- `,` / `.` seek back / forward 5 seconds
- `+` / `-` volume
- `Delete` remove from the queue
- `s` shuffle / unshuffle the songs not played yet
- `/` search (Up / Down pick, Enter add to the queue, Esc close)
- `Tab` switch between library and queue
- `?` help, `q` quit

## Algorithms

1. Smart shuffle - spreads each artist's songs out so the same artist doesn't play twice in a row
2. Recommendations - picks the next song when the queue runs out, from listening history and tags (designed, not built yet)
3. Fuzzy search - search as you type, letters have to be in order but not next to each other

## Stack

- C# / .NET 10
- Terminal.Gui 2.5 (UI)
- LibVLCSharp 3.10 + libvlc (playback)
- TagLibSharp 2.3 (tags)
- xUnit (tests)

## Structure

- Core: library, queue, history, algorithms, player (no UI code)
- Tui: the terminal app
- Tests: xUnit tests for Core
- the player is behind an IPlayer interface so tests can use a fake one
- the music comes from an IMusicSource: FolderSource (files) or SubsonicSource (a server)
- SubsonicClient talks to the server with HttpClient + System.Text.Json (no extra library)

## Requirements

- .NET 10 SDK
- VLC (on Windows / macOS it comes with a NuGet package)
- on Nix: `nix develop`

## Run

- `dotnet build`
- `dotnet test`
- `dotnet run --project Tui` (uses Music/ folder)
- `dotnet run --project Tui -- <folder>`
- `dotnet run --project Tui -- --server <url> --user <name> --password-file <file>`
  - the password is read from a file so it never shows up in the process list
  - the server has to be https (plain http only to this machine, for testing)
