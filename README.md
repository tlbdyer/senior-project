# Terminal Music Player

- music player for the terminal, written in C# (.NET 10)
- plays music from a local folder
- keyboard only

## Features

- scan a folder and read tags (title, artist, album, track no., genre, year, duration)
- browse artist -> album -> track
- play / pause / stop / next / previous / seek / volume
- queue: add, remove, reorder, clear
- now playing bar (track, progress, time)
- help screen with the keys
- album art (maybe)

## Algorithms

1. Smart shuffle - spreads each artist's songs out so the same artist doesn't play twice in a row
2. Recommendations - picks the next song when the queue runs out, from listening history and tags
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

## Requirements

- .NET 10 SDK
- VLC (on Windows / macOS it comes with a NuGet package)
- on Nix: `nix develop`

## Run

- `dotnet build`
- `dotnet test`
- `dotnet run --project Tui` (uses your Music folder)
- `dotnet run --project Tui -- <folder>`
