# Savegames — `SAVEGAME\SAVEGAME.NNN` and `SAVEGAME.IJS`

*Runaway 1* keeps its saves in a `SAVEGAME` directory next to the executable: up to 1,000 slot files
`SAVEGAME.001` … `SAVEGAME.999`, plus one index file `SAVEGAME.IJS`.

There is **no header, no magic and no checksum** on either file. Both are written as a short sequence of
`fwrite` calls straight out of global buffers, so the file is simply those buffers laid end to end.
Recognise a save by the directory, the `SAVEGAME.NNN` name and the fixed 149,080-byte length; nothing
inside identifies it.

Everything is little-endian. Because whole buffers are dumped verbatim, several fields are *not* cleared
between writes and carry stale bytes from a previous, longer value — that is a property of the format,
not a decoding error, and a reader must stop at the first `NUL` (strings) or at the recorded count
(arrays) rather than at the first zero byte.

## 1. The writers, from the executable

The save and index routines are visible in `Runaway.exe` (image base `0x400000`), and they fix the
top-level layout exactly:

```text
fopen("SAVEGAME\\SAVEGAME.%03d")
    fwrite(0x5D3840, 1,  48960)     -- thumbnail buffer
    fwrite(0x5D2FA0, 1,    120)     -- slot name buffer
    fwrite(0x83DD60, 1, 100000)     -- game state blob
fclose                                 48,960 + 120 + 100,000 = 149,080

fopen("SAVEGAME\\SAVEGAME.IJS")
    fwrite(0xA68438, 1,      2)     -- slot count
    fwrite(0x5DFDC0, 1,   1000)     -- occupied flags
    fwrite(0x5D3060, 1,   2000)     -- slot order, u16[1000]
fclose                                      2 + 1,000 + 2,000 = 3,002
```

Two consequences worth stating plainly. The file is **not** padded: the 13,595 zero bytes at the end are
just the unused tail of the 100,000-byte state blob, which the game never fills. And there is no
per-field serialisation at all — the state blob is raw memory, so the file offsets below are the
in-memory offsets of the engine's own globals.

Throughout §3 the **state offset** is the offset inside that 100,000-byte blob;
`file_offset = 0xBFB8 + state_offset`.

## 2. `SAVEGAME.IJS` — the slot index

3,002 bytes, fixed:

```text
0x0000: u16       Count           -- number of occupied slots
0x0002: u8[1000]  Used            -- Used[n] != 0 => SAVEGAME.<n> exists; index 0 unused
0x03EA: u16[1000] Order           -- slot numbers in menu order, Count entries, then zero
```

The observed file has `Count = 10`, `Used[1..10] = 1`, `Order = 1,2,…,10`. Slot **names are not here** —
they live in each slot file (§3.2).

## 3. `SAVEGAME.NNN` — the slot

