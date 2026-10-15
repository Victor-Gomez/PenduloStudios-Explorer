# Archive containers

The game engine uses nameless offset-table archives with no extensions, file paths, or magic headers. Every archive is a flat container indexed by entry number (`e00`, `e01`, …).

## Purpose

All game assets are packed into archives in the `Resource/`, `Dataa/`, or `Datav/` directories. Different categories of data use distinct table conventions, but all entry data is stored uncompressed (except for natively compressed formats like JPEG, PNG, MP3, or Bink in later releases).

## Archive types

### 1. Scene archives (`RESOURCE.<L><nn>`)

Present in all games (*Hollywood Monsters*, *Runaway 1*, *Runaway 2*, *Runaway 3*, *The Next BIG Thing*, *Yesterday*). Each scene in the game is encapsulated into its own archive, where `<L>` is an area letter (A–I) and `<nn>` is a scene index (e.g. `RESOURCE.A01`, `RESOURCE.SP1`).

#### Layout variants

##### Variant A: Hollywood Monsters, Runaway 1, 3, The Next BIG Thing, Yesterday

The table starts immediately at offset 0:

```text
Table:
    u32 Offset[N]       -- absolute byte offsets to each entry data
    u32 Size[N]         -- exact byte sizes of each entry data

Data:
    <entry 0 payload>
    <entry 1 payload>
    ...
```

The table length is determined by `Offset[0]`, because Entry 0 immediately follows the table header:

```text
table_bytes = Offset[0]
entry_count = table_bytes / 8
```

For example, an archive with `Offset[0] == 320` has 40 entries (`320 / 8`). An unused entry slot has `Offset[i] == 0` and `Size[i] == 0`.

##### Variant B: Runaway 2

In *Runaway 2*, the table begins with a 4-byte header specifying the half-table size, followed by the entry 0 offset:

```text
Header:
    u32 TableHalfBytes  -- size of the offset half in bytes (Count * 4)
    u32 Entry0Offset    -- 4 + (TableHalfBytes * 2), identical to the start of entry 0 data

Table:
    u32 Offset[Count]   -- at byte 4
    u32 Size[Count]     -- at byte 4 + TableHalfBytes

Data:
    <entry 0 payload>   -- begins at Entry0Offset
    ...
```

> **Critical layout rule**: In both variants, the table is split into two halves: **offsets first, then sizes**. Reading it as a single flat array of offsets results in every size value being misinterpreted as an offset, creating hundreds of corrupt, misaligned phantom slices.

##### Special case: Hollywood Monsters headerless screen (`RESOURCE.I02`)

One *Hollywood Monsters* scene archive (`RESOURCE.I02`) has no table at all. It consists of a bare 1024×480 indexed raster (491,520 bytes) followed immediately by its 768-byte palette (`IndexedPalette.FullBlockBytes`), totaling exactly 492,288 bytes.

##### Hollywood Monsters playable-scene chunk layout

In *Hollywood Monsters* the first five entries of a playable scene archive are **fixed by index**, not
discovered by classification, and the table always declares 40 slots (`table_len == 320`):

| Entry | Contents | Size |
|---|---|---|
| `e00` | Background framebuffer | 491,520 bytes (1024×480) |
| `e01` | Scene palette block ([palettes.md](palettes.md)) | 528 or 768 bytes |
| `e02` | Region-map fill runs ([masks.md](masks.md)) | variable, 3-byte RLE |
| `e03` | Region lookup tables ([masks.md](masks.md)) | 1,792 bytes (7 × 256) |
| `e04` | Scene metadata tables (hotspots, routes, item points) | variable, usually > 24 KiB |
| `e05`.. | Sprites, overlays, and any further palette blocks | variable |

Verified against the shipped game: 79 of the 102 tabled archives have `e03` exactly 1,792 bytes long. The
23 exceptions are the `?00` chapter-opening archives and the `I*` intro/special archives, which are
presentation scenes rather than playable ones and do not follow the layout.

Entry `e04` is itself a pack of fixed-offset tables, the scene's compiled logic:

| Offset | Table |
|---|---|
| `0x0000` | Actor depth thresholds |
| `0x002a` | Palette delta table |
| `0x003f` | Palette adjustment table |
| `0x007d` | Route boundary points |
| `0x1529` | Route boundary steps |
| `0x35e4` | Scene item default strip |
| `0x35f9` | Scene item interaction points |
| `0x364d` | Scene item approach points |
| `0x36a1` | Scene item facing |
| `0x36b6` | Verb action records |
| `0x3956` | Relation records |
| `0x610a` | Mode-2 relation overlay |

