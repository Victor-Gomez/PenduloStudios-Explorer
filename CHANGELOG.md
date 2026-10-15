# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [6.5.0] - 2026-10-15

### Added
- **Continuous Integration (`ci.yml`)**: Automated multi-platform test pipeline on Windows and Linux runners with weekly scheduled runs (`cron: '0 6 * * 1'`).
- **Dependabot (`dependabot.yml`)**: Automated monthly dependency checks for NuGet packages and GitHub Actions.
- **Headless Linux InterFont**: Added `.WithInterFont()` in test application builder for headless Linux test stability.
- **Project License & Badges**: Added MIT License file, status badges (CI, Release, .NET 10, Platform, License), and a prominent Download section to README.
- **Navigation Mesh Centroid Graphs**: Represent navigation meshes as centroid graphs with portal gates and dedicated tree type filter.
- **Runaway 3 Empty Frames Support**: Allow empty frames (`SegmentCount == 0`) with dummy coordinates during sprite validation and bounds calculation.

### Changed
- Updated Avalonia packages (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Diagnostics`, `Avalonia.Fonts.Inter`, `Avalonia.Themes.Fluent`, `Avalonia.Headless`, `Avalonia.Headless.XUnit`) from 11.3.18 to 11.3.22.
- Updated LibVLCSharp and LibVLCSharp.Avalonia from 3.10.0 to 3.10.2.
- Updated VideoLAN.LibVLC.Windows from 3.0.23.1 to 3.0.24.
- Updated Microsoft.NET.Test.Sdk from 17.11.1 to 17.14.1 and xUnit from 2.9.2 to 2.9.3.

## [6.4.2] - 2026-10-11

### Added
- Route preview visualizer for precomputed path matrices.
- Moved known asset dictionaries to embedded JSON resources.
- Resolved Runaway 3 friendly name collisions.

## [6.4.1] - 2026-09-28

### Added
- Sprite atlas grid view.
- Hide swallowed data slots in scene trees.

## [6.0.0] - 2026-09-23

### Added
- Full support for *Hollywood Monsters* (1997): 8-bit paletted rasters, Smacker video playback, sound effects, and obfuscated script deciphering.

## [5.0.0] - 2026-09-14

### Added
- Support for *The Next BIG Thing* (2011) and *Yesterday* (2012): scene canvas geometry (`RESOURCE.TAB`), PNG/JPEG artwork (`RESOURCE.IFZ`), and fonts (`RESOURCE.FNT`).

## [3.0.0] - 2026-09-10

### Added
- Full support for *Runaway: A Twist of Fate* (Runaway 3): 1080p backgrounds, scene interaction database (`RESOURCE.005`), and bilingual dialogue viewer.

## [2.0.0] - 2026-09-01

### Added
- Full support for *Runaway 2: The Dream of the Turtle*: navigation meshes, 7-byte glyph tables, and dialogue phrase trees.

## [1.0.0] - 2026-08-15

### Added
- Initial release for *Runaway: A Road Adventure* (Runaway 1).
- Nameless archive decoding, RGB565 rasters, row overlays, sprite animations, and Bink video keyfile restoration.
