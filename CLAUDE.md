# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Id3TagAutomator is a .NET 10 console app that batch-tags an album folder of MP3 files. Given a directory, it infers/loads album metadata and per-track titles from filenames and writes ID3v2.3 tags (including cover art, artist logo, and lyrics) to every `*.mp3` file in that directory.

## Build & run

Build (from repo root, `Id3TagAutomator.slnx`):
```
dotnet build Id3TagAutomator.slnx
```

Run, passing the album folder as the first argument (if omitted, it defaults to the executable's own directory):
```
dotnet run --project Id3TagAutomator -- "<path-to-album-folder>"
```

Run tests (`Id3TagAutomator.Tests`, xUnit, covers `Program.cs` and `DataObjects/Album.cs` at ~100% line/branch coverage):
```
dotnet test Id3TagAutomator.slnx
```
There is no lint/format tooling configured for `Id3TagAutomator` itself.

## Solution structure

`Id3TagAutomator.slnx` (the modern XML-based solution format, `dotnet sln migrate`d from the legacy `.sln`) contains two unrelated things:
- `Id3TagAutomator/` — this project, the actual app (`Program.cs`, `DataObjects/`).
- `Id3TagAutomator.Tests/` — xUnit test project for `Id3TagAutomator`, referencing it via `ProjectReference` and using `InternalsVisibleTo` to reach its `internal` methods directly.
- `Id3-master/` — a vendored copy of the third-party [ID3.NET](https://github.com/JeevanJames/Id3) library (`Id3.Net`, `Id3.Net.Files`, `Id3.Net.Serialization`, plus its own test project). `Id3TagAutomator.csproj` references `Id3.Net.csproj` directly via `ProjectReference` rather than a NuGet package. Treat `Id3-master/` as third-party vendored source — only touch it if patching the ID3 library itself is actually required, not as part of normal app changes.

## Architecture

All logic lives in `Program.cs`, split into small `internal static` methods (so the test project can exercise each in isolation), and runs in two passes over the target directory:

1. **`ListAll`** — reads and prints existing tags (title/artist/album) for every `*.mp3` in the directory, before and after writing.
2. **`ChangeAll`** — does the actual tagging work, via:
   - `LoadOrCreateAlbum` — loads album metadata from an `Album.json` file in the target directory (`DataObjects/Album.cs`: Artist, Name, Year, Genre). If `Album.json` doesn't exist, it's derived from the directory name (expected format `Artist-AlbumName`) and written out for reuse/editing.
   - `GetImageBytes` — loads cover art from `FrontCover.jpg` and artist logo from `BandOrArtistLogotype.jpg` in the directory, if present, keyed by ID3 `PictureType`.
   - `ParseTrackAndTitle` — for each MP3, the filename is split on `-`; the first segment is the track number and the **third** segment is the title (so the expected form is exactly `<track>-<ignored>-<title>.mp3`, not an arbitrary number of segments).
   - `EnsureLyrics` — lyrics come from a sidecar file `<mp3filename>_lyrics.txt` next to the track; if missing, one is created with placeholder text `"No Lyrics"` and then read back in.
   - `BuildTag` — builds an `Id3Tag` (from the `Id3.Net` library) with copyright, artists, genre, band, year, album, track, title, lyrics, and pictures, then `ChangeAll` writes it via `mp3.WriteTag(tag, Id3Version.V23)`.

Directory/file naming conventions this code depends on (not enforced/validated beyond basic existence checks):
- Album folder name: `Artist-AlbumName`
- Track file name: `<track>-<ignored>-<title>.mp3` (exactly 3 `-`-separated segments)
- `Album.json`, `FrontCover.jpg`, `BandOrArtistLogotype.jpg` optional files in the album folder
- `<trackfile>.mp3_lyrics.txt` optional sidecar per track

`DataObjects/Track.cs` currently exists but is unused (empty class).