A scene carries 21 items, and the route tables are sized `21 x 21` by candidate or step count — walk
routing is precomputed between every pair of walkable regions rather than solved at runtime. This is why
`e04` is usually larger than 24 KiB: 91 of the 102 archives clear `0x610a`.

The explorer does not rely on this layout -- it classifies every entry structurally, so it also reads the
archives that deviate -- but the layout explains *why* `e01` is always a palette and `e02`/`e03` are always
the region data, and it names two entries that would otherwise be filed as anonymous data.

#### Common scene entries

- **Entry 0 (`e00`)**: Scene background art. Full-screen RGB565 raster (R1–R3, 1024×600 or panoramic 1560×600 in *Runaway 2* `RESOURCE.D12`, `D14`, `D17`), 1024×480 indexed bitmap (*Hollywood Monsters*), or 1080p JPEG (*The Next BIG Thing*, *Yesterday*).
- **Entry 1 (`e01`)**: Primary scene interaction mask (RLE walkboxes, hotspots, depth planes).
- **Subsequent entries**: Additional overlays, sprite animations, secondary masks, or auxiliary scene tables.
- **Fixed data entries**:
  - `335529 bytes` (`e03` in *Runaway 2* and *Runaway 3*): Precomputed waypoint navigation mesh and pathfinding matrix (present across 65 scene archives in Runaway 2 and 53 playable scenes in Runaway 3). Split into two planar sections for a $51 \times 51 = 2,601$ zone matrix: bytes `0..208,079` contain 2,601 slots $\times$ 80 bytes (up to 20 coordinates `(ushort X, ushort Y)` sampled along the shared boundary portal between adjacent zones, or 0 points for non-adjacent zone pairs), followed by bytes `208,080..335,528` containing 2,601 slots $\times$ 49 bytes (ordered zone transition hop sequences terminated by 0). Total: $208,080 + 127,449 = 335,529$ bytes. In the vector overlay, `RouteGraphRenderer` visualizes walk zones as centroid nodes derived from the scene mask, renders portal gates along the boundary seam, and reconstructs multi-hop paths traversing through intermediate portal midpoints ($C_{Z_0} \to M_{0,1} \to C_{Z_1} \dots \to C_{Z_k}$). See `RouteDecoder.cs` and `RouteGraphRenderer.cs`.
  - `43659 bytes`: Precomputed waypoint and walk route mesh in *Runaway 1* (present across 23 scene archives). Split into two planar sections for a $21 \times 21 = 441$ zone matrix: bytes `0..35,279` contain 441 slots $\times$ 80 bytes (up to 20 boundary seam coordinates `(ushort X, ushort Y)`), followed by bytes `35,280..43,658` containing 441 slots $\times$ 19 bytes (ordered zone transition hop sequences terminated by 0). Total: $35,280 + 8,379 = 43,659$ bytes. See `RouteDecoder.cs`.
  - `704 bytes`: Walkway Perspective Corridor Table in *Runaway 1* (present in `RESOURCE.F09` and `RESOURCE.H09` entry 4). Header `u16 SpanCount` (117) followed by 117 records of 6 bytes `{ u16 Width, u16 Y, u16 X }` defining horizontal walk corridor trapezoid bounds for every scanline from $Y=483$ to $Y=599$ (bottom of the 1024×600 canvas). Exactly: $2 + 117 \times 6 = 704$ bytes. See `CorridorSpanDecoder.cs`.
  - `194560 bytes` (`e03` in *The Next BIG Thing* and *Yesterday*): 1080p waypoint navigation matrix table. Stores 320-byte route slots encoding up to 40 32-bit `(int X, int Y)` coordinate waypoints, scaled for 1920×1080 and panoramic scenes, terminated by sentinel coordinate `0x0000FFFF`.
  - `589824 bytes` (`e02` in *The Next BIG Thing* and *Yesterday*): 1080p zone transition hop table pairing with the waypoint table.
  - `48960 bytes` (`e00`–`e06` in *Runaway 1* `RESOURCE.A03`): Chapter selection thumbnail cards. 7 fixed-size $204 \times 120$ RGB565 rasters ($204 \times 120 \times 2 = 48,960$ bytes) displayed on the main menu save/chapter select screen.
  - `3909 bytes` (`e07` in *Hollywood Monsters* `RESOURCE.I04`): World map travel selection mask. Continuous 3-byte RLE mask decompressing to $1024 \times 480$ (491,520 pixels) hit-testing the 7 destinations.
  - `516096 bytes` (`e43` in *Hollywood Monsters* `RESOURCE.000`): Inventory item icons book. 126 $64 \times 64$ 8-bit indexed icon pages ($126 \times 4,096 = 516,096$ bytes), rendered with `RESOURCE.000` entry 49's 80-color palette (slots 176–255; 0 transparent).
  - `785301 bytes` (*Yesterday* `RESOURCE.X03`): Cutscene illustrations archive. Runaway 2-style container ($32$-byte table half, $68$-byte table end) holding 8 $1920 \times 1080$ PNG slides.
  - `526654 bytes` (*The Next BIG Thing* `RESOURCE.004`): Lip-sync viseme tracks. $11,500$ `{u32 Offset, u32 Size}` slots ($92,000$-byte table) encoding mouth phoneme keyframe arrays.

