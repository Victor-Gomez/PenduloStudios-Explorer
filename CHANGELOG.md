# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [6.7.0] - 2026-10-16

### Added
- **Yesterday Embedded Image Packages Support (`YesterdayImagePackage`)**: Added detection, hierarchical VFS unpacking, and decoding for cutscene and comic panel image package archives in *Yesterday* (e.g. `RESOURCE.A02\e05`, `RESOURCE.A03\e06`).
- **Interactive Image Package Atlas Grid View**: Selecting an unpacked package directory in the explorer tree renders all child sprites in a uniform atlas grid with zoom, pan, clipboard copy, and sprite sheet / sequence export, while expanding the folder in the tree allows selecting and viewing each individual sprite.
- **Palette Swatch Grid Inspection & Rendering (`PaletteDecoder`)**: Interactive 16×16 swatch grid visualization ($512 \times 512$ px) for *Hollywood Monsters* scene palette blocks and *Runaway 1* character palettes. Includes block vs. assembled 256-colour mode toggle with the shared character tail, golden border divider, live hover details (slot index, `#RRGGBB`, 8-bit RGB, 6-bit VGA), click-to-copy hex codes, and PNG export (`{name}_palette.png`).

## [6.6.0] - 2026-10-16

### Added
- **DDS Alpha Occluder Masks Support (*The Next BIG Thing*)**: Added [`DdsDecoder`](src/PenduloStudiosExplorer.Core/Formats/DdsDecoder.cs) to classify and decode 8-bit Alpha DirectDraw Surface textures (`DDPF_ALPHA`, Direct3D `D3DFMT_A8`) used across 28 scene archives (67 entries, e.g. `RESOURCE.A02` `e04`, `e06`, `e07`) for foreground walk-behind occluders.
- **Occluder Mask Compositing & Batch Export**: Integrated DDS textures into the scene mask overlay viewer, occluder toggle compositing, and PNG batch exporter.

## [6.5.1] - 2026-10-15

### Fixed
- **Linux Batch Export Path Resolution**: Fixed destination directory path calculation on Linux where backslash-delimited virtual paths caused files to be exported directly to the output root instead of preserving the virtual directory hierarchy.
- **CI Test Suite**: All unit and headless Avalonia UI tests now pass 100% on both Windows and Linux runners.

### Changed
- **About Window**: Widened dialog from 540px to 620px and unconstrained header layout to prevent text clipping; removed technical formats section.

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