| File | State | Size | Field |
| --- | --- | --- | --- |
| `0x00000` | — | 48,960 | Thumbnail — 204 x 120 RGB565 (§3.1) |
| `0x0BF40` | — | 120 | `Name` — NUL-terminated slot name (§3.2) |
| `0x0BFB8` | `+0x00000` | 81 | `HotspotLabel` — NUL-terminated (§3.2) |
| `0x0C009` | `+0x00051` | 81 | `ActionLabel` — NUL-terminated (§3.2) |
| `0x0C05A` | `+0x000A2` | 25 | scalars: scene id, viewport, actor anchor (§3.3) |
| `0x0C073` | `+0x000BB` | 3,007 | `Route[97]` — 31-byte step records (§3.4) |
| `0x1B2A3` | `+0x0F2EB` | 62 | `Actor[2]` — two step records: current and target |
| `0x1B2E1` | `+0x0F329` | 14 | route bookkeeping (§3.3) |
| `0x1B2EF` | `+0x0F337` | 14 | misc: 8 x `0xFF`, `0x0F 0x0F`, 4 bytes |
| `0x1B2FD` | `+0x0F345` | 400 | `Viseme A` — in-flight lip-sync stream (§3.5) |
| `0x1B48D` | `+0x0F4D5` | 400 | `Viseme B` |
| `0x1B61D` | `+0x0F665` | 81 | `InvSlots` — item id per inventory position (§3.6) |
| `0x1B66E` | `+0x0F6B6` | 81 | `InvPos` — inventory position per item id |
| `0x1B6BF` | `+0x0F707` | 82 | `InvIcon` — icon index per item id |
| `0x1B711` | `+0x0F759` | 2 | `u16 InvCount` |
| `0x1B720` | `+0x0F768` | 12,789 | undecoded; written only mid-chapter (§3.10) |
| `0x1E915` | `+0x1295D` | 8 | header: `u16 9`, then three `u16` |
| `0x1E91D` | `+0x12965` | 1,395 | `Anim[45]` — step records, same layout (§3.4) |
| `0x1EE90` | `+0x12ED8` | 4,805 | undecoded; **most likely the progress flags** (§3.10) |
| `0x20155` | `+0x1419D` | 31 | one step record |
| `0x20174` | `+0x141BC` | 37 | speech / subtitle placement (§3.7) |
| `0x205AE` | `+0x145F6` | 514 | uninterpreted; bit-noise, largely stale (§3.8) |
| `0x207ED` | `+0x14835` | 128 | audio engine state (§3.8) |
| `0x2099F` | `+0x149E7` | … | small per-scene counters |
| `0x21127` | `+0x1516F` | 24 | `u32[6] Settings` — volume / speed sliders (§3.9) |
| `0x2113D` | `+0x15185` | 13,595 | never written; zero |

Ranges the game leaves zero in every observed save are omitted; the blob is sparse, and most of its
100 KB is never touched.

### 3.1 Thumbnail

204 x 120 pixels, RGB565, row-major, no stride padding — 48,960 bytes starting at offset 0. That is the
1024 x 600 game screen reduced 5x (1024/5 = 204.8, truncated to 204). It is the picture the original's
menu panel shows in its top left. Decoding it with the engine's normal RGB565 pixel format reproduces the
scene exactly, actor included.

It is also the practical way to identify which scene a save belongs to without running the game: compare
the thumbnail against each scene archive's background entry, sampling at
`background[ViewX + 5x][5y]` (§3.3 for `ViewX`) and scoring on the better half of the samples so that
sprites and the actor do not dominate. Over the ten observed saves that picks the right archive with a
5x margin over the runner-up, and it agrees with the scene id in §3.3 in every case.

### 3.2 Names and labels

`Name` is what the player typed in the SAVE dialog. The game seeds the field with a default of the shape
`<date> ~ <time>`; clearing it writes a single `NUL` at position 0 and leaves the rest, so most slots read
as an empty name with a visible ` ~ ` a few bytes in. Treat an empty `Name` as "unnamed" and fall back to
the slot number.

`HotspotLabel` and `ActionLabel` are the two halves of the interface's status line — the object under the
cursor and the full action sentence, in the game's language (Spanish in the observed copy, CP-1252:
`MUÑECO`, `PÓSTER`). Both buffer addresses are referenced individually by the executable, which is how
their 81-byte stride is known.

In every observed save `HotspotLabel` is **empty** — byte 0 is `NUL` — because a save is taken from the
menu, where nothing is under the cursor. The readable `ONTINUAR JUEGO` one byte in is the residue of the
menu's own highlighted entry, `CONTINUAR JUEGO`, whose leading `C` was overwritten by the clearing `NUL`.
`ActionLabel` is live (`IR A`, `EXAMINAR PÓSTER`) and carries the same kind of residue after its
terminator. Only the first string in each buffer is meaningful.

### 3.3 Scene, viewport and route bookkeeping

At state `+0xA2` (file `0x0C05A`) sits a block of individually-addressed scalars:

```text
file 0x0C062  state +0x0AA: u8  ?          -- near-constant per scene, unexplained (see below)
file 0x0C063  state +0x0AB: u8  SceneId    -- selects the scene archive
file 0x0C065  state +0x0AD: u8  0xFF
file 0x0C06B  state +0x0B3: u16 ViewX      -- scene scroll offset of the visible window
file 0x0C06D  state +0x0B5: u16 ViewY
file 0x0C06F  state +0x0B7: u16 X, Y       -- actor destination / anchor
```