##### Character sprite archives: `RESOURCE.SP1`–`RESOURCE.SP5` (*Runaway 2*)

In *Runaway 2*, the chapter character sprite libraries are named `RESOURCE.SP1` through `RESOURCE.SP5`
("SP" for Sprites, rather than Spanish localized text). They contain 546 animations in total across 1,092
paired entries: an even entry containing a 3,200-byte frame table (`CharacterFrameRecord[100]`) followed by
an odd entry containing the body and coverage stream data. All 546 animations tile byte-exact.

### 2. Audio archives (`RESOURCE.M<nn>`, `RESOURCE.S<nn>`, `RESOURCE.002`)

Hold background music tracks (`M`), sound effects and ambient loops (`S`), and cinematic audio (`002`).

#### Runaway 1 layout (plain offsets)

```text
Table:
    u32 Offset[N]       -- absolute byte offsets; slot 0 is always 0

Data:
    <audio payloads>
```

Audio archives in *Runaway 1* do not have a size half. Because slot 0 is always 0, the total table size in bytes is determined by the **smallest non-zero offset** in the table:

```text
table_bytes = min(Offset[i] for Offset[i] > 0)
slot_count  = table_bytes / 4
```

The size of each entry is computed as the gap to the next highest offset in file order. Multiple slots in `RESOURCE.002` may point to the exact same offset (aliased audio).

#### Runaway 2 layout (explicit size and format)

In *Runaway 2*, audio archives carry explicit sizes and format tags:

```text
Header:
    u32 Count           -- number of audio records
    u32 FirstOffset     -- 4 + (Count * 9), pointing to first audio payload

Records (Count records × 9 bytes):
    u32 Offset          -- absolute byte offset
    u32 Size            -- byte size of audio stream
    u8  Format          -- 0 = WAV / raw PCM, 1 = MP3
```

#### Hollywood Monsters layout (terminated offset table)

In *Hollywood Monsters*, audio tables (`RESOURCE.M*`, `S*`, `001`, `002`, `004`) use an offset table with no size half, terminated by a slot holding the file length. Slot 1 always holds the table's own byte length. A clip's size is the gap to the next slot; runs of repeated offsets denote unused entries and are dropped.

**Runaway 1 uses the same layout, and the same reading holds.** Its thirteen containers (`RESOURCE.M01`-`M06`, `S01`-`S06`, `002`) each have exactly one slot holding the file length, and it is always the last live slot. Slot 0 is empty. Counts are 100 slots for `M*` and `002`, 1000 for `S*`, of which 11 to 79 are used.

Derive the table length by **minimising over the table, not over the file**: guess from the first non-zero offset, re-minimise over the slots that guess covers, and repeat until it stops moving. Minimising over the whole file reads PCM data as offsets and yields counts of 1, 5, 10, 11 or 14.

Repeated offsets in *Runaway 1* are **always consecutive runs** — checked across all thirteen containers with no exception — which supports reading a run as one real entry plus unused padding rather than as one recording shared by several ids. **Which end of a run carries the real id is unresolved**: it depends on whether the exporter records the running offset for every id and advances only on a real one (making the *last* slot of a run the real one) or records only on write (the *first*). `M01` slots 1..11 all hold offset 400, the table end, so the ambiguity covers the first music track itself.

### 3. Voice archives (`DATAACA<0-6>.000` / shards)

Dialogue voice clips are sharded across multiple large archives. In *Runaway 1*, seven shards (`DATAACA0.000` to `DATAACA6.000`) provide up to 12,000 clip slots.

#### Layout

```text
Table:
    u32 Offset[12000]   -- 48,000-byte table of absolute file offsets

Data:
    <voice payloads>
```

**Every shard's table describes every clip.** In *Runaway 1* all seven tables cover the same 5,591 clips (slots 1..5591; slot 0 is empty, and slot 5592 is an end marker holding 241,442,681, which is larger than any shard). A shard gives an offset **local to itself** for the clips it holds, and repeats a shared placeholder for the rest.