**`SceneId` is the scene selector.** Across the ten observed saves it is the *only* byte, word or dword in
the whole 100,000-byte blob whose value partitions the saves exactly by scene — constant across the six
saves taken in `B02` and distinct for each of the other four scenes. Known values, confirmed both by
matching thumbnails against backgrounds (§3.1) and by eye:

| `SceneId` | Archive |
| --- | --- |
| 7 | `RESOURCE.B02` |
| 8 | `RESOURCE.B06` |
| 15 | `RESOURCE.D05` |
| 23 | `RESOURCE.F09` |
| 24 | `RESOURCE.F22` |

The enumeration is **not** archive-name order and not the order of the scene-name literals in the
executable: `F09` and `F22` take adjacent ids while sitting six apart in the literal table, and `B02` is
the tenth literal but id 7. Nor is it derivable from the name's numeric part. Completing the table for all
74 scenes is the open item; the cheap way is more saves in known scenes — one save yields one row.

The byte at `0x0C062` is near-constant per scene (`228, 230, 230, 230, 231, 230` across the six `B02`
saves; `12` for `B06`, `210` for `D05`, `78` for `F22`, `202` for `F09`). It may be the low half of a
`u16` at state `+0xAA` whose high byte is the scene, or an independent per-scene value; either way the
scene selector is the high byte alone, since the six `B02` saves share it while the low byte moves.

Note that the scene id is **not** reachable from the executable by an absolute address: nothing in `.text`
references `0x83DE0B`, and no `mov byte [reg+0xAB], imm8` exists either, so the field is written through a
base pointer that does not start at the blob.

`ViewX` matches the thumbnail: slot 001 has `ViewX = 496` and its actor at scene X 1075, which lands at
(1075-496)/5 ≈ 116 of the thumbnail's 204 columns — where the character actually is in the image.

At state `+0xF329` (file `0x1B2E1`), four `u16`: the current step index into `Route`, the route's step
count, and two further counters. Slot 001 reads 17 / 17 (route finished, and the array holds exactly 17
live records); slot 009 reads 24 / 47 (mid-walk, with 50 further records left over from an earlier walk).

### 3.4 Step records — 31 bytes

The same record appears in four places. It is the complete draw state of one animated sprite for one frame:

```text
+0x00: u8  Direction       -- facing / walk direction
+0x01: u8  Animation       -- animation id within the sprite
+0x02: u8  Frame           -- frame index inside that animation
+0x03: i16 X, Y            -- position, scene coordinates
+0x07: i16 L, T, R, B      -- current bounding rect, scene coordinates
+0x0F: i16 L, T, R, B      -- previous frame's bounding rect (restore/dirty rect)
+0x17: i16 W, H            -- current rect extent  (R-L+1, B-T+1)
+0x1B: i16 W, H            -- sprite's own extent before clipping
```

Coordinates are **scene** coordinates, not screen: scenes are up to 1,740 px wide against the 1024 px
screen, and the values seen (X up to 1,243) confirm it.

`Route[97]` is the walk the actor is executing, one record per frame — X/Y advance a pixel or two per
entry while `Frame` cycles through the walk cycle and settles on the idle frames at the end. The array is
**not cleared between walks**: a short route leaves the tail of a longer previous one behind, so honour
the count in §3.3 rather than scanning for zeros.

`Anim[45]` is a second buffer of the same records, in practice one animation held on a single position
(X/Y constant, `Frame` ramping then sticking).

### 3.5 Viseme buffers

Two 400-byte byte arrays, values `0`–`5`, run in groups of about three with `0` between them. These are
the mouth-shape streams of `RESOURCE.004` (see [global-data.md](global-data.md) §5 — same six phoneme
shapes, same 0–5 encoding) copied in for the line being spoken when the save was taken, one per speaking
actor. Neither is length-prefixed; the live part ends at the first trailing run of zeros.

### 3.6 Inventory

Three 1-based `u8` arrays of 80 item slots and a count, mutually consistent in every observed save:

- `InvSlots[1..n]` — the item ids the player holds, in the order they appear in the inventory screen.
- `InvPos[id]` — the 1-based position of `id` in `InvSlots`, or 0 if the item is not held. Exactly the
  inverse permutation of `InvSlots`.