**Take the smallest of the seven values for a slot.** It is the real offset, and the shard it came from is the shard holding the clip. A seven-way tie means shard 0, whose local offsets *are* the placeholder values.

A clip's size is the gap to the next **recording** in the same shard, and the last one in a shard runs to that shard's end. The next recording is not always a clip the shard owns. In *Runaway 1*, 869 lines are stored in two shards, all of them in shards 4 and 6 (382 copies in shard 4, 487 in shard 6), and the smaller offset wins as above. The losing copy is still a local value in its shard's table: it is not the slot's placeholder, which is the value most of the seven shards agree on. That copy bounds the clip before it, so every local value in a shard's table is a boundary, not only the owned ones. With those boundaries, the clips and the second copies tile every shard from byte 48,000 to EOF with no gap and no overlap.

> **Correction.** This previously used only the owned clips as boundaries and said the clips tile every shard exactly. They did, but only because each clip absorbed the second copies after it. Clip 2535 ("Busca un buen arqueologo", F09) ran on through 5.4 MB of shard 4's copies of shard 6 lines, about six minutes of audio, and the longest clip came out at 726 s instead of 14 s.

> **Correction.** This previously read: "If an offset in a shard is greater than or equal to that shard's file size, it indicates the clip resides in another shard. The reader searches shards sequentially until a valid offset is found." That is unsafe, and a lone shard's table does not say which of the other six to try. All shards carry the *same* placeholder, so whenever that placeholder is smaller than a large shard's file size the sequential search stops on that shard and reads the middle of an unrelated clip. Shards 4 and 6 are 69 MB and 85 MB, so this is not hypothetical. Taking the minimum avoids it.

**Do not** derive the table length the way the `RESOURCE.M*`/`S*` readers do. The placeholders sort below the 48,000-byte table's own end, so the fixed point collapses. The voice table length is fixed.

**Reject slots that fail two structural tests.** A few thousand slots past the end of the live run, beyond the zeros, sits a block of small leftover values — the same exporter-buffer junk that fills page 4 of the zone attribute table (see [masks.md](masks.md)). In *Runaway 1* it starts at slot 6001 and runs 5,591 values in the range 4,021 to 225,624, and read as offsets they become clips pointing into the middle of shard 0's own table. Two tests reject them, and cost nothing on a live slot:

- the winning offset lies **inside the shard**, at or past the 48,000-byte table and before end of file;
- within a shard, offsets **rise with slot index**, since the exporter wrote clips in slot order.

Stopping at the first slot no shard fills would also work on a full install, but only there: the tests above hold on a partial or synthetic archive too, which is why the reader uses them instead.

### 4. Video keyfiles (`DATAVC00.<nnn>`)

Bink video files in `Datav/DATAV*.<nnn>` have their first 1024 bytes replaced with obfuscated padding. The legitimate 1024-byte Bink headers are stored in an encrypted catalog file (`DATAVC00.<nnn>`).

#### Layout

```text
Header:
    u32 DataOffset      -- 8 + (N * 15); low byte is the XOR seed (0x21)
    u32 VideoCount      -- number of video header records (N)

Catalogue (N records):
    byte[13] Filename   -- null-padded ASCII filename (e.g. "INTRO.BIK")
    u16      Flags      -- 2 bytes metadata

Header blocks (N blocks):
    byte[2048] Block    -- first 1024 bytes contain the true Bink header
```

The catalog and header blocks are XOR-chained:

```text
plain[0] = cipher[0] ^ seed
plain[i] = cipher[i] ^ cipher[i - 1]
```

Restoring a video consists of reading the decrypted 1024-byte header and appending the video file's payload starting at offset 1024:

```text
restored_bik = decrypted_header[0..1024] + datav_file[1024..]
```

#### Hollywood Monsters Smacker cutscenes (`.SMK`)

Unlike *Runaway 1*–*3*, which use XOR-obfuscated Bink headers stored in a keyfile catalog, *Hollywood Monsters* uses standard Smacker video files (`.SMK`, four-character signatures `SMK2` or `SMK4`) stored plainly in cutscene directories (`Video/`, `Movies/`, `Cinematics/`, `Smk/` or the game root). They do not require keyfile restoration. See [audio-video.md](audio-video.md) §4 for details on the 104-byte Smacker header layout, audio track bitmasks, and LibVLC playback.

### 5. Scene navigation table (`RESOURCE.TAB`)

In *The Next BIG Thing* and *Yesterday*, scene geometry, panoramic canvas dimensions (up to 3492×1080), camera viewport coordinates, and waypoint navigation bounds are stored in a dedicated table `RESOURCE.TAB`:

#### Layout

```text
Header:
    u8  SceneCount              -- number of scenes (N)
    u16 SceneId[N]              -- scene identifier (e.g. 101 -> RESOURCE.A01)
    u32 Offset[N]               -- byte offset to each scene definition record

Records:
    u16 Width                   -- canvas width in pixels (e.g. 1920, 2020, 3492)
    u16 Height                  -- canvas height in pixels (1080, 1120)
    <Camera and waypoint coordinate pairs (float X, float Y)>
```

### 6. Yesterday high-definition cutscene animation archives (`RESOURCE.X69`)

In *Yesterday*, `RESOURCE.X69` (8,547,597 bytes) uses the Runaway 2-style offset-size table header ($36$ byte offset half, $76$ byte table boundary) holding 9 cutscene entries:
- **Entries 0 & 1**: Cutscene interaction metadata and control records (188,471 B and 149,086 B).
- **Entries 2 through 8**: Seven large 25-frame cutscene animation payloads (exactly 1,172,852 bytes each, starting with frame count `fc=25` and offset `fo0=352`).

### 7. Runaway 3 chapter interaction database (`RESOURCE.005`)

In *Runaway 3*, `RESOURCE.005` (478,152 bytes) stores the game's chapter trigger and interaction database across 7 chapters (2,173 220-byte records). Each record contains an adventure interaction definition with Spanish object identifiers (`URNA`, `GUANTE`, `RADIAL`, etc.) enciphered via a chapter-local XOR-chain formula. See [global-data.md](global-data.md) §8 for record layout, the XOR decryption algorithm, verb mappings, and the Mermaid interaction graph format.

## Known unknowns

- The meaning of the 2-byte trailer flags in the video keyfile filename records.

## Decoders

- [`SceneArchive`](../../src/PenduloStudiosExplorer.Core/FileSystem/Archives.cs)
- [`AudioArchive`](../../src/PenduloStudiosExplorer.Core/FileSystem/Archives.cs)
- [`VoiceArchive`](../../src/PenduloStudiosExplorer.Core/FileSystem/Archives.cs)
- [`VideoKeyfile`](../../src/PenduloStudiosExplorer.Core/FileSystem/VideoKeyfile.cs)
- [`SmackerVideo`](../../src/PenduloStudiosExplorer.Core/Formats/SmackerVideo.cs)
- [`ChapterInteractionArchive`](../../src/PenduloStudiosExplorer.Core/FileSystem/ChapterInteractionArchive.cs)
- [`SavegameArchive`](../../src/PenduloStudiosExplorer.Core/FileSystem/SavegameArchive.cs)
- [`RouteDecoder`](../../src/PenduloStudiosExplorer.Core/Formats/RouteDecoder.cs)
- [`CorridorSpanDecoder`](../../src/PenduloStudiosExplorer.Core/Formats/CorridorSpanDecoder.cs)
- [`SceneTable`](../../src/PenduloStudiosExplorer.Core/Formats/SceneTable.cs)
- [`HollywoodAttributeTableDecoder`](../../src/PenduloStudiosExplorer.Core/Formats/HollywoodAttributeTableDecoder.cs)
- [`HollywoodSceneMetadata`](../../src/PenduloStudiosExplorer.Core/Formats/HollywoodSceneMetadata.cs)
- [`HollywoodFrankensteinReward`](../../src/PenduloStudiosExplorer.Core/Formats/HollywoodFrankensteinReward.cs)
- [`ArchiveWindowStream`](../../src/PenduloStudiosExplorer.Core/FileSystem/Archives.cs)
- Tests: [`ContainerTests`](../../tests/PenduloStudiosExplorer.Core.Tests/ContainerTests.cs), [`Runaway1Tests`](../../tests/PenduloStudiosExplorer.Core.Tests/Runaway1Tests.cs), [`Runaway2Tests`](../../tests/PenduloStudiosExplorer.Core.Tests/Runaway2Tests.cs), [`Runaway3Tests`](../../tests/PenduloStudiosExplorer.Core.Tests/Runaway3Tests.cs), [`YesterdayTests`](../../tests/PenduloStudiosExplorer.Core.Tests/YesterdayTests.cs), [`SceneTableTests`](../../tests/PenduloStudiosExplorer.Core.Tests/SceneTableTests.cs), [`HollywoodMonstersTests`](../../tests/PenduloStudiosExplorer.Core.Tests/HollywoodMonstersTests.cs)