- `InvIcon[id]` — the icon to draw for `id`; identity (`InvIcon[i] == i`) until an item changes appearance,
  after which entries are remapped (slot 005 has ids 16 and 17 pointing at icons 0x16 and 0x15).
- `InvCount` — `n`, the number of held items.

Observed progression: `[1,2,3,4]` at the start, growing to 15 items, then reset to `[1,2,3,4,5]` for the
chapter that opens in the western town — so ids are reused per chapter, not globally unique.

### 3.7 Speech placement

After the step record at file `0x20155`: a run of `u16` counters, then `13, 337, 492, 505` and a
near-repeat of the same triple. 337/492 are the actor's own extent from the preceding step record, so this
block is where the subtitle for the current line is placed relative to the speaker.

### 3.8 Audio engine state

State `+0x14835` is a raw dump of the audio side: a track number (`11` in the hospital chapter, `20` in
the western town), several byte positions and lengths into the streamed audio, and attenuations in the
DirectSound convention of hundredths of a decibel (`0xFFFFF768` = -2,200 = -22 dB, `0xFFFFF074` = -3,980).
The block contains uninitialised words as well; only the leading fields look meaningful.

The 514 bytes at state `+0x145F6` are not decoded. They are bit-noise (≈1,500 of 4,112 bits set), and
pairs of saves taken seconds apart differ in as few as 2 bits while others differ in ~1,600 — the
signature of a buffer carried over wholesale and only partly rewritten. It is not a palette: rendering it
as 256 RGB565 entries gives random colours.

### 3.9 Settings

`u32[6]` at state `+0x1516F`, identical in nine of the ten observed saves:

```text
1800, 1800, 2000, 3000, 3000, 2200
```

These are the menu's slider positions — the settings panel the original draws in two columns. One save
(008) has the second and third zeroed, which is consistent with a muted channel.

### 3.10 The two undecoded regions

Neither of these is understood, and the second is the one that matters.

**State `+0x12ED8` (file `0x1EE90`), 4,805 bytes.** Nonzero byte counts across the ten saves:

| Save | 001 | 002 | 003 | 004 | 005 | 006 | 007 | 008 | 009 | 010 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| bytes | 0 | 2,312 | 2,310 | 2,310 | 2,272 | 2,272 | 2,276 | 0 | 1,538 | 1,746 |

It is empty at the very start of the game (001) and empty again in 008, and otherwise carries one to two
thousand live bytes that move as the game progresses. That profile — empty at a beginning, filling in as
puzzles are solved — makes this the best candidate for the **game progress flags**, which are otherwise
absent from the file: inventory is fully accounted for (§3.6), but nothing else records what the player
has done, said, opened or triggered. Until this block is read, a save cannot actually be restored.

Identifying it does not need more format archaeology, it needs controlled play: save, perform one
discrete action, save again, and diff. Each pair should isolate a byte or a bit.

**State `+0x0F768` (file `0x1B720`), 12,789 bytes.** Written only in saves 004–007 (4,183–6,094 live
bytes) and untouched in the other six. Internally it is a run of records on a 160-byte stride whose live
length shrinks steadily from ~126 bytes down to ~22, which is a shape, not yet a meaning. Its being
confined to four consecutive saves in one scene suggests something scene-scoped and transient rather than
persistent state.

## Open items

In rough order of what would unblock the most:

- **The progress flags**, §3.10 — almost certainly state `+0x12ED8`; nothing else in the file records
  puzzle state, so no save can be restored without them.
- **The `SceneId` enumeration**, §3.3 — 5 of ~74 rows known.
- **State `+0x0F768`**, §3.10 — 12,789 bytes, four saves, unexplained.
- **The 514 bytes at state `+0x145F6`**, §3.8.
- **The byte at `0x0C062`**, §3.3 — near-constant per scene, purpose unknown.

## Sample

The ten saves analysed are in `F:\Games\Steam\steamapps\common\Runaway A Road Adventure\SAVEGAME`
(Steam release): six in `B02`, one in `B06`, one in `D05`, one in `F22`, one in `F09`.
