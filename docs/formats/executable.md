# The executable: where the game logic lives

Every other doc here describes a container. This one describes what is **not** in any container.

Repeated attempts to find a scene script — the data that says which animation plays when, what a verb
does to an item, when a chapter title appears — have come up empty in the archives. This doc records
where that logic actually is, how it was established, and what reading the executable confirms about
the file formats.

Addresses are for *Runaway 1*'s `Runaway.exe` (1,875,968 bytes, image base `0x400000`). RVAs are given
where a virtual address is not more convenient; both appear as `0x4xxxxx` (VA) or `0x0xxxxx` (RVA).

---

## 1. There is no scene script

**The per-scene logic is compiled code, one block per scene.** It is not a bytecode, not a table, and
not a data file. Four independent observations, any one of which would be suggestive and which together
are conclusive:

**The archives have no room for it.** Surveying all 74 scene archives of *Runaway 1* (1,062 live
entries), exactly three entry sizes recur across many archives:

| size | in how many archives | what it is |
| --- | --- | --- |
| 1,228,800 | 41 | a 1024×600 RGB565 background |
| 43,659 | 59 | the walk route table |
| 1,536 | 64 | the zone attribute table |

Nothing else recurs in more than 20. There is no per-scene verb table, item table or event list.

**There is no dispatch table.** All 74 scene archive names appear in the executable as individual
string literals (`"RESOURCE.A00"`, `"RESOURCE.B01"`, …), referenced 129 times from `.text` between
RVA `0x03da34` and `0x17f4a8`. Scanning the whole image for runs of consecutive pointers into `.text`
finds no table of 74 (or 66, or any scene-like count) function pointers — the longest run anywhere is
26, and those are ordinary `switch` jump tables. Each scene is reached by a call site compiled against
its own name.

**There is nowhere to put it.** `.data` has a virtual size of 5,279,788 bytes but only 24,576 bytes of
raw initialised data; the rest is BSS — runtime buffers, several of them 1024×600×2. `.rdata` is
117,214 bytes. The executable carries no large static data. What it carries is 1,712,432 bytes of code.

**The sister game confirms it.** *Hollywood Monsters* runs on the same engine family and its scene
archives use the same table convention. The ScummVM `hollywood` engine, which is a working port,
implements its scenes as **90 hand-written C++ files, 54,279 lines**, one per scene, full of
transcribed constants — entry coordinates, facings, ambient cue indices, state ids. That is what
porting compiled per-scene logic looks like.

### What *Hollywood Monsters* keeps in data, and Runaway does not

HM is not purely code-driven: it has a per-scene metadata blob holding, at fixed offsets, the route
tables **and** the item and verb data:

| offset | contents |
| --- | --- |
| `0x0000` | actor depth thresholds |
| `0x002a` | palette delta table |
| `0x003f` | palette adjust table |
| `0x007d` | route boundary points, 441 × 12 |
| `0x1529` | route boundary steps, 441 × 19 |
| `0x35e4` | scene item default caption strip, 21 bytes |
| `0x35f9` | item interaction points, 21 × 4 |
| `0x364d` | item approach points, 21 × 4 |
| `0x36a1` | item facing, 21 bytes |
| `0x36b6` | verb action records, 21 items × 8 verbs × 4 bytes |
| `0x3956` | inventory relation records, 121 × 21 × 4 |
| `0x610a` | a second relation overlay, same shape |

A verb action record is `{u16 actionHandlerId, u16 movementMode}` — so even in HM the *data* only names
a handler, and the handler itself is code.

**Runaway 1 keeps the routing half and drops the rest.** Its route entry is 43,659 bytes =
441 × (80 + 19): the same 441-pair matrix and the same 19-byte step list as HM, with a larger point
array (80 bytes, 20 points, against HM's 12). It begins at offset 0 and ends exactly on the last byte,
so there is no region corresponding to HM's `0x35e4` and beyond. The same reduction shows in the zone
table: HM has 7 pages of 256, Runaway has 6, having dropped the palette-remap page a 16-bit game
cannot use.

So *Runaway 1* moved into code what *Hollywood Monsters* had kept in data.

### The actor scale factor is not in the data either

The same search, run for one specific constant, comes back the same way. *Runaway 1*'s characters are
authored at a single size — every frame in `Resource.001` is 418 to 492 px tall, and the second sprite
library in `RESOURCE.000` is that one size too ([global-data.md](global-data.md)) — so the runtime has to
resample them per scene. Where the factor is **not**:

- the 32-byte frame record has no spare field;
- no scene archive carries a recurring per-scene table for it. Listing every entry under 4 KB over all 74
  archives, the only size present in every scene is the 1,536-byte zone attribute table; the rest are
  one-off small sprites;
- **zone table page 3 is not a depth ramp.** Over 38 scenes, restricted to the ids a mask actually paints,
  its value agrees with the zone's mean screen row on 42% of pairs — below chance. It is the lighting
  field ([masks.md](masks.md)). Page 0 scores 35% and page 2 41% on the same measure;
- the 43,659-byte route table's arithmetic is exact (441 × 99) and leaves no spare bytes;
- the executable has no relevant strings.

So it is in the code, along with the animation frame rate, the per-entry palette choice and the verb logic.

---

## 2. A scene's code block

Scene-local state pins the block down. The intro's current-animation and current-frame globals
(`0x5d2ef8`, `0x5d2ef1`) are referenced from 41 and 31 sites respectively, and **every one of them lies
between RVA `0x03cdbb` and `0x03f60b`** — a contiguous block of roughly 10 KB. That block is the intro
scene, and the 129 scene-name references spread over `0x03da34`–`0x17f4a8` mark the others.

### The intro scene, `0x43da20`

Reading it straight through gives the whole shape of a scene:

```
0x43da20  sub esp, 8 …                       ; takes a chapter number as its argument
0x43da33  push "RESOURCE.A00"                ; opens its archive by name
0x43da38  mov word [0x7fc576], 0x400         ; scene width  = 1024
0x43da41  mov word [0x7fc56e], 0x258         ; scene height = 600
0x43da7a  call 0x40bf70                      ; open archive  -> handle
0x43da8e  call 0x58dc60 (0x856420, 1, 0xa0)  ; read the 40-entry offset table
0x43daa0  call 0x58dc60 (0x94f420, 1, 0xa0)  ; read the 40-entry size table
0x43dab4  … loop, ebp = 6 …                  ; load the six animations, entries 0-5
0x43dafd  call 0x58dc60                      ; load the background, entry 6
0x43db10  mov edx, [esi + 0x85643c]          ; 0x856420 + 0x1c = entry 7
0x43db18  call 0x58d920                      ; seek to entry 7 + chapter
0x43db36  call 0x58dc60                      ; load it -> the chapter title overlay
0x43dc45  push "RESOURCE.003"                ; then the text and cue tables
```

Two things are worth extracting from this.

**The chapter title card is entry `7 + n`.** The scene takes a chapter number, masks it to a byte,
scales by 4 and indexes the archive's offset table at `0x85643c`, which is the table base `0x856420`
plus `0x1c` — entry 7. So `RESOURCE.A00` entries 7 to 12 are the six chapter titles, selected by the
scene's own argument. The archive is loaded once and reused between chapters.

### The opening, `0x43dff0` (exe scene 2010)

The game's first scene calls the loader as `0x43da20(0)` (no title card) and runs Brian's story.
Traced on the original with `engines/pendulo/tools/trace_original.py` (ScummVM tree); times in ms
from the start of `0x43dff0`:

| ms | what |
|---|---|
| 0 | segment 0's first frame, `fadeIn(50)` (0.5 s) |
| 572 / 1854 | segment 0 (positions 0..20), then cue 0 (stage 100 row 0, texts 500-502) |
| 7287 / 8274 | segment 7 (190..206), cue 1 |
| 18554 / 19615 | segment 8 (210..227), cue 2 (6 lines) |
| 51895 / 53875 | segment 5 (150..182), cue 3 |
| 67153 | `DATAVA02.001` frames 0..700 at 24 fps, at scene (256, 148), 400x234 |
| 96342 | video closed, `0x43df60` clears its rect (black in the picture) |
| 96374 / 98395 | segment 6 (182 down to 149), cue 9 (5 lines) |
| 135586 | `fadeOut(50)`, `0x404e90` |
| 136210 | return, `0x444b00(1)` (M01, S01, DATAACA1), then B01 |

- **Segments.** `0x43d450(n)` sets the sequence position `b[0x5d2eea]` to the segment's start;
  `0x43d560(n)` steps it once per 60 ms flag `0xa6708e`, takes the frame from the 228-byte table at
  `0x5a4730` into `b[0x5d2ef1]`, and at the end stores the talk entry in `b[0x5d2ef8]`. Starts and
  ends: 0: 0-20 -> 1, 1: 30-59 -> 2, 2: 59-84 -> 1, 3: 90-124 -> 3, 4: 129-146 -> 1, 5: 150-182 -> 4,
  6: 182 down to 149 -> 1, 7: 190-206 -> 5, 8: 210-227 -> 1. `0x43cd80` draws the body (entry 0).
- **Lip-sync.** While a talk entry is set, `0x43d110` draws it over the held body at frame
  `0x5a46fc[viseme]` (the table is 0..5 twice), the viseme from `0x416de0`; 0 between lines.
- **Lines.** A cue's lines follow 150 ms after each line ends (`0x4140f0(2, 0, 0, ...)`), and the
  next segment starts ~25 ms after the last. A line ends on the talk step's clock, not the audio's
  (read 2026-10-14): the sound thread `0x40abe0` wakes on each DirectSound notification and
  `0x40a440` moves every channel's position (`+0x679e68`) on by one chunk (`0x74d380[ch]`: 3,000
  bytes for the 16 kHz 8-bit voice, `0x4084bf`; 6,000 and 12,000 for the 16-bit formats; 187.5 ms
  each) and clears the channel's flag (`+0x679e7c`, voice `0x679f74`) once it passes the length
  (`+0x679e64`). The talk step `0x43d110` runs when the 50 ms pace flag `0x890680` is 2; finding the
  flag clear it calls `0x412b20(1)`, and if `0x5fdb4b` (more lines) is set it counts `0x5c12e0` up
  over three more ticks and then calls `0x4140f0(2, 0, 0, 11)`. Measured on cues 0-3 (13 lines):
  the line ends `ceil50(ceil(bytes / 3000) * 187.5) + 50` ms after its text, 12 of 13
  within 4 ms (the 13th, clip 477, 27 ms early).
- **The video.** At `0x43a4b0` the exe loads `RESOURCE.002`'s table into the music bank
  (`0x6d9060`), then `playSound(0, 0, 0, 4, 100, 0)`: entry 4, 16 kHz stereo, 29.98 s, once. Cues
  4..8 (texts 514-518) are said as the frame (`0x8bc170`) passes 72, 214, 356, 498 and 641.
- **Skip.** Escape sets `0x9d2735`, which the loops test to end the intro.

**Frame counts are hardcoded.** Six calls to the sprite converter each take a byte from a table at
`0x5a46cc`:

```
b2 23 07 23 23 23   =   178, 35, 7, 35, 35, 35
```

Those are exactly the frame counts of `RESOURCE.A00` entries 0–5 as decoded from the archive. The
executable stores them as a literal table rather than reading them from the file.

### What follows the opening: B01, `0x44dce0`

Scene 2010's dispatcher case calls `0x43dff0`, then `0x444b00(1)` and `0x40f110` (chapter 1), then
`0x44dce0`, before its `jmp`; `interactions.py` dumps such followers as `then_<va>` (the JSON's
`thenFunctions`). `0x44dce0` loads `RESOURCE.B01` (`0x44d0e0`) and ends by storing scene `0x7d0`
(2000, the chapter title). Traced (ms from B01's start, the original against the engine's
`SceneB01`):

| s | what |
|---|---|
| 0 | `0x401ce0`, `playSound(1, 0, 2, 32, 80, 1)` (M01 #32, channel 2, looped), `fadeIn(50)` |
| 0.5 | caption (entry 7) blended in by `0x44d3a0`, 5% a 60 ms tick, counter to 300, out again |
| 5.4 | pan 568 -> 328, a pixel a step of the 10 ms flag `0x6d93cc` |
| 10.05 | credits A (`0x44d880`): view 0, `0x404e90`, the composite cleared, so entries 5 and 8 are centred on black |
| 19.65 | `0x43f680`: `DATAVB04.001` frames 0..250 (the suitcase), S01 #27 on channel 5 |
| 30.57 | wait for channel 5 (`0x679fb2`, at most 8 counter steps), S01 #28 on channel 6, credits B (`0x44da30`, entries 9 and 6) |
| 36.98 | entry 6 full: `playSound(0, 0, 0, 17, 100, 0)` (M01 #17, the music, once), `0x408fe0(2, 40)` |
| 38.47 | 150 steps on: S01 #29 on channel 5, both run back |
| 40.1 | wait for channel 6 (`0x679ff0`, at most 16), `DATAVB04.001` frames 261..312, black, `0x408fe0(5, 70)`, stop 5 and 6 |
| 42.29 | the picture back, view 328, car (entry 1) from frame 48 a 60 ms tick; pan to 0, 2 px a step |
| 45.58 | `fadeOut(50)`, `stopSound(2)`, black |
| 46.29 | `0x4407c0`: `DATAVA01.001` 0..87, then `DATAVB08.001` (the drive, with lines, stage 201 row 1) |
| | `0x679e7c = 3` (channel 0 fades), wait for it (at most 12), scene 2000 |

- **Credits.** Both entries are drawn every pass by `0x44d660` (75 frames, `0x4333c0`): the first's
  frame `0x5d2ba0` goes 0..74 on the 40 ms flag `0xa65ac4`, `0x44d810(50, ...)` waits 50 steps of
  the 10 ms flag `0x807b68`, the second's `0x5d2bb1` goes 0..74, `0x44d810(150, ...)`, and both go
  back on `0x6d93cc` until the first is 0.
- **Steps that redraw run at the frame rate.** `0x6d93cc` is set every 10 ms, but the pans and the
  credits' way back redraw on every pass, and the trace has a step every ~20 ms (caption and the
  first pan 10.05 s; the last pan's 164 steps in 3.3 s). The credits' waits redraw nothing and keep
  to 10 ms.
- **Escape** sets `0x9d2735`; `0x44d7d0` (and `0x4399f0` in the video loops) clears it, blacks the
  screen and opens the options menu `0x42daf0`, and returns 1 only if the scene word became 255 or
  `0x856708` is set (a game was loaded or the player quit). Otherwise the sequence goes on.
- **`0x43aa10`** plays `DATAVB08.001` up to frame 2250 like `0x43a650`, but draws the drive's
  credits over each frame. Each frame it clears the back buffer `0x74d3f8` (`0x404ff0`), blits
  the video into it from (0, 0, R, 600) to (0, 84, R, 684), calls `0x440410` and `0x4406e0`, and
  flips. Everything steps once a video frame. "Frame" below is the counter `0x8bc170`, which is
  set after the flip to the frame just shown, so it holds the one before the frame being drawn.
  The pendulo engine plays it as a `VideoStep::kPlayOverlay` step, with `SceneB01` as the
  `VideoOverlay`.
  - **The crop.** R starts at 1024. Past frame 343, R = `(0x80 - v) << 3` with `v = b[0x5d2f04]`
    before it goes up (to at most 63), so the video narrows to 520 px and the right of the screen is
    black. Past frame 2185 the same is done before v goes down. Once v is 0, R keeps its last value
    (1016) to the end.
  - **Credits** (`0x440410`, from frame 420, done flag `0x5c69b0`). The pass `w[0x5d2eb8]` goes up
    first. Passes 1..11 draw frame w, 12..100 frame 11, 101..112 frame `112 - w`, and 113..146
    nothing. At 147 the next sprite (`w[0x5d2ec0]`, entries 26..37 via `0x68f0e0[]`) starts at pass 0,
    or after the twelfth the flag is set. `0x43ffe0` reads the frame's 14-byte header
    `{u32 offset, u16 x, width, top, bottom, runs}` (12 of them, 168 bytes) and clears
    `(x, top) .. (x + width, bottom)` to black. Then it copies `runs` runs `{u16 x, y, u8 n, n RGB565}`
    from `168 + offset`, and BltFasts the box to the primary. The coordinates are 1024 x 600 ones
    (+84 on screen). The sprites sit at x 539..1005, y 319..579.
  - **The window** (`0x4406e0`, from frame 420, done flag `0x5c69b4`). `0x43fe90` reads entries
    10..25 to `base + 0xe8fb0 + i * 0x102de0` (`base = d[0x679e4c]`), each in the last of ten
    106,032-byte slots. `0x43fdb0` clears pictures 16 and 17 to black and calls `0x43fb70(a, b)` for
    (255, 0), (0, 1) .. (16, 17). That fills slots 0..8 of block b with `a * (100 - L)% + b * L%` for
    L = 10..90: each RGB565 field is scaled and truncated on its own (`* 0x51eb851f >> 5`), and
    a = 255 is black. So step k of block p fades picture p - 1 (black for p = 0) into picture p
    (black for p = 16), and step 9 is the picture. On even frames (`0x590320`, the 64-bit remainder
    by 2) `0x4404a0` draws step `min(s, 9)` of block `w[0x5d2ec2]`, then `s = w[0x5d2ee0]` goes up.
    At 10 with block 16 the flag is set; at 54 the next block starts at step 0. Odd frames redraw
    step `min(s - 1, 9)`, or block - 1's step 9 when s = 0. `0x4404a0` copies the 282 x 188 step
    opaque to (627, 140) (1024 x 600: (627, 56)) and BltFasts it to the primary. So a picture fades
    in over 20 frames and shows for 108 in all; the last fades out by about frame 2168.
  - Nothing resets these words: they are statics, zero when the exe starts.
  - `0x43bb30` (also `0x7a1cc0`, the surface restore hook) loads `DATAVB08.000` into `0x92d280`.
    The overlay does not use it: it holds the mouths drawn over the held stills (read 2026-10-15,
    below).

**The held stills' mouths** (read 2026-10-15). A video function with talking stills has a loader
that opens `DATAV<room>.000`, a Bink of whole 1024 x 600 frames, and calls
`0x43ae70(handle, n, rects, firsts, lasts, table)`. `0x43ae70` allocates `n` 6-byte entries
`{records, u16 count}` behind `table = {entries, u16 n}`. For each, `0x43af10(handle, l, t, r, b,
first, last, entry)` does `BinkGoto(first)` (1-based) and copies the inclusive rect of each frame up
to `last` into a surface of its own. Each record is 20 bytes `{surface, l, t, r, b}`.
`0x43b250(table, anim, frame)` BltFasts record `frame` of entry `anim` to the front buffer
(`[0x7a1ca4]`) at `(l, t + 84)`, `(l, t)` in game space. Nothing redraws the still under it: the
mouth stays until the next one or the next video frame.

`DATAVB08.000` has 36 frames, three mouths of 12: `(587, 74, 647, 161)` frames 1..12 is the man in
the yellow coat at the club door (still 2566), `(258, 318, 313, 373)` 13..24 is Brian kneeling in
the street (2626), and `(402, 28, 531, 229)` 25..36 is the man's close-up (2655). `0x4407c0` sets
`b[0xa6708d]` to 0, 1, 2 before each hold. A mouth's frames are the twelve visemes of
`0x416de0`; 6..11 are the blink bank.

The hold's waiter (the loop after the still's line) picks what is drawn:

- `0x52cd70` loops `0x439bb0`, the video talk step (pace flag `0x890680`, 50 ms). It draws nothing
  for the first ticks of a line (`b[0x5c12e0] <= 2`). While the track has visemes
  (`0xa0bb16 != 0xa81b74`) it calls `0x416de0` and draws frame `v` of `b[0xa6708d]` (bank 6, the
  blink frames, one time in 5). When the track or voice channel `0x679f74` is done, it draws frame 0.
- `0x518740` and `0x45a0b0` blink once: on each `0x9dade0` tick while the line plays, one chance in 15
  (`rand() % 0xf`) draws frame 6 once (`esi = 1`), and every tick draws frame 0. B01's line 5 at
  2655 ("Puede ser...") uses it.
- `0x448b60` blinks again and again: frame 0 after a 6, then maybe another 6.
- `0x458920`, `0x458950`, `0x52ac20` and `0x47b020` add a room drawing call to the lip-sync, and
  `0x52cda0` draws random frames. None of these is lifted yet.
- `0x4399a0` is not a waiter: it is the skip handler of timed stills.

Every room uses the same table (`xref_const.py 0x92d280`: 88 references, from B01 to I03). Each
video function's loader fills it when that function starts. The 21
callers of `0x43ae70` are: B01 (`0x43bb30`), B09 (14 mouths), B01/B02/B07, D01/D05/D07/D08/D10, E06,
F01/F02, G03/G04, H04/H07/H08/H09 and I02/I03. Some mouths have 6 frames and so no blink bank.
`codegen.py` reads each loader's arguments (`mouth_loader()`) and each hold's waiter
(`hold_mouth()`), and emits `VideoStep::kMouth*` plus a `VideoMouth` table for each script that
has one. Holds whose waiter it does not know are listed in `REPORT.md` under "video mouths".

  Then `0x439820(2)` waits 2 steps of the 250 ms counter and `0x43a650` goes on to 2566.
- **Cue frames.** `0x435f00(frame, 0, n)` is true when `n` equals its counter `0x6babb6`, cue `n` has
  not fired (`0x890340[n]`), and the video's frame (`0x8bc174:0x8bc170`) is at or past `frame`; it
  then marks it and steps the counter. So a callback's cues fire once each, in order, even when a
  play starts past them.

---

## 3. The intro's timeline, and why it is not the general mechanism

The intro's per-frame update at `0x43dd40` is a chain of five identical blocks:

```
if (counter > threshold && step == n) { fireCue(1, 0, n + 4, 0x0b); step++; }
```

against a 64-bit counter at `0x8bc170`/`0x8bc174`, with thresholds:

| threshold | 72 | 214 | 356 | 498 | 641 |
| --- | --- | --- | --- | --- | --- |
| gap | | 142 | 142 | 142 | 143 |

The cue arguments identify the lines exactly. `RESOURCE.003` stage 100 (`= RESOURCE.A00`) row 0
frames 4 to 8 are five short exclamations:

| frame | text id | voice id | line | clip |
| --- | --- | --- | --- | --- |
| 4 | 514 | 481 | ¡los líos en los que me iba a ver metido! | 2.00 s |
| 5 | 515 | 482 | ¡los extraños personajes que se iban a cruzar en mi camino! | 2.89 s |
| 6 | 516 | 483 | ¡los cientos de peligros que me acecharían a cada paso! | 3.04 s |
| 7 | 517 | 484 | ¡Sí, toda una aventura! | 1.92 s |
| 8 | 518 | 485 | Pero empecemos por el principio. | 1.71 s |

Five thresholds, five consecutive frames, five short lines of one beat each. The match is not in doubt.

**The counter's unit is not established.** The five clips average 2.31 s against a uniform 142-unit
gap, which suits a ~60 Hz frame counter (2.37 s) far better than the 20 Hz animation tick (7.10 s) —
but the longest of the five is 3.04 s, which a 2.37 s gap would cut off, and the update only tests the
thresholds while no line is playing, so the counter may not advance during speech. Treat the gap as a
floor of unknown unit, not a duration.

**This device appears once in the whole executable.** Scanning `.text` for compares against that
counter finds 22 sites forming exactly one chain of more than two. So the evenly-paced threshold
timeline is a montage device written for this one sequence, **not** how scenes are driven in general.
The general mechanism is ordinary control flow.

### What is still missing

The chapter title's wipe-in is **not** found. Nothing located so far draws that overlay with a moving
horizontal reveal, so its duration and edge softness remain unmeasured.

---

## 4. What the executable confirms about the formats

Reading the loaders is a free check on the decoders, and every check passed.

**Sprites** (`0x436000`). The loop steps `edi` by `0xe` per frame record, reads a `u32` at `+0`, a
`u16` at `+0xc` as the segment count, and per segment reads 4 bytes of coordinates plus a `u8` count at
`+4` followed by `count` 16-bit pixels. That is the 14-byte frame record and the 5-byte segment header
exactly as [sprites.md](sprites.md) has them.

**Overlays** (`0x43cd00`). Reads a `u16` run count, then per run a 6-byte header (`u16 x`, `u16 y`,
`u16 length`) followed by `length` 16-bit pixels — [overlays.md](overlays.md) as written.

**Both loaders convert RGB565 to RGB555 in place**, with

```
pixel = ((p >> 1) & 0x7fe0) | (p & 0x1f)
```

This is worth stating plainly because it is easy to misread as evidence that the files are 555: it is
the opposite. The **files are RGB565**, and the original game degrades them to 555 on load to match its
DirectDraw surface. A decoder — or a re-implementation — that keeps 565 is reproducing the artwork more
faithfully than the original did.

**`RESOURCE.003`** (`0x43dc45` onward). The loader reads a 100,000-byte block (`push 0x186a0`), then a
`u8` and a `u16` count, then computes row strides with `eax*41` and `eax*321` — a 20,000-record cue
table of 5 bytes, a small-row stride of `0x29` and a large-row stride of `0x141`, matching
[global-data.md](global-data.md).

---

## 5. Reproducing this, and continuing it

This was done with a few throwaway Python scripts over `pefile` and `capstone` — no disassembler
install needed — which are not part of this repository. The method matters more than the scripts,
and it is short:

**To read a scene you have not read yet**, find a global that scene alone uses — a current-frame
or current-animation byte near its code — then scan `.text` for every 4-byte little-endian
occurrence of that address and look at where the hits fall. They cluster, and the cluster is the
block: `0x5d2ef8` gives 41 references, all within RVA `0x03cdbb`..`0x03f60b`. Disassemble a couple
of hundred bytes from the block's start, annotating any operand that points into `.rdata` with the
NUL-terminated string it finds there. Scene archive names then label themselves, so a scene's
opening identifies itself within a few instructions.

**Known anchors for Runaway 1:**

| address | what |
| --- | --- |
| `0x43da20` | the intro scene (`RESOURCE.A00`), takes a chapter number |
| `0x43dd40` | its per-frame update, the cue timeline |
| `0x43cd80` | its animation driver — animation index `0x5d2ef8`, frame `0x5d2ef1` |
| `0x450ed0` | `RESOURCE.B02` scene loader (first playable room: hospital) |
| `0x450580` | B02 actor depth scaling function |
| `0x434c20` | `placeActor(x, y, facing, flag, region)` |
| `0x413990` | `fireCue(stage, row, frame)` |
| `0x44e880` | actor walk dispatcher |
| `0x436000` | sprite loader / RGB565→555 converter |
| `0x43cd00` | overlay loader / converter |
| `0x40bf70` | open archive by name → handle |
| `0x58dc60` | read entry into a buffer |
| `0x58d920` | seek to entry |
| `0x5a46cc` | A00's hardcoded frame counts |

`0x32ea0`, `0x40c120`, `0x40dcf0`, `0x40e060` and `0x18dc60` are RVAs from the earlier Frida work
in the game folder's `_re/README.md`, and still check out.

---

## 6. A playable scene's code block: `RESOURCE.B02`

`RESOURCE.B02` (the hospital room in Chapter 1) is the first interactive scene of the game. Its loader
at `0x450ed0` and entry sequence at `0x44eb00` provide the prototype for all 71 playable scenes.

### Scene dimensions and camera
- **Dimensions**: width 1520 (`0x5f0`), height 600 (`0x258`), set at `0x45147f` and `0x451488`.
- **Viewport scroll limit**: `[0x8093f2] = 496` (`0x1f0` = `1520 - 1024`), set at `0x45149a`.
- **Archive layout**:
  - Entry 0: 1520×600 RGB565 background (1,824,000 bytes)
  - Entry 1: 3-byte continuous RLE mask (15,015 bytes = 5,005 runs)
  - Entry 2: 6-page zone attribute table (1,536 bytes)
  - Entry 3: walk route table (43,659 bytes)
  - Entries 4–23: animations, occluders, and overlays

### Player entry pose and placement
- Entry placement is executed at `0x44eb17` via `placeActor(x=1075, y=534, facing=7, 0, 0xff)`:
  - `x = 1075` (`0x433`), `y = 534` (`0x216`), facing 7.
- Character palette: `RESOURCE.000` slot 45 (Costume 2: Brian in red jacket and jeans).

### Actor depth scaling formula
Every playable scene installs a depth-scaling function pointer into `[0xac5a04]`. For B02, `0x4506b4`
installs `0x450580`, which implements a **piecewise linear ramp split at `x = 864` (`0x360`)**:

```
if (x < 864) {
    height = y * 1.2638888 - 302.06946;
} else {
    height = y * 0.44444444 + 188.77777;
}
scaleFactor = height / 455.0;
```

- **Left branch (`x < 864`)**: Hospital room floor.
  - Slope: `1.2638888` (stored at `0x4514cd` as float `0x3fa1c71c`).
  - Intercept: `-302.06946` (stored at `0x4514d7` as float `0xc39708e4`).
  - Corresponds to `horizonY = 239`, `fullY = 599`. At `y = 599`, `height = 455` px (100%). At minimum floor `y = 455`, `height = 273` px (60%).
- **Right branch (`x >= 864`)**: Corridor / doorway transition.
  - Slope: `0.44444444` (`4/9`, stored at `0x451501` as float `0x3ee38e39`).
  - Intercept: `188.77777` (`1700/9`, stored at `0x45150b` as float `0x433cc71c`).
  - At `y = 599`, `height = 455` px (100%). At doorway threshold `y = 411`, `height = 371.5` px (81.6%).
- Global normalization factor is `0.0021978023` = `1 / 455.0` (`fmul` at `0x5a46a8`), where 455 is Brian's authored base height.

### Opening sequence and speech cues
At `0x44eb5c`–`0x44ec1f`, the scene triggers Brian's internal monologue via `fireCue(stage, row, frame)`
at `0x413990`. In `RESOURCE.003` Stage 202 (Row 0):
1. Frame 0 (text 500, voice 598): "Se ha dormido, le han debido hacer efecto los tranquilizantes..."
2. Frame 1 (text 501–505, voice 599): "¡Qué historia! No sé qué pensar..."
3. Frame 2 (text 506–507, voice 604): "...¿cómo actuar en una situación como ésta?"
4. Frame 3 (text 508, voice 606): "Sí, creo que debo hacer algo para proteger la vida de Gina..."

## 7. Consequences

- **A faithful re-implementation cannot be data-driven for scene logic.** There is no script to
  interpret. Each scene has to be written, as the ScummVM `hollywood` engine writes them.
- **The archives are fully accounted for.** Every entry in a *Runaway 1* scene archive is art, a mask,
  a zone table or a route table. Searching them further for behaviour is searching for something that
  is not there — a result, not a gap.
- **The executable is the source for constants**, not just for logic: frame counts, scene dimensions,
  entry indices and cue numbers are all literals in code, and can be read out one scene at a time.

## 7. Parallax scrolling, and the title wipe

Two things that no amount of staring at the archives could settle, because neither is in them.

### Parallax exists, and it is per scene

The view origin is a pair of u16 globals: `0x68f094` (x) and `0x807b94` (y), clamped against
`0x8093f2`, which is written as `sceneWidth - 0x400`. A second pair, `0xa838de`/`0xa83458`,
holds the **last drawn** view; ordinary code only ever compares against it, as a redraw test.

**110 call sites over four callees draw a layer at `base - viewX / n`** (n = 1, 2, 3, 4, 5, 6 and one
x4 form; not the {3, 5, 18} of an earlier version of this page, whose "/18" sites `0x468677` and
`0x469df7` are D06's camera ease). The callees: `0x5642b0(surface, xoff)` draws an overlay strip and
`0x556220(surface, xoff)` erases it, called with the last drawn view; `0x44cc30(surface, x, y)` and
`0x44cd90(surface, x, y)` draw and erase against the pristine background `0x900f08`, in x and y.

The offset is added to each run's own x in **scene** coordinates, into the scene-sized composite
`0x92af28`; the blit `0x4324b0` then copies the window `[V, V+0x3ff]`. So screen x = scene x - V, and a
layer at `base - V/n` moves on screen at `100 * (1 + 1/n)` percent of V. Layer surfaces are `0x68f0b0 + 4k`
= archive entry 4+k in scenes with the chapter-title preamble, and `0x68f0a0 + 4k` = entry k in B01 and I01.

| Scene | maxV | Entry | base | end (V = maxV) | n | Screen % | Sites (first) |
|---|---|---|---|---|---|---|---|
| D10 | 512 | 6 | 580 | 68 | 1 | 200 | `0x4724ed` |
| E06 | 356 | 4 | 1200 | 1022 | 2 | 150 | `0x48c521` |
| E08 | 408 | 6 | 408 | 204 | 2 | 150 | `0x495a37` |
| F24, H24 | 704 | 8 / 7 / 6 | 440 / 800 / 820 | 206 / 624 / 586 | 3 / 4 / 3 | 133 / 125 / 133 | `0x4dfc76`, `0x5564b4` |
| F30, H30 | 596 | 4 / 5 | 1220 / 230 | 1022 / 111 | 3 / 5 | 133 / 120 | `0x506a49`, `0x564554` |
| B01 | 568 (by rule) | 3 / 4 / 2 | 100 / 1100 / 2480 | base-V / base-V / base-4V | 1 / 1 / 1/4 | 200 / 200 / 500 | `0x44d004` |
| I01 | 1024 | 2 | 1532 | 1532-V/4 | 4 | 125 | `0x445dd6` |
| G03 | 496 | 4 / 5 / 6 | -42 / -42 / 248 | -42-V/6 / -42-V/4 / 248-V/2 | 6 / 4 / 2 | 117 / 125 / 150 | `0x524304` |

I01 and G03 are 900 high and scroll y with the same n (G03: y = 50 / 75 / 150 minus Vy/n; I01: y = 300 - Vy).
B01's loader (`0x44d0e0`) never stores a scroll max. Every base is a literal in the scene's own code.

Every draw site, per scene (the erase sites are the matching `0x556220` / `0x44cd90` calls):

| Scene | Draw sites |
|---|---|
| D10 | `0x4724ed`, `0x472923`, `0x472e28`, `0x473320`, `0x47383d` |
| E06 | `0x48c521`, `0x48cac3`, `0x48d034`, `0x48d593` |
| E08 | `0x495a37`, `0x49604a`, `0x49661e`, `0x496b7e`, `0x4988fa` |
| F24 | `0x4dfc76` (e08), `0x4dfc9a` (e07), `0x4dfcc3` (e06) |
| H24 | `0x5564b4` (e08), `0x5564d8` (e07), `0x556501` (e06) |
| F30 | `0x506a49` (e04), `0x506a74` (e05) |
| H30 | `0x564554` (e04), `0x56457f` (e05) |
| B01 | `0x44d004` (e03), `0x44d021` (e04), `0x44d03e` (e02); y fixed at 200 / 0 / 0 |
| I01 | `0x445dd6` (e02) |
| G03 | `0x524304` (e04), `0x524344` (e05), `0x524377` (e06) |

**In a reimplementation**, the screen x of a layer is `base - V - V / n`, with the division truncating
toward zero as the exe's `idiv` does. Converting to a rounded percentage (133%, 117%) drifts by a pixel
or two against the original. ScummVM's `pendulo` engine carries this table as `kExeParallax` in
`engines/pendulo/playable_scene.cpp`, one row per layer, each citing its first draw site.

Not established: where a layer sits in draw order relative to the actor and the scene's animations.
`pendulo` draws the layers last, in front of everything, which is right for the foreground poles and
trees seen so far but has not been read out of the exe.

**The actor drives nothing.** The hunt was repeated against the actor position globals (`0x92b923/25`,
`0x8fc883/85`, the `0x67fe40` table) over all four callees: no argument depends on them; actor y only
orders or hides sprites. In particular **E06 scrolls** (background 1380 wide, scroll max 356); a viewer that
reads it as one screen wide has picked the wrong background.

Two independent reasons this is parallax and not a fixed offset:

- `0x5642b0` is always called with the **live** view origin and `0x556220` always with the
  **last drawn** one, at otherwise identical arithmetic. That is draw-at-new / erase-at-old,
  which only a moving layer needs.
- Several scenes draw two or three surfaces in the same frame at different fractions of the view.

Structural null control: overlays with left <= 8 and full scene height, over all 74 archives, include every
entry drawn through a site, and no site names an entry outside them. Unmatched left-anchored overlays, with
no view-dependent site found: D03 e13, G05 e04, H10 e07 (all 1024 wide), F39 e07 (389 wide), G03 e07.

Every one of these is in the **high region** (0x468000 upward), the per-scene compiled code.
The scene-engine region (0x42xxxx-0x46xxxx) never scales the view origin at all. So parallax is
**hand-written per scene**, not a property of the scene format - which is why the exhaustive
search through the archives - overlay extents, the sprite descriptor width, the 704-byte
table, the second full-height rasters - correctly found nothing to find.

### The title wipe

Driver `0x43d080`, draw `0x43cec0`, bounding box `0x43d970`. The driver advances the sweep
(`0x5d2ee2`, BSS, so it starts at 0) by **one pixel per frame**, stopping at `0x5d2ee8`, which
`0x43d970` sets to the title overlay's own maxX **plus 0x64**. The draw compares how far the
sweep has passed a column against 0x64: past 100, full brightness; at or behind the edge,
nothing; between, each RGB565 channel is multiplied by that distance and divided by 100. The
ramp is a multiply **towards black**, not a blend with the backdrop.

So **there is no duration constant** - a wide title takes longer than a narrow one. The
wall-clock speed is not recoverable either: the image contains exactly one `Sleep` and its
argument is `0`, so the main loop is unpaced and ran as fast as the machine allowed.

### Three ways to get this wrong

Each of these produced a confidently wrong answer before being caught.

1. **Backtracking to find an instruction.** Searching for the bytes of an address and then
   disassembling from a few bytes earlier drops the `0x66` operand-size prefix, turning every
   16-bit load into a 32-bit one and finding arithmetic in the garbage that follows. Match the
   encodings exactly: `66 8B /r` with mod=00 rm=101, and `66 A1`.
2. **Missing the widening loads.** Matching only `66 8B`/`66 A1` misses `movsx`/`movzx`
   (`0F BF`/`0F B7`), and a signed coordinate calculation *always* widens. The parallax
   sites are `movsx`, and were invisible until those encodings were added.
3. **Substring register matching.** Tracking `ax` by substring matches `eax`, which made four
   unrelated `sar eax, 1` sites look like halved scrolling.

And recover a divisor as `2^(32+shift) / magic`, not from the magic alone: `0x66666667` is /5
at `sar edx, 1` but /10 at `sar edx, 2`.

## 8. Footsteps, chapter sound archives, and the lighting table

### Footsteps

The footstep trigger is the tail of the per-frame actor renderer (`0x431aa2`-`0x431bfe`).

- **Material.** The mask id under the actor's feet, `mask[y*sceneW + x]` (expanded mask `0xa838c8`,
  actor x/y `0x8fc883/85`), indexes zone-table page 5 (`0xac3c80 + 0x500`, read at `0x431b49`). That is the
  only read of page 5 in the image; page 4 is never read.
- **Sample.** `0x40a210(material, x, y, z, ...)` plays 3-D buffer `2*material - 2 + (rand() & 1)`.
  Material 0, or above 7, plays nothing (`0x40a31e`). Material m alternates between two variants.
- **Bank.** Not per scene, and not `RESOURCE.S<nn>`: `RESOURCE.002` (opened at `0x40e1f3`, handle `0x6d921c`,
  100-slot offset table at `0x6d40a0`). The loader loop `0x408c81`-`0x408e24` builds 14 buffers, buffer i from
  the PCM between slots **21+i and 22+i**, as 16,000 Hz mono s16 (WAVEFORMATEX built at `0x408cee`).
  So material m uses slots `19+2m` and `20+2m`: slots 21-34 are materials 1-7.
- Slots 5-21 share one offset and the exe reads slot 21 as the first footstep, so **the last slot of a
  shared run owns the entry**.
- Check: the 14 entries are 4,264-13,156 bytes (0.13-0.41 s). Lag-1 autocorrelation as s16 mono is
  0.63-0.998, against -0.016 for a shuffled null and about 0.1 read as u8. Material 7's two entries are the
  same length as material 2's, correlate 0.70 / 0.64 with them (null 0.04) and are 1.5x louder.
- **Trigger.** Actor state byte `0x8fc882` in {0, 6, 12}; `0x856708`, `0x8fc941`, `0x5d2f0c` all 0; and a latch
  `0x7a1cbc` that flips on every call, so the sound plays on alternate calls of the renderer. The walk-cycle
  frame number is not tested here, so which cels fire depends on how often the renderer runs. Loudness is
  `[0x8fc89d] * 100 / 455` percent.
- **Settled (2026-09-30).** `0x8fc882` is the walk frame, copied from the camino record by `0x42f260`
  (see *Walk frames and speed*). The check runs once per keyframe: `0x5fdb4f` is set by the actor tick
  `0x434080` on the 60 ms flag `0xa65a32`, and cleared by the footstep. So a step plays on walk frames 6
  and 12, and on the final frame-0 key while the walk flag `0x9d2c0f` is still set.
  - Suppressed by: talking, which draws the body with `0x431d20`, a renderer with no footstep tail;
    actorAnim `0x8fc941`; a save just loaded `0x856708`; and the mute flag `0x5d2f0c`, set by placeActor
    and by E08's scripted walks.
  - Off the mask the last material is kept. Only the chosen buffer is restarted.
  - At full effects volume the height changes nothing.
  - Pan is about `127*dx/sqrt(dx^2+200^2)`, with dx = actor x - (view x + 512).
  - NPC footsteps are 263 hard-coded `0x40a210` calls in room code, not this routine.

### Chapter sound archives

`RESOURCE.S0<n>` and `RESOURCE.M0<n>` are opened per **chapter** n (`0x40ee20`, called from `0x444b00` and
the chapter loader `0x444c60`-`0x444d17`), not per scene: M on channels 0-2 (music), S on channels 5 and 6
(effects, `0x409080` type 3). The chapter comes from the global scene number `0x6f4258`: `n = scene / 1000 - 2`,
with 2000-3999 both chapter 1 (`0x406783`-`0x4067ae`). Which chapter each scene archive belongs to was not
extracted. Effect ids index the 1000-slot table at `0xa670a0`, music ids the 100-slot table at `0x6d9060`.

### Lighting table (zone-table page 3)

Page 3 is read at `0x4314f5` (`0xac3f80 + id`) and indexes a table of 3-byte **percent multipliers** at
`0xa32ba0` (read at `0x431577`-`0x431587`, applied per palette entry with a /100 magic-divide). It is written
by immediate stores in each scene's own code, not loaded from an archive: B02 (`0x451442`-`0x45152c`) gives
index 0 = 100,100,100, index 1 = 60,60,80, index 2 = 80,80,100.

**How it is applied** (2026-09-30, `tools/exe/lighting.py`, which also emits the engine's table with `--cpp`):

- **Sampling.** Page 3 is sampled at the actor's position (`0x4314aa`). A class of 0 sends the lookup
  through the `0x430b50` search: down the column, then up from the bottom, then along row 0 to the
  right, then to the left. Off the mask, the previous class is kept.
- **Palette.** It is rebuilt only when the class changes (`0x43152f`, state `[0x9d2c0d]`), with no fade.
  - Class 0 copies the palette (`0x431543`).
  - Any other class maps each RGB565 entry to `r5*R/100, g6*G/100, b5*B/100`. The R, G, B bytes are
    signed; the result truncates and is not clamped.
- **The table.** `0xa32ba0` holds 10 classes. It is never cleared, so a class a room does not store keeps
  the previous room's value.
  - E03 has a run-time variant keyed on b[`0x9d26c1`], the window being cleaned.
  - F22 and H22 each have two loaders.
- **Shadow.** A kind-0 edge run gets coverage `0x7c`, so what is under the shadow keeps 131/255
  (`0x431a60`, `0x42fdb1`).

### One-shot overlays: `0x433080`

`0x433080(entry)` stamps an overlay into the composite `0x92af28`, clips its box to the view
(`0x4331b6`-`0x433260`) and presents that box at once. The stamp persists until the room restores the
clean background. `tools/exe/oneshot.py` lists the 13 sites:

- **B02 hook `0x44e180`** (`0x44e3a5`). This is the smoke detector LED, e(20 + b[`0x9d1d02`]): e20 off, e21
  on, 26x25 at (388, 25).
  - The step `0x44e650` runs on the 80 ms flag `0x92af22`. It stays off for 17 steps and on for 9,
    counting in `0xa33f01`, and sets `0x7a1cb4` to redraw.
  - Every entry resets it to 0. entry_2022's alternate hook `0x44e460` consumes the toggles without
    drawing.
- **D03 action_303 and action_304** (`0x45bea5`, `0x45bee7`). The same sequence in both:
  1. `actorIdleWait(4, 0xffff)` (`0x436170`).
  2. e13, a 1024x600 close-up at scene (0, 0).
  3. The line.
  4. `0x45bdd0`, which waits for the speech without redrawing and then restores the clean background.
  5. `0x436260`.
  `interactions.py` prints step 4 as `waitSpeech()`.
- **G05** `0x5283a0` (first visit, flag `0xa81900`) and action_301 `0x528500`: the same pattern with e04.
- **H10** `0x5399c0`: e07. It is also baked into the backdrop until the archive reloads.
- **D14 keypad** `0x479090`: the per-click function called from the scene loop at `0x479988`, not an
  entry handler. It draws e(2 + k) for k = b[`0x856874`] in 1..12, and the code is 8-1-3-7.

Also: F39 e07 is stamped into the backdrop by the loader helper `0x516680` for scene 6391 only (flag
`0xa81921`), which also remaps zone values 3 to 1 and 4 to 2 at `0xac3d80`. G03 e07, E01 e08 and E01 e13
(a 329-frame bird) are never drawn.

---

## 9. Scene interaction logic, and the extractor that dumps it

Section 1 established that the verb, item and cutscene logic is not in the data. It is compiled
code, but the code is regular enough to extract mechanically. `tools/exe/interactions.py` does that
for every room. It writes `docs/scenes/exe/<scene>_<room>.{md,json}` and `chapter<n>.{md,json}`.
This section records what the tool relies on, so its output can be checked by hand.

### Scene numbers and the dispatcher

The global scene word `0x6f4258` holds `chapter-ish * 1000 + room * 10 + variant`.

- **The dispatcher** at `0x406700`..`0x40738b` is a `cmp ax, K` ladder. Each hit calls one **scene
  function**. Several variants share one function: 2020..2023 all call B02's `0x4512b0`.
  Two rungs compare a register: the prologue loads `ebp = 0x1b62` (7010, G01) and `edi = 0x1fa4`
  (8100, H10) at `0x40670e`, and the ladder has `cmp ax, bp` (`0x406e52`) and `cmp ax, di`
  (`0x406f47`). `parse_dispatcher` tracks those moves; before it did, G01 and H10 were missing from
  the dumps and the scene table, and chapters 5 and 6 stopped after their intros.
- **After a chapter's intro returns, the dispatcher stores the first room itself**, around the
  chapter start call `0x444b00`: 2000 → 2020, 4000 → 4010, 5000 → 5030, 6000 → 6350, 7000 → 7010,
  8000 → 8100. Codegen's `INTRO_NEXT` holds these and ends each intro room with that scene.
  Scene 1000 (`0x445a80`) is the chapter screen, where the game starts and I03's credits end;
  see "The title sequence and the chapter screen" below.
- **The RESOURCE.003 stage is the loader's, usually `scene / 10`.** The loader seeks
  `[0x8083e0 + stage*4]` (the stage offsets RESOURCE.003's header holds), so scene 2020 is stage 202,
  `RESOURCE.B02`. But each loader computes the index itself, and not all use `scene / 10`:
  - Chapter 6's rooms subtract 2000 (H13's `0x53bc18`: `(scene - 0x7d0) / 10`). They are chapter 4's
    Douglasville rooms revisited and speak chapter 4's text: H19 is stage 619, not 819.
  - I01 and I02 subtract 1000 (stages 801, 802), and I03 loads stage 100 (A00's).
  - A loader that seeks none keeps the stage the previous room loaded. D07, the coffee machine
    close-up, speaks D06's stage 406. The intros (B00, D00, ...) seek none either.

  `interactions.py` reads the stage from the loader's emulation (the read of `0x8083e0`), and for a
  loader that reads none takes the stage of a room in the same chapter that enters it. The dumps'
  `stage` is that stage, and codegen passes it to `PlayableSceneConfig`.
- **A scene change is a store to that word**, for example `0x4500c6` `mov word [0x6f4258], 0xfa0`
  (B02 into chapter 2).
- **The first call in a scene function is the room's loader** (B02: `0x450ed0`).
- **Then the scene function picks the entry function** for the variant: it subtracts the base scene
  and jumps through a table (B02 table `0x451428`). After the entry function it runs the main loop.
  B02's four entries:

  | scene | entry | what it does |
  | --- | --- | --- |
  | 2020 | `0x44eaf0` | the opening |
  | 2021 | `0x44ec20` | back from B08 |
  | 2022 | `0x44ed10` | back from the window |
  | 2023 | `0x44ef60` | plays `DATAVB05.001` |

### The title sequence and the chapter screen

Before its loop, the dispatcher calls `0x445180` (at `0x406700`) and then sets scene 1000.

**The title sequence, `0x445180`.** Its loader `0x444f80` reads `RESOURCE.A02` into the 1024x600
buffer `0x94f4e8`. Entries 0 to 2 are sprites, at `0x68f0a0`..`0x68f0a8`. Entry 3 is an overlay,
at `0x68f0ac`.

1. **FX's logo, `0x4450f0`.** It draws entry 3 (`0x432fe0`) on black and fades it in over 1 s
   (`0x401ed0(100)`). It holds the logo 3 s on the 1 s counter `0x900f04`, fades it out (`0x401d70(100)`)
   and clears to black. The skip flag `0x9d2735` cuts the logo short without the fade out, and is
   cleared again afterwards.
2. **The theme.** The main theme starts with `0x4096a0(0, 0, 7, 2, 100, 0, 1, ...)`. That is
   `RESOURCE.002` entry 2 (12.02 s, 16 kHz stereo s16), once, on channel 7, which no other code
   uses. The screen is cleared and the next part is drawn straight on.
3. **Pendulo's logo, `0x444de0`.** Each part's frame starts at -1, kept in `0x5d2c40`, `0x5d2c44`
   and `0x5d2c48`.
   - Entry 0, the runner drawn by a spark, steps its 175 frames on the 40 ms flag `0xa65ac4`.
   - Once it is done, entry 1 (the frame) and then entry 2 (the name) step their 48 frames on the
     10 ms flag `0x6d93cc`. The loop only sees that flag about every 20 ms.
   - `0x4333c0(buffer, sprite, count, frame)` draws them. Its third argument is the frame count,
     not a position; each frame carries its own.
4. **The end.** Two seconds on the finished logo, then `0x401d70(100)` and black. It waits for
   channel 7's state byte `0x67a02e` (`0x679e7c + 7 * 62`) to drop to 0, then `0x4099f0(7)`.
   The skip flag ends everything from step 2 on.

**The chapter screen, `0x445a80` (scene 1000).** RESOURCE.000's header byte 2 (`0xa291f1`) is the
last chapter started. If it is 0, the screen sets scene 2010 (the opening) and returns.

- **Loading.** `0x4459c0` reads `RESOURCE.A03`'s seven raw 204x120 RGB565 pictures one after
  another into `0x94f4e8`: entry 0 is the prologue, 1 to 6 the chapters. `0x4455a0` writes a
  half-brightness copy of each after them: `(p >> 1) & mask`, where the mask is `0x7bef` in 565.
- **Layout.** `0x445670` fills the hit mask `0xa838c8` with 0xff, then each card's rectangle with
  its number. Cards 0 to n (n = the byte) go at the scene positions in the table at `0x5a4a64`:
  `u16 x, u16 y` pairs, seven to a row, and row n - 1 for n chapters:

  | n | card positions (x, y) |
  | --- | --- |
  | 1 | (410,150) (410,290) |
  | 2 | (410,150) (298,290) (522,290) |
  | 3 | (410,150) (186,290) (410,290) (634,290) |
  | 4 | (410,80) (186,220) (410,220) (634,220) (410,360) |
  | 5 | (410,80) (186,220) (410,220) (634,220) (298,360) (522,360) |
  | 6 | (410,80) (186,220) (410,220) (634,220) (186,360) (410,360) (634,360) |

- **Drawing and hover.** All cards are drawn dim (`0x445420(card, x, y, bright, present)`), and the
  screen fades in over 0.25 s (`0x401ed0(25)`). The card under the pointer is the mask's byte at
  `(mouseY - 84) * width + mouseX`. It is redrawn bright, and `0x445700(card)` sets the caption
  (`0x417270`): "JUGAR RUNAWAY DESDE EL COMIENZO" (`0x5bdfb4`), "JUGAR EL CAPÍTULO n" (`0x5bdfd4`,
  every 20 bytes), or an empty string for 0xff.
- **Clicks.**
  - Card 0 sets scene 2010, card 1 scene 2000, and card k `(k + 2) * 1000`. The screen is then
    cleared to black.
  - A click off the cards in the top band's options word (`y < 74`, `611 < x < 712`), or the skip
    flag, opens the options menu (`0x42daf0`). The screen is redrawn if no game was loaded.

**Chapter progress, `0x4440b0(ch)`.** `0x444b00` calls it whenever a chapter starts. At a scene
that is a multiple of 1000, if `ch` is greater than `0xa291f1`, it stores `ch` there and writes the
byte back into `RESOURCE.000` at offset 2.

### The three dispatch tables

The room's init writes these into BSS with immediate stores, when the loader runs.

| table | address | index | entry |
| --- | --- | --- | --- |
| hotspot x verb | `0xa29200` | `(hotspot*6 + verb)*4` | `{u16 action, u16 move}` |
| item x hotspot | `0x8fe7c0` | `(item*31 + hotspot)*4` | `{u16 action, u16 move}` |
| handlers | `0x9d1d40` | `action*4` | handler function |

**The lookup inputs:**

- **Hotspot** is page 1 of the zone table (`0xac3d80`), under the cursor.
- **Verb** is byte `0x6d91f0`: 1 look, 2 use/take, 3 talk, 4 go.
  - Right-click cycles through the hotspot's verb set, `0xa67060[h]`.
  - The sets are: 1 look/use, 2 look/talk, 3 use/talk, 4 look/go, 5 use/go.
- **Held item** is byte `0x94f4f8`. When it is non-zero, the item table is used instead of the
  hotspot table (`0x4328d0`, `0x432d50`).

**`move` says where the actor goes first:**

- `0xffff`: nowhere.
- `0`: turn to the look-at point, `0x9dad20 + 4h`.
- any other value: walk to the approach point, `0x5fdb60 + 4h`. In mode 1 the actor then faces
  `0xa24c20[h]`; modes 2 and 3 leave the facing free.

**Action ids.**

- **301..399 are the room's own.** The room installs them (B02 `0x4506a0`) and default-fills the
  range with the no-op `0x480df0`.
- **Ids below 301 are the chapter's global handlers**, installed at chapter start. These are the
  refusals ("Eso no tiene sentido", `say(0, 2, 0)`) and the inventory-screen actions. The
  installers:

  | chapter | installer |
  | --- | --- |
  | 1 | `0x4189e0` |
  | 2 | `0x4195b0` |
  | 3 | `0x41aa80` |
  | 4 | `0x41d2b0` |
  | 5 | `0x41df00` |
  | 6 | `0x41eda0` |

- **Handlers are called** through `call [reg*4 + 0x9d1d40]`: at `0x43426b` and `0x43430e` for the
  room, and at `0x411dc8` and `0x411e7e` for the inventory.
- **Code is shared across rooms.** The linker merges identical functions, so a room's handler can
  sit in another room's code block. B02's handler 308 is at `0x540110`.

**The inventory screen has four more tables per chapter.** The chapter's inventory init fills them.
In chapter 1, *look* at item k in the inventory is global action 49 + k.

| table | address | index | read at |
| --- | --- | --- | --- |
| verb on an item | `0x8fc4a0` | u16 action at `(item*6 + verb)*2` | `0x411e76` |
| held item on an item | `0x7a1d60` | u16 action at `(held*81 + target)*2` | `0x411daa` |
| default verb | `0x74d3a0` | u8 verb at `item` | `0x411989`, `0x411d45`, `0x411eda` |
| verb set | `0x7a1d00` | u8 set at `item` | `0x411f38` |

**The inventory's verbs work like a hotspot's.** Verb, held item and handler table are the same
globals as in a room. The code:

- **Hover** (`0x411989`): the item under the cursor becomes `0x9d27f8` (slot) and its verb
  `0x74d3a0[item]`.
- **Right click with nothing held** (`0x411f2d`): toggles the verb inside the item's set,
  `0x7a1d00[item]`. The sets are the hotspot ones: 1 look/use, 2 look/talk, 3 use/talk.
- **Right click with an item held** (`0x411ea9`): drops it. The verb goes back to the hovered
  item's default, or 0 over empty space.
- **Left click with nothing held** (`0x411de0`):
  - verb 2 (hand) with `0x8fc4a0[item*6 + 2] == 0` picks the item up. It becomes the held item
    (`0x94f4f8`) and the verb becomes `0xa683e0[item] + 1`.
  - Otherwise it runs `0x8fc4a0[item*6 + verb]`.
- **Left click with an item held, on its own slot** (`0x411d45`): puts it back. The verb becomes
  the item's default.
- **Left click with an item held, on another item** (`0x411d8e`): runs `0x7a1d60[held*81 + target]`.
  Actions 1 and 5 first call `0x40fc30`, which picks one of the random refusal actions
  (15, 17, 3, 24, 2, 31, 32 or 5).

The values the inits write:

| chapter | default verb | verb set | exceptions |
| --- | --- | --- | --- |
| 1, 2 | 2 (hand) for items 1..19 | 1 (look/use) | none |
| 3 | 2 (hand) for items 1..80 | 1 (look/use) | item 23 defaults to 1 (look); it also has a use action, 93 |
| 4, 5, 6 | 2 (hand) for items 1..80 | 1 (look/use) | none |

So hovering an inventory item shows the hand and reads "COGER", and a right click turns it into
"MIRAR". Item 0 is 0 in both tables.

**The bottom-band label** is built by `0x415210` from strings at `0x5bc9fc`: `MIRAR `, `COGER `,
`USAR `, `HABLAR CON `, `IR A `, `DEJAR `, ` CON `, then `INVENTARIO`, `OPCIONES`,
`CONTINUAR JUEGO`, `CARGANDO...`. Item names come from `0x6d93e0 + item*81`. On the inventory
screen (`0x4154d7`):

| state | label |
| --- | --- |
| verb 1 (look) | `MIRAR <item>` |
| verb 2 (hand) | `COGER <item>` |
| verb 0 | the item's slot-0 override string, below |
| verb 3 (talk) | nothing |
| held item over its own slot | `DEJAR <item>` |
| held item over another item | `USAR <held> CON <target>` |

A per-item, per-verb override replaces the look and hand labels when it is not empty. It lives at
`0x9d2c20 + item*405 + verb*81`. A per-pair override for `USAR ... CON` lives at
`0x9500e0 + (held*81 + target)*81`. Overrides are templates that are copied one character at a
time (`0x415300`, `0x415540`). `#` (`0x5c165c`) becomes the item's name, or the held item's name in
a pair override. `@` (`0x5c1658`) becomes the target's name in a pair override.

**All of this text is compiled into the exe; no data file carries it.** Each chapter start calls a
text function that fills the tables from string literals in `.data`:

| chapter | text function | called from |
| --- | --- | --- |
| 1 | `0x5805b0` | `0x40ef9a`, `0x40f0ea` |
| 2 | `0x581ac0` | `0x40f2da` |
| 3 | `0x583850` | `0x40f5ea` |
| 4 | `0x585e10` | `0x40f84a` |
| 5 | `0x589230` | `0x40f99a` |
| 6 | `0x58a270` | `0x40fb5a` |

Each function:

- **writes the item names**, `0x6d93e0 + item*81`, upper case and Windows Latin-1 (`0xc1` is Á).
  This is not the cp850 of `RESOURCE.003`. Chapter 1 has "CARTERA", "LLAVES DEL COCHE", "RESGUARDO",
  "TROZO DE ÁMBAR", "BOLSA DE GINA", "TABLETA DE PASTILLAS" and so on for items 1..19.
- **fills the rows past the last real item with alternate names**, e.g. chapter 1's 20..29
  ("JERINGUILLA CON ALCOHOL", "ROTULADOR INYECTADO", ...). `0x416120` copies the right one into
  an item's row from the item's state. For example, item 11 gets row 23 when `0x8fc980 >= 2` and
  row 27 otherwise.
- **clears both inventory override tables** and sets the few overrides the chapter has. The only
  one in the game is chapter 3 item 23, "PLAN", whose hand label is "PONER EN MARCHA EL PLAN".
- **fills the scene's held-item-on-hotspot labels**, `0xa33f20 + (item*31 + hotspot)*81`, which
  `0x4157a5` shows over a room. The default is the template "DAR # A @" (`#` the item, `@` the
  hotspot). Specific ones are written after it, e.g. item 9 on B02 hotspot 7, "QUEMAR BOLSA DE GINA".

A translated release carries its own copy of all of this in its own exe. The Italian (`GUARDA`,
`PRENDI`, `USA`, ...) and Brazilian (`OLHAR`, `PEGAR`, `USAR`, ...) exes are different compiles, so
none of these addresses hold in them.

The inventory inits:

| chapter | inventory init |
| --- | --- |
| 1 | `0x448180` |
| 2 | `0x457f90` |
| 3 | `0x479ca0` |
| 4 | `0x4b2f50` |
| 5 | `0x517510` |
| 6 | `0x529870` |

### Room hotspot names, labels and verbs

Rooms have the same kind of compiled-in text as the inventory. Each room loader calls a **room
text function**, which fills tables indexed by the zone-table page 1 hotspot `h`:

| room | scene function | loader | text function |
| --- | --- | --- | --- |
| B02 | `0x4512b0` | `0x450ed0` | `0x580a40` |
| B06 | `0x454150` | `0x453e70` | `0x5813b0` |
| B08 | `0x456f20` | `0x456c60` | `0x581800` (called at `0x456c70`) |

| table | address | entry | written by |
| --- | --- | --- | --- |
| name | `0x92af40 + h*81` | Latin-1 string | text function |
| per-verb label template | `0x83ac40 + h*405 + v*81` | string, `#` = the name | text function |
| held item over hotspot | `0xa33f20 + (item*31 + h)*81` | string, `#` = item | text function |
| default verb | byte `0x9d1ce0 + h` | 1 look, 2 use, 3 talk, 4 go | loader |
| verb set | byte `0xa67060 + h` | 1 look/use, ..., 4 look/go | loader |

- **Rows past the room's real hotspots hold alternate names**, as for items; the room's state code
  copies one over a live row. Sites: B02 `0x450bc6` (names) and `0x450dc8` (zone remap), B06
  `0x453b21`, B08 `0x456b50` (row 10 "BOTE DE ALCOHOL" over row 4 once the bottle is looked at).
  The inventory equivalent is `0x416120`.
- **The same code rewrites zone page 1** to hide or merge hotspots as the room changes. For
  example, B08 clears the bottle's zones once it is taken.
- B08, as its loader and text function leave it:
  - Names 1..9: PAPELERA, GRIFO, PUERTA, BOTE, CARTEL, TUBERÍA, ALCACHOFA, ARMARIO, TAZA DEL WATER.
  - Labels: "ABRIR #" (use, h1/h2/h8), "DESENROSCAR #" (use, h7), "USAR #" (use, h9),
    "SALIR DEL BAÑO" (go, h3).
  - Held labels, for every item: "TIRAR # A LA PAPELERA" (h1), "TIRAR # AL WATER" (h9).
  - Verbs: h3 defaults to go with set 4; every other hotspot defaults to look with set 1.
  - Scale: `height = y * 0.26627219 + 295.50296` (`0x4570c1`, `0x4570cb` via `0x4aca10`).
  - State bytes: `0x9d1d20` (marker taken from the bin), `0x9d1d21` (alcohol: 0 not seen, 1 taken,
    2 looked at).

**`0x9d23f9`, the hurried exit.** The click handler sets it at `0x407d43` when the clicked hotspot
matches `0x9dae00` for the zone under the pointer; the dispatcher clears it at `0x406749`. Exit
handlers test it and skip their door animation when it is set. For example, B02's door (action
346, `0x450120`) runs `if (!b[0x9d23f9]) actorAnim(5, 5); playSound(3, 0, 5, 47, 100); scene = 2080`.

### Scene number to archive: `scene_table.py`

`tools/exe/scene_table.py` turns the dumps into a table of every scene number the dispatcher
knows, giving for each one the archive its loader opens and the background entry it loads. It is
the source of the pendulo engine's `scene_table.cpp`:

```
python scene_table.py > <scummvm>/engines/pendulo/scene_table.cpp
```

**The background entry** is the first entry the loader reads once it has pushed both the offset
table (`0x856420`) and the size table (`0x94f420`):

- B02 reads `[0x94f420]`, which is entry 0.
- F26's second scene function (6263, and H26's 8263) reads `[0x856470]`, which is entry 20.
- F22's second function (6226) reads entry 0 and then entry 4, so which background it shows is
  not settled.

The engine's scene index is the background's ordinal among the archive's entries. Scene functions
that open no archive are chapter titles and cutscenes: 1000, 2000, 2010, 4000, 4010, 5000, 5140,
6000, 7000, 8000 and 9020. `virtual_archive()` gives each of them, except the chapter screen and B01's
2010, its room's name as a placeholder (`RESOURCE.D00`, `RESOURCE.I02`, ...), so the engine can find
them by archive. Codegen then tells the engine what that room really loads:

- **The intros (B00, D00, E00, F00, G00, H00, and I02 around its videos)** call `0x43da20(n)`, which
  loads `RESOURCE.A00` and chapter title `7 + n`, and speak stage 100. They get
  `config.archiveFile = "RESOURCE.A00"` and `config.chapterTitle`. Their videos keep the room's own
  stage: I02's `DATAVI02.001` speaks stage 802 (the Sandretti interrogation), not 100.
- **D01** only plays videos (`config.noArchive`, a black background).

A false negative to watch for: `0x40cd20` frees and reallocates the scene buffers right before
E14's real loader `0x4a9720`. It is not a loader (`interactions.NOT_LOADERS`).

### Fades

These routines fade the gamma ramp through `IDirectDrawGammaControl`:

- The interface is queried at `0x401c7e` and `0x40786a` (IID at `0x5bfe3c`).
- Its pointer is kept in `0x6d93c0`.
- `0xa0bac8` records whether gamma is supported.
- The saved ramp is at `0x92c420`.

| function | what it does |
| --- | --- |
| `0x401d70(n)` | Fade to black. On each tick where `0x807b68 == 2`, subtract `ramp/n` from every channel. After `n` such ticks, zero the ramp. |
| `0x401ed0(n)` | Fade in from black to the saved ramp. |
| `0x401ce0()` | Set the ramp to black at once. |
| `0x401d40()` | Restore the saved ramp. |

- **Step counts.** `n` is 50 at most call sites; 25, 100, 200 and 300 also occur.
- **Callers.** The callers are chapter, cutscene and video code (`0x436787` is on the video
  close-up path). **None is in B02, B06 or B08.**
- **Dispatch.** Every dispatch calls `0x404e90(1)`, which clears the back buffer to black and
  flips 8 times.
- **Not fades.** `0x415ff0`/`0x439790` and `0x416060`/`0x4395e0` are cursor save and restore.

**A room change does not fade.** Take B02 to B08 and back:

1. The exit handler sets the scene word.
2. The dispatcher's `0x404e90(1)` zeroes the back buffer and BltFasts it to the primary 8 times.
   This is a visible cut, since `0x5c11b8` is 1 except in the no-gamma fallbacks.
3. The loader shows the CD icon (`0x436850`), loads, and hides the icon (`0x436860`).
4. The entry then waits while channel 5's busy byte (`0x679fb2`) is set, which is the previous
   room's door sound, for at most 5 steps of the 250 ms counter `0xa33f04`
   (B08 `0x455f10`, B02 `0x44ec32`).
5. The screen stays black throughout, because redraw needs `0x9d1cd4`, which only `0x415ff0` sets.
6. The room then appears on its first frame at full brightness.

The "fade" is this black hold. The only fades in the room code range, `0x44dd00`..`0x44e0b0`, are
in `0x44dce0`, the intro pan after scene 2010.

**Timing.** The fade loop runs `n + 1` ticks. A tick is the 10 ms multimedia timer
(`timeSetEvent(10, 0, 0x40caa0, 0, TIME_PERIODIC)` at `0x406679`), whose callback sets
`0x807b68 = 2`. So a 50-step fade takes about 510 ms.

**The callback's pace flags.** Each tick, `0x40caa0` also counts down a set of dividers and sets a
flag byte to 2 when its divider runs out. Room code polls a flag for 2, does one step, and stores
1. A loop that waits on a flag nothing sets never ends: D10's entry loop waits for
`0x92af21 == 2`. The engine emulates the flags (`PenduloEngine::kTimerFlags`): a flag reads 2
whenever a new period has started since its last store.

| flag | period (10 ms ticks) | used by, e.g. |
| --- | --- | --- |
| `0x807b68`, `0x6d93cc` | 1 | the fade loop, the actor tick `0x434080` |
| `0xa65ac4` | 4 | |
| `0xa65a32`, `0xa6708e` | 6 | `0xa6708e`: the 60 ms pace of the room animation loops (B02/B06 window climbs) |
| `0xa0bb01` | 7 | |
| `0x5fdb38`, `0x92af22`, `0x92af21`, `0x92af25`, `0x92af24`, `0x92af2c` | 8 | `0x92af22`: B02's LED; `0x92af21`: D10's door loops |
| `0xa838d9` | 10 | |
| `0xa838d8`, `0x8fc940` | 25 | |
| `0x68f16c` | 200 | |
| `0x9dade0`, `0x890680` | the bytes `0x5c12e1`, `0x5c12e2` | `0x890680`: E10's peek |

The generated rooms read the flags in loops as `pace(flag)` and store 1 as `takePace(flag)`;
both go to the same emulation. F37's lift waits on `0x5fdb38`, E10's loops on `0x92af22` and
`0x890680`.

### Cursor

The cursor atlas (`RESOURCE.000` slot 112, and slots 113-117 for chapters 2-6, loaded by
`0x405730`, `0x4058e0`, ... `0x405fa0`) is a grid:

- **Cells are 60x60, 15 to a row.** Cell `i` is at `((i % 15) * 60, (i / 15) * 60)` (`0x4394b0`).
- **It is drawn centred on the pointer**, at `(mouseX - 30, mouseY - 30)` (`0x439504`), so the
  hotspot is always the cell centre.
- **The current cell** is byte `0x8bc178`.

| cells | contents |
| --- | --- |
| 0-3 | crosshair, magnifier, hand, balloon |
| 4-7 | exits, up-left, up-right, down-right, down-left (`4 + 0x8fc8c0[hotspot]`) |
| 8 | the blob every morph passes through |
| 9-24 | morph strips of verbs 0-3, cells `4v+9 .. 4v+12`, cursor end first |
| 25-40 | morph strips of the four exits |
| 41+ | held-item cursors in pairs, `0xa683e0[item] + h`; `h` = 1 over a hotspot (white outline) |

**The morph.** Code sets `0x5fdbfc` = old verb, `0x6d91f0` = new verb and `0xa67084` = 1, then
calls the click `0x40a330` (slot 150; it only plays when `0x94f401` is set). The timer callback
(`0x40caca`) then steps `0x9dade1` every 2nd tick, i.e. every 20 ms:

- steps 1-4 show `(old + 2) * 4 + step`;
- step 5 shows cell 8;
- steps 6-9 show `new * 4 + 18 - step`;
- step 10 ends it.

With new verb 4 (exit), steps 1-4 and 6-9 add `4 * direction` (`0x40cb31`), so the old half is
shifted as well. A second trigger during a morph does not reset the step.

**Held-item cursors.** When an item is held, the cursor cell is `0xa683e0[item] + h`.

- **The table.** Each chapter start fills it; chapter 1 does so at `0x40f050`. Items 1-15 get
  41, 43, ... 69; item 16 gets 81; items 17-19 get 71, 73, 75.
- **The outline, `h`.** `h` is 1 in three cases:
  - in a room, over a hotspot with a non-zero verb set (`0x433c33`);
  - on the inventory page, over a tile holding an item (`0x4118cf`, from the page's zone map
    `0xa838c8` and the tile contents `0x807ba0`);
  - when an item has just been picked up (`0x411e10`).
- **State variants.** Chapter 1's part of `0x416120` swaps icon (`0x9dac60`) and cursor together
  while a state byte is 1:

  | item | state byte | icon | cursor |
  | --- | --- | --- | --- |
  | syringe 15 | `0x8fc981` | 20 | 77 |
  | chart 16 | `0x8fc982` | 22 | 83 |
  | head 17 | `0x8fc983` | 21 | 79 |
  | glass 13 | `0x8fc986` | 23 | 85 |

  Otherwise the icon is the item's own id and the cursor is the base value. Item 11 (the marker)
  changes only its name.
- **Every chapter.** The chapter switch `0x42de40` (jump table `0x42df88`) calls one start function
  per chapter, and each start calls its cursor init and then its state function with argument
  `0xff` (all items):

  | chapter | start | cursor init | state function |
  | --- | --- | --- | --- |
  | 1 | `0x40f0e0` | `0x40f050` | `0x416120` |
  | 2 | `0x40f2d0` | `0x40f240` | `0x416360` |
  | 3 | `0x40f5e0` | `0x40f520` | `0x416560` |
  | 4 | `0x40f840` | `0x40f750` | `0x4168c0` |
  | 5 | `0x40f990` | `0x40f910` | `0x416c50` |
  | 6 | `0x40fb50` | `0x40fab0` | `0x416cb0` |

  The state rules are not all "byte == 1": some items have several looks by value (chapter 4's
  item 26 has five), and some take any non-zero value. Chapter 3's function reads the dword at
  `0x8fc980` once and switches on its low bytes, so a read hook must count every byte of a load.
  Chapter 3 item 15's plain icon is 29, not its id. No item depends on two state bytes.
  [`tools/exe/item_looks.py`](../../tools/exe/item_looks.py) emulates the inits and state
  functions (each state byte set to 0..7) and writes the engine's `item_looks.cpp`; run
  without `--cpp` it lists every chapter's rules.
- **After a state change.** Callers re-read the cursor straight away, for example `0x418651`
  after loading the syringe.

The cells were checked by rendering slot 112: 41 is the wallet, 43 the keys, and so on up to 85,
the glass of water.

These morph: hover (`0x433cf2`), leaving hotspots (`0x433d7e`), right click (`0x433998`), and
their inventory counterparts (`0x411989`, `0x411a2d`, `0x411f2d`). Item pick-up, drop and resets
are instant.

**The pulse.** Every 5 ticks, with no item held and no morph running, counter `0x8bc158` steps
0..19. For a counter value above 1 the draw uses a darkened copy of the cell,
`T = 0,0,1,2,...,9,9,...,1`. `0x405510` makes those copies in rows 9-13 of an 840-row buffer, with
per 565 channel `R, B *= (31-T)/31` and `G = G*2*(31-T)/63`.

### The primitives

Each has a call site that shows it in use.

| primitive | address | arguments | verifying call site |
| --- | --- | --- | --- |
| say | `0x413990` | `(table, row, frame)`: 0 static cues, 1 the stage's cues, 2 continue a line (see note 1) | `0x44eb61`, `say(1, 0, 0)` in the B02 opening |
| waitSpeech | room copy, B02 `0x44e7e0` | spins on `0x6d4230` | `0x450067` |
| addItem | `0x4107f0` | `(item, preTable, preRow, preFrame, postTable, postRow, postFrame)`; table 3 = no cue (note 2) | `0x44f19a`, `addItem(13, 3,0,0, 0,62,0)`, the glass |
| addItemQuiet | `0x410ed0` | `(item, resetCursor, ?)`, no cue | `0x41802b`, `addItemQuiet(9, 1, 1)`, the matches |
| removeItem | `0x410e30` | `(item)`: clears the slot and compacts the list | `0x44ff2e` |
| clearHeldItem | `0x411290` | `()`: clears `0x94f4f8` and resets the verb | `0x45006c` |
| has-item test | byte `0x7a50e0 + item` | non-zero means held | `0x450005`, item 9 |
| play video | one function per video, e.g. `0x44a510` plays `DATAVB06.001` | opens with `0x43a4b0(0x94f500, "DATAV\\...")`, closes with `0x43a4f0` | `0x450071` |
| change scene | store to word `0x6f4258` | | `0x4500c6` (4000), `0x450198` (2080) |
| scene flags | bytes owned by the room; B02's are `0x9d23e0`..`0x9d23f9` | plain stores and tests | `0x455fe6` sets `0x9d23e0` |
| walkTo | hook `[0x9daddc]`, B02 `0x44e880` | `(fromX, fromY, toX, toY, facing, flag)`, then waitWalk, which spins on `0x9d2c0f`. A target off the mask (region 0) takes the start's region (`0x44e957`..`0x44e986`), so the actor walks straight to it, e.g. out through D10's door to (-100, 465). There is no snapping to the mask; clicks use the nearest-walkable hook `[0xac5a10]` | `0x44eb8a` |
| placeActor | `0x434c20` | `(x, y, facing, flag, region)` | `0x44eb57` |
| actorAnim | `0x435200` | `(anim, arg)`, then waitAnim, which spins on `0x8fc941` | `0x44f174` |
| setObjectState | room copy, B02 `0x4508e0` | `(n)`: switches the room's sprite states; the loader calls it with `0xff` | `0x44f187` |
| playSound | `0x409080` | `(type=3, 0, channel, id, volume, pos...)` | `0x450193` |
| playSfx | `0x40ade0` | `(id)` | `0x44ff43` |
| stopSound | `0x4099f0` | `(channel)` | `0x45008c` |
| playFootstep | `0x40a210` | `(material)`, see §8 | |
| rand | `0x58e030` | reduced with `idiv` for `% n`, and with `and 0x80000001` for `% 2` | |

Notes on the table:

1. The static cue table is indexed by `(row*10+frame)*5` and the stage's by `(row*100+frame)*5`.
   §5 calls `say`'s first argument a "stage". It is this table selector.
2. `addItem` appends the item to the list at `0x807ba0`, whose count is at `0x9dacca`, and sets the
   item's slot `0x7a50e0[item]`.

**Hooks.** The room also installs:

| hook | what it is |
| --- | --- |
| `[0x9d2694]` | waitSpeech |
| `[0xac5a04]` | scale |
| `[0x9daddc]` | walkTo |
| `[0xac5a10]` | nearest walkable point |
| `[0x9dac4c]` | per-frame redraw |

**Still unnamed.**

- `0x436170(kind, arg, redrawFn)`. It sets `0xa838ce` and redraws through `redrawFn` until the
  actor state clears it.
- Most other unnamed calls in the dumps are the renderer. Handlers call it directly when they
  animate objects:

  | address | job |
  | --- | --- |
  | `0x42f260` | begin frame |
  | `0x4332f0`, `0x4333c0`, `0x4334b0` | sprite frame |
  | `0x430cd0(cb)` | compose |
  | `0x431c00` | present |

- `0x408e80` builds a 3-D sound position vector.

### Inventory icons

The icons are **not** in slots 116/117. The inventory page draws each item from a per-chapter icon
bank (`0x410137`..`0x41016d`):

- **Each icon is 80x80 RGB565**, at `bankOffset + (icon - 1) * 0x3200`.
- **The icon number is `0x9dac60[item]`.** The chapter start sets that table to the identity
  (`0x40f0fb`), so the icon number is the item id.
- **The bank offset is a RESOURCE.000 slot offset.** The exe copies the offset table into
  `0x807c00` (`0x40e08f`). Then `0x410100`..`0x41010b` picks an entry by `scene/1000 - 2`:

  | scenes | chapter | slot | icons |
  | --- | --- | --- | --- |
  | 2xxx | 1 | 119 | 23 |
  | 4xxx | 2 | 120 | 26 |
  | 5xxx | 3 | 121 | 42 |
  | 6xxx | 4 | 122 | 47 |
  | 7xxx | 5 | 123 | 17 |
  | 8xxx | 6 | 124 | 24 |

  The switch is a jump table at `0x410308` (index `scene/1000 - 2`, `0x4100c4`) reading
  `0x807ddc` + 4*k. Index 1 (3xxx) and anything above 8 fall through to `0x410114` with the bank
  offset never set, so the page is presumably never opened there. The bank is therefore slot
  118 + chapter. Each bank holds exactly as many icons as the highest icon number in that
  chapter's `item_looks.py` table. Slot 120 is 160 bytes (one row) short of 26 icons. The exe
  reads each icon with one 0x3200-byte read into `0xa25ee0`, so icon 26's last row is whatever
  that buffer held before; the engine pads it with black.

Slot 119, rendered:

| icon | item | icon | item |
| --- | --- | --- | --- |
| 1 | wallet | 13 | glass |
| 2 | keys | 14 | pillows |
| 3 | envelope | 15 | syringe |
| 4 | amber pendant | 16 | blank chart |
| 5 | Gina's purse | 17 | dummy head |
| 6 | chart | 18 | spray can |
| 7 | blanket | 19 | vademecum |
| 8 | pill blister | 20 | syringe |
| 9 | "Pink Iguana" matchbook | 21 | wigged dummy head |
| 10 | wig | 22 | chart |
| 11 | marker | 23 | cup |
| 12 | medicine bottle | | |

### Verified against play: the B02 fire detector and the bathroom door

**Matches: item 9, icon 9.**

- The purse is item 5. B02 hotspot 7 gives it, through action 313.
- *Look* at the purse in the inventory runs global action 54 (`0x417f10`). It says "Veamos...".
- If item 9 is not held, it calls `addItemQuiet(9)` and says "La caja de cerillas puede serme útil."

**Spray: item 18, icon 18.** B06 hotspot 4 ("Es un spray de limpieza.") runs action 309
(`0x453690`), which calls `addItem(18)`.

**Fire detector.** Using item 18 on B02 hotspot 12 runs action 342 (`0x450000`).

- **It needs three things:**
  - flag `0x9d23e0`, meaning the attack has happened;
  - `0x8fc984`, meaning the Somnilotón entry has been read in the vademecum (item 19, global
    action 68);
  - hasItem(9).
- **When all three hold**, it plays, in order:
  1. `say(1, 37, 2)` ("Buena idea, podría utilizarlo como un lanzallamas..."), then clearHeldItem;
  2. **`DATAVB06.001`**;
  3. `DATAVB02.001-004` (`0x448f60`);
  4. `DATAVB07.001` (`0x44c370`).

  Then it sets scene 4000, chapter 2.
- **Without the matches** it says `say(1, 37, 0)` ("...necesito un mechero o unas cerillas").
- **Item 9 alone on the detector** runs action 343: "Está demasiado alta...".
- So this is two items on one hotspot, not a combination: the spray is held, the matches are in the
  inventory.

**Bathroom door.**

- B02 hotspot 1 with verb go runs action 346, which sets scene 2080 (B08, the bathroom).
- B08's entry (`0x455f00`) checks three flags:
  - `0x9d23e7`: the chart is on the empty bed (set by action 335, item 6 on hotspot 10);
  - `0x9d23e8`: the dummy is in the bed (actions 340/341);
  - `0x9d23e0 == 0`.
- If all three hold, it sets `0x9d23e0 = 1` and scene **2023**.
- B02's entry for 2023 plays **`DATAVB05.001`**, followed by `say(1, 81, 5)` and `say(1, 81, 6)`.

### Draw order: each room's redraw hook (`redraw.py`)

The exe has no depth sort. **Each room installs a redraw hook at `[0x9dac4c]`** (B02: `0x44e180`)
that draws the room's layers and the actor into the scene-sized composite `0x92af28`, in an order
it picks by branching on the actor's position and on room state. The clean background is
`0x900f08`. A frame starts with `0x42f260`, which restores the actor's old box from the clean
background, and ends with `0x4324b0` (blit), `0x431c00` (present) or `0x4054d0` (end of frame).

The draw primitives:

| Call | Draws |
|---|---|
| `0x430cd0(cb)`, `0x431d20(cb)` | the actor; `0x431d20` when speaking (`0x6d4230`), or when a head frame (`0x8fc999`) or idle head (`0x8fc86d`) is set |
| `0x4333c0(blob [0x94f4e8], entry, count, frame)` | the opaque spans of one sprite frame |
| `0x432fe0(entry)` | an overlay (spans with pixels) |
| `0x432ea0(entry)` | an **occluder**: `u16 n`, then n x `{u16 x, u16 y, u16 length}` with no pixels. It copies the clean background's pixels over the composite in that silhouette. B02 e12, e14 and e15 are occluders (size = 2 + 6n exactly). |
| `0x5642b0(entry, x)` | a parallax overlay at a view-dependent x |

The translator ignores these calls: `0x556220` (erases a parallax layer at the last drawn view
`0xa838de`), `0x4332f0` (erases a sprite frame's box), `0x4334b0` (presents a frame's box) and
`0x433080` (a one-shot overlay after the blit).

**Entry `e` is `[0x68f0a0 + 4e]`, an absolute archive slot.** B02 e12/e14/e15 are pure span lists,
and F26 scene 2 uses e27 to e34. An entry can also be indexed: `[0x68f0f0 + b[var]*4]`.

**The actor pixel test.** This is at `0x42fd92`, `0x42ff63`, `0x4308a1` and `0x430936`. It
covers body pixels (colour `0x6f4300`, alpha `0x68f1a0`) and shadow pixels (`0x890740`, colour
`[0x9d2460]`). A pixel is drawn only if actor y `[0x8fc885]` > `T[page2[mask(x, y)]]`.

- **`T` is the u16 table at `0x9dada0`**, set by the room loader or its helpers. F13 sets
  `{0, 480, 599}`; B02 sets only `T[0] = 0`, so in B02 the mask never hides the actor.
- **Handlers change `T` at run time**: D06 stores 1199 at `0x9dada2`.
- **Entries past those the loader sets are BSS.** The engine reads them as 0.

Actor x/y are the u16s `0x8fc883`/`0x8fc885`; `0x8fc880` is an actor state byte of unknown
meaning. The view is at `0x68f094` (x) and `0x807b94` (y).

**B02's program** (from `redraw.py --ops`):

- **If y < 503:** the actor, then sprite e22 (frame `b[0xa838c2]`), overlay e11 and sprite e17
  frame 0.
- **Otherwise:** those three first, then the actor. This is why Brian at the foot of the bed
  (y ≈ 565) is in front of the footboard.
- **Then:** if y < 434 and x < 600, occluder e14, or e15 when `b[0x9d23e8]` is set. If x > 1000,
  occluder e12.

**`tools/exe/redraw.py`** translates every room's hook. How it works:

- It finds the hook by emulating the room loader. It follows a wrapper with a single callee
  (D07 goes to `0x46e230`).
- It translates the code after `0x42f260` into a flat program of `if`/`goto`/`end`/draw ops. It
  duplicates tails so that pushed arguments survive joins, then simplifies the result.
- It reads `T` by emulating the loader, following helpers with up to three callers.

Output modes:

- The default is a summary.
- `--ops` prints the programs.
- `--cpp` writes the pendulo engine's `redraw_table.cpp`.

Results: 61 rooms translate with no unknown calls left. **E01 and G03 do not translate**: E01's
hook never calls `0x42f260` and draws no actor; G03 begins with its own copy of it (`0x523b80`)
and draws y-scrolling layers through `0x44cd90`/`0x44cc30`. Both are transcribed by hand in the
engine (`SceneE01`, `SceneG03`); see below. F35 and H35 translate to a program that draws nothing.

**E01 (travel map, hook `0x47c5b0`, full-frame order from `0x47c72f`).** Sprites, each at a frame
word the room's timers step every 80 ms (the 10 ms timer `0x40caa0` arms the room flags every
8 ticks at `0x40cc1c`):

1. e09 at w[`0x890670`] (48 frames, `0x47ca70` loops it).
2. e11 at w[`0x890660`] (186), e14 at w[`0x9dacc8`] (307), e10 at w[`0x94f4ec`] (700): clips that
   play once to the last frame and park; parked, each restarts at 0 on `rand() % n == 0`, with
   n = 10 / 50 / 10 (`0x47cb30`, `0x47cc30`, `0x47cab0`; active flags `0x856704`, `0x6f4274`,
   d[`0x7a50b4`]).
3. While e10 is active: overlays e16 then e17 for 0x100 < f < 0x151, and e17 for 0x1fa < f < 0x215.
4. e12 at w[`0x679e40`] (31) and e15 at w[`0x8fc8ba`] (1052): play once while `0x68f170` /
   `0xac8c44` are set (`0x47cbb0`), never restart.

Entry `0x47cf80` starts e10/e11/e14 at a random frame on `rand() & 1` (or if still active) and
parks them otherwise; one of e12 (from 0) or e15 (from `rand() % 80`) plays, the other is parked.
e13 (`0xa838c0`, flag `0x74dc7c`) is only erased, e08 is converted and never drawn. The loader's
`0x47d310` bakes e03-e06 into the backdrop for the visited flags `0x9d2700`-`0x9d2703` and e07
always.

**G03 (hook `0x524080`).** Sprites e10 and e11 at w[`0x5cb4a4`] (96 frames, `0x524530`), e08 and
e09 at b[`0x5cb380`] (48, `0x5244f0`), the actor, then `0x44cc30` overlays at
e04 ((496 - V) / 6 - 124, (300 - Vy) / 6), e05 ((496 - V) / 4 - 166, (300 - Vy) / 4) and
e06 ((496 - V) / 2, (300 - Vy) / 2), truncating. The scale hook `0x524ef0` returns 12 px; the loader
sets T = {0, 899}, view max x 496 (`0x8093f2`), y range 0 (`0xa294ec`) to 300 (`0xa29186`), 100 on a
first visit until the arrival walk starts. e07 is converted but never drawn by G03's code.

**Not extracted:**

- **Frame operands of the form `TABLE[counter]`** (e.g. `0x5a77a4 + w[0x9d2c16]`). The room's
  timer code drives the counter, and that code is not read. The engine uses its own layer clock
  instead.
- **Room sprites a loop draws itself.** Some loops draw a room sprite straight to the screen
  through a helper of their own, rather than through the redraw hook. Examples: B06's window climbs
  (`0x453010(slot, count, frame)`), B02's climb in (`0x44e460`), and D10's doors (`0x472b10`). The
  frame usually comes from a local byte table indexed by the loop's counter. `interactions.py` keeps
  these: stack operands are relative to esp at entry (`stack[0x4]` is the first argument, locals are
  negative), an indexed read keeps its index (`stack[-0x2c + eax]`), and each function's byte stores
  to its locals are saved as `stackBytes`. `codegen.py` lifts such a call to `scriptScreenSprite()`.
  If the helper draws no actor after the sprite, the sprite hides him: the exe erases the sprite's
  box, which removes him there.
- **Sprite frames stamped into the background.** `0x433610(surface, handle, count, frame)` copies
  a frame's runs into the clean background `0x900f08` (the row stride is the scene width
  `w[0x7fc576]`). The frame then stays until the room reloads, and the actor is erased against it.
  For example, D10's door Go (`0x475b00`) leaves the door open (slot 21, frame 12) while Brian walks
  out, and D06's entry 4062 stamps its door open, closes it, then stamps frame 0. The exe follows a
  stamp with erase, draw and present (`0x4332f0`/`0x4333c0`/`0x4334b0`) of the same frame, which
  shows it at once. `codegen.py` lifts the stamp to `scriptStampSprite()` and marks those three
  calls as showing it.
- **A sub's loop that reads the sub's arguments.** Generated loops run once a frame, after the sub
  that started them has returned. So `codegen.py` queues the arguments with the loop
  (`scriptLoop(id, a0, a1, a2)`), and the body reads them as `loopArg(i)`. Example: D06 `0x467390`,
  whose argument picks the door.
- **Room size.** Each loader stores the scene size to `w[0x7fc576]` x `w[0x7fc56e]`, often in a
  helper. The dumps record it as `sceneSize`, and the background entry's size over the width as
  `rasterSize`. When the height is not 600, `codegen.py` writes `config.backgroundWidth/Height`
  (D06 1760x1200, E10, F37, G02, H37, I01, I03), plus `config.rasterHeight` when the picture is
  shorter than the scene (F27, §10.6). E10, F37 and H37 used to load at 600 rows and fail, because
  the dumps only kept the size words when the room's code read them.
- **`0x433080` one-shot overlays.**
- **What `0x8fc880` means.**

### What the extractor cannot do yet

- **Cutscene and close-up rooms have no tables.** That is 27 of the 82 scene functions (180 dispatcher entries); E04,
  for example, is a random choice made in the scene function. For these the tool decompiles the
  scene function itself. There is no event timeline.
- **The pseudo-code is flat.** It uses labels and `goto`, not structured `if` and `while`.
- **Values in stack slots are not tracked.** Constant propagation works per register, so an argument
  computed into a local shows as `stack[0x..]`. B02's `say(1, 42, rand()%2)` prints as
  `say(1, 42, stack[0x0])`.
- **Stage cues in global handlers are unresolved in the chapter files.** Their text depends on the
  current stage, so only the room files resolve it.
- **The speaker is unknown.**
- **Dialogue trees come out as raw code.** Gina's talk (action 303) is code over `0x4140f0`. The
  menu loop is decoded by the code generator, not the dump (§ "What lifting the dialogue trees
  took").
- **Flags and globals have no names.** Room flags, and globals such as `0x8fc984`, print as raw
  addresses.
- **Tables are read from a fresh game.** The loader runs with BSS zeroed and follows only helpers
  that have a single caller, or whose every caller is a room loader. A table that depends on
  progress, rather than being fixed at load, is read in its initial state. The room state function
  the loader calls with 255 (F08 `sub_4bcb30(255)`) is called by handlers too, so it is skipped and
  listed under `loaderCalls`.

Two shapes the extractor had to learn (2026-10-09):

- **A room revisited in a later chapter shares its init.** F13 and H13 both call `0x53bf20`, which
  fills the hotspot table, and `0x58a970`, the room text function. Helpers with two callers were
  skipped, so F13, F18, F21, F26 (6263) and their chapter 6 twins came out with no hotspots. Helpers
  whose callers are all loaders are now followed (`Emu.shared_inits`).
- **A scene function can tail-jump to its body.** F27's `0x4f2a00` calls the loader and then
  `jmp 0x4f2530`, a walk-through (Brian crosses, comments on the crater, and leaves for 6291). D07
  and H29 are the same. The jump target is now the scene's entry.

What lifting the E10, F37, H37 and F29 entry loops took (2026-10-09):

- **Room-local helpers are subs, not primitives.** A function below the library whose callers all
  sit in one scene function's code (E10's `0x49b570`, `0x49b910`, `0x4a2020`) is decompiled
  into the room, however many callers it has (`Dumper._room_local`).
- **A loop can call the room's own subs.** F29's loop runs `0x4fb6e0`, which steps the chief's
  animation and ends the loop. A sub with no waits, speech, walks or cycles is lifted a second time
  as `now_X()`, which runs its stores and sounds at once (`codegen.RoomCtx.call_now`).
- **The redraw clears dirty flags.** The room's redraw hook zeroes the variables its loop sets to
  ask for a redraw (F37: `0x5cbc54`, `0x5fdb4f`, ...). The dump lists them as `redrawClears`, and a
  loop's `pumpMessages()` clears them, so a loop that waits for the clear ends.
- **Lost compares.** A block that is only a conditional jump, reached from a block that set the
  flags, is threaded: the jump is emitted where the compare is. 29 lost compares became 10.
- **Switches and back tails.** `switch` jump tables become `if (x == n) goto`, and a backward jump
  to a straight run that returns (`state = 0; take the pace; return`) gets its own copy, so the
  body is not mistaken for a loop.
- **`actorIdleWait(x, 0xffff)` is no hold** (`0x4361d2`): it is a look, not a wait.

What the cutscene rooms took (2026-10-10):

- **The Escape flag folds away.** A cutscene loop tests `b[0x9d2735]` (Escape), then calls
  `0x439b40`, the in-game menu, and leaves if it returns nonzero. The engine never sets that flag,
  so `lift.fold_escape()` drops the test, the call and that exit. Many loops are left with one exit
  (I01's music fade).
- **Re-entry loops become one call.** `L: sub_X(); if (c) goto L` (I01's pans: `0x446387` calling
  `0x446240`) re-runs a sub that has its own frame loop, until that loop's condition holds. The sub
  returns only when the condition holds, so `lift.collapse_reentry()` keeps only the call. A test may
  also jump to the labels right after the run (D00's story at `0x43e687`).
- **Timeouts against the 250 ms counter.** `w[0xa33f04] < w[0xa33f04] + n`, with the left side
  read again each frame and the right side the value at loop entry, is "n steps since the loop
  began". It lifts to `loopTicks() < n`.
- **A loop's own stores read the variable as it is.** In a loop body, `x = x - 4` reads `ev(x)`,
  not `sv(x)`. `sv()` sees stores still queued in the script: I01's pan read the credits'
  `view x = 0` (`0x4365d0`) and stayed at 4.
- **The intro block (`0x43da20`..`0x43f60b`).** Its talk subs (`0x43d6c0`, `0x43d6f0`, `0x43d770`,
  `0x43d7f0`, `0x43d870`, `0x43d8f0`) loop while speech plays and lift to nothing. `0x43d560(n)`, the
  story animation, is not lifted: the decompiler loses `eax` across its switch. The opening that
  uses them (`0x43dff0`) is hand-written in the engine (`SceneA00`), from the trace above.

What lifting the dialogue trees took (2026-10-10):

- **One loop, run by the engine.** The 13 talk handlers that copy a 0x578-dword template to
  `0xac4420` all run the same loop: the `0x40b690` menu, the player's line, the reply, the effect
  code, then a switch on the action. The decompiler loses the register-held level, node and action
  (`b[0xa838bd] = ecx`, `stack[-0x8]`), so the loop is not lifted. `codegen.lift_conversation()`
  takes from the handler only what is the room's own. The engine runs the loop
  (`PlayableScene::showConversationMenu()`).
- **What is lifted.** The template is read from the exe at the `rep movsd`'s `esi`, which the dump
  now prints. It becomes a `DialogueTreeEntry[]`, with the player row from the menu call and the NPC
  row from the reply's `call_4140f0`. The prologue becomes `scriptBeginConversation(handler)`, the
  room's patches of the tree, and `scriptConversation()`. The code between the loop's head and
  the menu runs before each menu (H10 `0x53a2bf`). The effect code runs from the reply's end
  (`if (b[0xac4424 + r] == 255) goto X`) to the action switch, with `b[0xac442k + r]` being
  field k of the chosen option. The Escape block (`b[0x9d2735] = 0; call_40b2a0()`) and the code
  after the loop are lifted too. The first label after the switch whose code says nothing and jumps
  only within itself is where both endings meet.
- **Room actions above 4.** H38's switch has actions 5 to 7, which set the level to a constant and
  the node to `next`. They lift to `moveTo()` in `onConversationEffect()`.
- **What is left.** E16, F29, F30 and F37 reset the level after the switch, by registers the
  decompiler lost. F30's prologue stores `setcc` results the decompiler dropped
  (`b[0xac4688] = ebx`). D10's reply has its own animation calls. D05's effect jumps back to the
  head. H09's menu is the room's own wrapper (`0x534060`). H09's and H38's callers do not lift for
  other reasons. A sub with one argument keeps it in a register it never assigns (`arg_regs`), but
  that guess is off in talk handlers, whose unassigned registers are those lost `setcc`s.
- **Arguments past a call's own are saved registers.** E16's greeting
  `call_4140f0(1, 90, 0, 24, edi, esi, ebx)` lifts from its first four arguments.

---

## 10. The actor: skin, walk direction, walk speed, talking head

Read from the exe on 2026-09-29. VAs. "Verified" means read straight off the code at the cited
address; "inferred" means an interpretation of what the code does.

### Skin: which `RESOURCE.000` group, and why there are pairs

**The loader is `0x40e6f0`.** Every room loader calls it. It reads the offset/size tables
(`0x807c00`/`0x92caa0`, the `RESOURCE.000` slot tables copied at `0x40e08f`) with a computed slot
`g`. It caches `g` in `0x5c12f4` and does nothing if `g` is unchanged. What it loads (verified,
`0x40e7ac`-`0x40ea07`):

| slot | into | what |
|---|---|---|
| g | `0xa32c00` | walk frame table, 8 x 19 records |
| g+1 | `0x856500` | palette (converted to 555 in place if `[0x8093f0]`) |
| g+2..g+9 | `[0x7a50ac] + k*0xf59b0` | walk run data, direction k |
| g+10..g+15 | `[0x7a1d58] + k*0x5ef88` | head run data, head k |
| g+16 | `0x8fc9a0` | head frame table, 6 x 40 records |

It also sets the per-direction stride table `0xac4380` = `8,11,19,11,8,11,19,11` (`0x40ea46`).
Separately, `0x40dcf0` loads all six walk tables and all 216 `Resource.001` tables into one record
array `[0x94f4e8]` (152 records per group, in slot order) for bounding-box work.

**How `g` is chosen** (verified, `0x40e70c`-`0x40e775`):

- **The costume comes from `scene / 1000`**, through the byte table `0x40eac4` =
  `0,0,0,0,0,1,2,2,2,2` into three cases. Case 0 is group 10 (scenes 2xxx-4xxx, chapters 1-2).
  Case 1 is group 44 (5xxx, chapter 3). Case 2 is group 78 (6xxx-9xxx, chapters 4-6).
- **The byte `0x94f400` adds 17 when it is non-zero**: `neg; sbb; and 0x11; add base`. That selects
  the second group of the pair (27, 61, 95).
- **Each room loader sets `0x94f400`** just before calling `0x40e6f0`. B02 does this at `0x4511e4`:
  `cmp [0x94f400],1 / je / mov [0x94f400],1 / call 0x40e6f0`. So **scene 2020 (B02) uses group 27,
  palette slot 28** (identical to 11), not group 10. B06 and B08 also use 27.
- Chapter start `0x40ce20` and init `0x40e630` store `0xff` to force a reload.

**What the second group of a pair is.** Rendered: the two groups of a pair are the same drawing,
lit from opposite sides. The palettes are identical, but the run data differs everywhere (for
example 730 KB of 997 KB in slot 12 against slot 29).

- In group 10 the shaded side of the body is on the viewer's left, so the light comes from screen
  right.
- In group 27 the shaded side is on the viewer's right, so the light comes from screen left.

So **`0x94f400` is a key-light-side flag** (inferred from the rendering). The cast shadow is part
of the coverage stream, and it falls to the other side. That would explain why only `anchorX` and
`width` differ between the two tables (also inferred).

**The gestures use the same flag.** `0x40ebc0(arg)` loads a `Resource.001` entry (verified,
`0x40ec34`-`0x40ec99`). It is called from actorAnim `0x435200` at `0x43520a`. The entry index is:

    entry = ((skin*6 + gdir[facing])*2 + light)*6 + sub[arg]
    skin  = 0,0,0,0,0,1,2,2,2 by scene/1000
    gdir  = facing 0..7 -> 0,1,0,2,3,4,0,5
    sub   = arg 0..11   -> 0,0,1,2,3,4,5,2,2,3,4,5

So the "12 directions per skin" of `Resource.001` are really **6 facings x 2 light sides**.

**Scene to group.** This comes from `tools/exe/actor_skin.py`. "inherit" means the loader does not
set the flag, so the previous room's value stays.

| group | scenes |
|---|---|
| 27 | B02, B06, B08, D06 |
| 10 | D03, D05, D07, D09, D10 |
| 44 | E01, E02, E03, E05, E10, E16 |
| 61 | E06, E07, E08, E11, E13, E17 |
| 95 | F08, F09, F13, F18, F21, F22, F27, F28, F29, F30, G02, G03, G04, H09, H13, H18, H21, H22, H29, H30, H40, H41, H42 |
| 78 | F19, F20, F24, F25, F26, F35, F37, G05, H10, H19, H20, H23, H24, H26, H35, H37, H38 |
| inherit | A00, B00, B01, D00, D01, D14, E00, E04, E14, F00, F03, F39, G00, H00, I01-I03 |

For D03, D05, D09, D10, F20, F24, F37, H20, H24, H37 and H38 the store is `mov [0x94f400], bl`,
and `bl` is 0 from an earlier `xor ebx, ebx` (inferred from the register tracking, not traced).

### The facing enum is the walk direction

There is one enum. The renderer indexes the walk table as `0xa32c00 + (dir*19 + frame)*32`, with
`dir` = byte `0x8fc881` and `frame` = byte `0x8fc882` (verified, `0x4316c5`-`0x4316f5`). The
placement and walk code write the same facing to `0x92b921`. placeActor
`0x434c20(x, y, facing, flag, region)` stores facing at `0x92b921`, x at `0x92b923` and y at
`0x92b925`. It also centres the view so that the actor is 508-516 px from the left edge, in steps
of 4.

| value | direction |
|---|---|
| 0 | away from the camera (up the screen) |
| 1 | up-right |
| 2 | right |
| 3 | down-right |
| 4 | towards the camera (down the screen) |
| 5 | down-left |
| 6 | left |
| 7 | up-left |

The values come from the direction code below, which is verified. The same numbering is used by
the walk table, by the hotspot facings (`0xa24c20`) and by placeActor.

**Standing is never in profile.** At the end of a walk, after the final turn, facing 2 becomes 3
and 6 becomes 5 (verified, `0x42f1af`-`0x42f1f2`, which also patches the keyframe). That is why the
hotspot table only uses 1, 3, 5 and 7, and why the heads share 2 with 3 and 5 with 6.

**Choosing the direction from a movement vector.** This is the walk-leg builder `0x42e860`, at
`0x42ea00`-`0x42eb2e`.

- If `dx == 0`, the direction is 0 when moving up and 4 when moving down.
- Otherwise it compares `r = |dy| / |dx|` with two constants (verified):
  - `tan 10deg = 0.176327` at `0x5a46b0`;
  - `tan 50deg = 1.191754` at `0x5a46b8`.
- If `r <= 0.1763`, the direction is horizontal (2 or 6).
- If `r > 1.1918`, it is vertical (0 or 4).
- Anything in between is a diagonal (1, 3, 5 or 7).

So a horizontal sector is 20 degrees wide, a diagonal sector 40 degrees and a vertical sector 80
degrees. The sectors are not a perspective transform of the vector. They are just biased towards
the vertical directions. A forced direction (argument `+0x30` != 0xff) skips all of this.

**Turning in place.** The table `0x5a4418` is 8 x 8 x 4 bytes, indexed `[from*8 + to]`. The code
steps the facing through it one keyframe at a time until it equals `to`. Examples:

- 0 -> 4 goes 1, 2, 3, 4;
- 4 -> 0 goes 5, 6, 7, 0;
- 2 -> 6 goes 3, 4, 5, 6;
- 6 -> 2 goes 7, 0, 1, 2.

Each intermediate facing is shown for one keyframe.

### Walk frames and speed

The walk-leg builder does not play the walk live. It precomputes a keyframe list, the "camino"
table `0x67fe40`: 31-byte records with +1 facing, +2 frame, +3 x and +5 y, at most 2000 of them.
`0x42dfb0` appends one record. Verified:

- **Start**: frames 13, 14 in place, then 11.
- **The cycle** is frame + 1 per keyframe, wrapping from 12 back to 1.
- **The step count is adjusted** so the leg ends on frame 3 or 9 (jump table `0x42f228`).
- **The stop** is frames 15, 16 if it ended on 3, or 17, 18 if it ended on 9. Then frame 0.
- So 13-14 is the start and 15-16 / 17-18 are two stops, one for each foot.

The footstep trigger's states {0, 6, 12} in §8 are these frame numbers: 0 is standing, and 6 and 12
are the two footfalls.

**Steps in a leg** (`0x42e750`, verified):

    dist  = |dy| for dirs 0/4, |dx| for dirs 2/6, sqrt(dx^2+dy^2) for diagonals
    h     = (scale(from) + scale(to)) * 0.5        -- scale hook [0xac5a04], px; 455 = full size
    steps = dist / (stride[dir] * h / 455)         -- stride 0xac4380 = 8,11,19,11,8,11,19,11
    steps = trunc(steps) + (frac > 0.5)

Position is interpolated linearly over the steps (`0x42efaf`-`0x42f057`).

**So the stride per walk frame at full scale is 19 px horizontal, 11 px diagonal (Euclidean) and
8 px vertical, scaled by the actor's height / 455.**

**One keyframe per 60 ms** (verified 2026-09-30). `0x406679` calls `timeSetEvent(10, 0, 0x40caa0, 0,
TIME_PERIODIC)`, and the callback raises each flag every n ticks, phase-locked; consumers reset a flag
to 1 and never catch up.
- The counter `0xa838b4` wraps at 6 and raises `[0xa65a32] = 2` (`0x40cbdc`).
- `0x434080` tests the flag at `0x4341c2`, and `0x42f260` copies the next camino record into
  `0x8fc880` (`0x42f334`).
- The room redraw then clears the flag and increments `w[0x890678]` (B02 `0x44e423`).

So full-scale speed is **317 / 183 / 133 px/s**. The old "20 fps, 380 px/s" figure was an assumption.

### Talking head and lip sync

**The two renderers** (verified, B02's redraw `0x44e209`-`0x44e232`):

- If Brian is speaking (`0x6d4230`), a head frame is pending (`0x8fc999`) or an idle head move is
  on (`0x8fc86d`), the room calls **`0x431d20`**.
- Otherwise it calls **`0x430cd0`**, the full body renderer.

`0x431d20` redraws the body from the walk table at the current `dir` / `frame`, so the body does
not animate while talking. It then draws the head patch over it.

**The head patch** (verified, `0x431d29`-`0x431d4a`, `0x43222b`-`0x43225c`):

    head   = hmap[facing]   hmap = 0,1,2,2,3,4,4,5
    record = 0x8fc9a0 + (head*40 + [0x5c12e3])*32
    runs   = [0x7a1d58] + head*0x5ef88
    edge run count = record +0xc (edgeCountB alone)

The last line is the B-only rule `CLAUDE.md` found by rendering, now seen in code.

**Opening a viseme track.** `RESOURCE.004` is opened at `0x40e665`. Its 36,000-byte header goes to
`0x946760`. `0x413630(speaker, ?, voice)` reads track `voice` (`{u32 off, u16 size}` at
`voice*6`) into `0xac3940 + speaker*400`. Brian is speaker 0; speaker 1 is the NPC, whose reader is
`0x416de0`. For speaker 0 it also resets the tick `0x8bc168`.

**The per-tick reader is `0x416d50`**, for Brian. Its caller is `0x434744`, which stores the result
into `0x5c12e3` (verified):

    v = track[tick++]                           -- 0..5
    if hold (0x5e01cc) > 0:  hold--, frame = bank + v
    elif bank (0x5e01cd) != 0: bank = 0, hold = 3, frame = v
    elif rand() % 5 == 0:   bank = 6, hold = 3, frame = 6 + v
    else:                   bank = 0, hold = 3, frame = v

**So the viseme maps to a head frame directly: head frame = viseme (0-5), or viseme + 6 (6-11)
during a blink.** The blink bank is chosen per 4-tick window with probability 1/5, and never in
two windows in a row. That frames 6-11 are blinks is inferred from the idle code below, where frame
6 is the blink of rest frame 0.

**The idle head, when not talking** (`0x434789`-`0x4348cd`, `0x43437f`-`0x4343bf`, verified):

- **Blink.** With probability 1/15 per call, the head blinks for one call, depending on the
  expression `0x5e7304`:

  | expression | rest frame | blink frame |
  |---|---|---|
  | 0 | 0 | 6 |
  | 1 | 24 | 25 |
  | 2 | 17 | 18 |
  | 3 | 38 | 39 |
  | 4 | 31 | 32 |

- **Head turn.** A `rand() % 500` scheduler starts a turn. It steps `0x6f4268` (0..12, 6 = centre)
  by ±1 per call towards a target in `0x94f532` (0, 6 or 12). The frame comes from `0x5a4688` =
  `24,23,22,21,20,19,0,12,13,14,15,16,17` (or from `0x5a4698` = `38..33,0,26..31`, used when the
  expression is 3 or more, or the frame is 26 or more).

**The head's 40 frames:**

| frames | what |
|---|---|
| 0-5 | visemes |
| 6-11 | visemes with the eyes shut |
| 12-17, 19-24 | a head turn to each side, 17 and 24 the extremes |
| 18, 25 | blinks at those extremes |
| 26-31, 33-38, 32, 39 | a second turn set and its blinks |

**Idle and scripted head** (verified 2026-09-30):

- **The driver `0x4345c0`** runs on `0x9dade0`, with divisor `0x5c12e1`. It is 5 (50 ms) from a track open
  (`0x4136ac`) and 15 (150 ms) after a line ends (`0x412b5d`); the static value is also 15.
- **Idle blink** (`0x434789`). On each tick with no turn running (`[0xa838ce] == 0`), `rand()%15 == 0`
  starts a blink that lasts one tick. The expression `0x5e7304` picks the rest and blink frames
  (`0x4348e4`, `0x4348f8`): 0:0/6, 1:24/25, 2:17/18, 3:38/39, 4:31/32.
- **Turns** step one index per 60 ms flag (`0x43436e`), using T1 `0x5a4688` = 24..19, 0, 12..17 and T2
  `0x5a4698` = 38..33, 0, 26..31; index 6 is the centre. The step uses T2 if the expression is 3 or more,
  or if the previous frame is 26 or more.
- **Random glances** (`0x434428`) roll `rand()%500` on each 60 ms tick while idle.
  - At the centre: <2 turns to index 0 (expression 1), and <4 to index 12 (expression 2).
  - At a side: <2 sweeps across, and <20 returns to the centre (expression 0).
  - So a glance starts every 7.5 s on average and a side is held about 1.5 s. Set 2 is never chosen at
    random.
- **Scripted look** `0x436170(expr, hold, redraw)`, 73 sites: expression 1 or 3 turns to index 0, and 2 or
  4 to index 12. It sets `0x94f40c`, which keeps the random glances off.
  - With a hold, it waits (hold+1) ticks of the 250 ms counter `w[0xa33f04]`, blinking, then turns back.
  - `0xffff` leaves the head turned, and `0x436260` turns it back.
  - About 43 sites use expression 4 with hold 2. Which way set 2 faces has not been rendered.

The inventory portrait uses the same 12-frame convention, `portrait*12 + headFrame`
(`0x412197`-`0x4121bc`).

### Not established

- Which light side a scene should use is a per-room literal. Nothing ties it to the zone table's
  brightness page.
- What `0x856708` gates in `0x431d20`: the body redraw, or something wider. It has 406
  references. For footsteps it reads as "a save was just loaded".

**The NPC reader `0x416de0`** (read 2026-09-30, `tools/exe/npc_lipsync.py`, 88 sites):

- It is Brian's `0x416d50` with its own state: track `0xac3ad0`, tick `w[0x94ff9e]`, hold/bank
  `0x5e01d8`/`0x5e01ce`, output `0x5c12e4`, pace flag `0x890680` (divisor `0x5c12e2` = 5 at `0x4136e4`),
  voice channel 4 (`0x679f74`) and talking flag `0x5fdb40`.
- It knows nothing about sprites. Each room's own talk step maps the viseme to a frame, either `base+v`
  (12 frames) or a 12-byte table, and stores it in the variable the room's redraw passes as a sprite
  frame. It stores the viseme-0 frame at line end. For example, D07 writes `0x5d1b68 = v+24/54/105`,
  which e4 draws.
- `0x439bb0` is the video talk step: it draws `0x416de0`'s viseme as a mouth over a held video
  still (`0x43b250`, the `.000` mouth tables; see "The held stills' mouths").
- E10 and H10 test Brian's flag `0x9dade0` rather than the NPC's.

**Who speaks** (read 2026-09-30). `say(t, row, frame)` = `0x413990` is **always Brian** (speaker 0,
flag `0x6d4230`, `0x413700(0, ...)`). Its first argument is the cue table, not a speaker: 0 the static
cues (`0x6d4240`), 1 the current stage's cues (`0xa0c560`, row*100+frame, 5-byte entries), 2 the next
line of the same cue. `0x4140f0(t, row, frame, a4)` is its **speaker-1 copy, the room's NPC**: it
calls `0x413700(1, ...)` (`0x4143ac`), then `0x413630(1, ...)`, sets `0x5fdb40` (`0x4143bb`) and
plays the voice on channel 4 (`0x41448a`). a4 is 0x20 at every F09 site and only reaches the subtitle
routine `0x412850`; probably a colour (inferred). RESOURCE.003 does not say who speaks, so the
engine's `scriptSay` takes the speaker from the call site.

**F09's Oscar, a worked NPC** (read 2026-09-30):

- The scene function `0x4c2430` copies 180 bytes from `0x5ab1f8` to `0x5ceec0` (`0x4c2433`) and sets
  the sprite slot `b[0x5cf082] = 6` (RESOURCE.F09 entry 6, 113 frames; counts per slot at `0x5ab1f0`).
  `0x5ceec0` is BSS, so `redraw.py` reads it empty. Index `w[0x8bc156]` -> frame: 0 = 106; 1-30 =
  0-29; 31-61 = 11-41; 62-69 = 106; 70-88 = 42-60; 89-96 = 53-60; 97-110 = 53-66; 111-120 = 51-42;
  121-129 = 106; 130-165 = 71-106; 166-169 = 106; 170-175 = 107-112 (talk).
- The talk step `0x4c0f10` (called from the loops `0x4c10f0` and `0x4c2580`): while he speaks,
  `w[0x8bc156] = T[v]` with T `0x5ab2ac` = 170-175 twice (no blink); at line end T[0] = 170
  (`0x4c0fae`), and `d[0x7a50c0] = 1`.
- The idle driver `0x4c0ce0`, every 80 ms (flag `0x5fdb38`, `0x40cc0c`), on `b[0xa32bec]`: 0 picks
  1 (w = 0) or 2 (w = 70) by `rand() & 1`; 1 counts to 62; 2 counts to 121 with
  `playSound(3,0,5,80,10)` at 80; 3 counts 130-165, then 1 in 5 goes to 0, otherwise w = 130 and
  `playSound(3,0,5,76,60)`; the end of 1 and 2 is also w = 130, state 3, sound 76. 4 and 5 hold.
- `sub_4c1120` runs before his first line: the same increments, but each sequence ends with w = 0,
  state 5; then w = 170. So he finishes his idle before he speaks. After a conversation the handlers
  store state 3, w = 130 and sound 76.
- Entry 6090 (`0x4c16c0`) takes the state from `b[0xa32be7]` / `w[0x8bc160]` and always places Brian
  at (838, 551) facing 7. The first visit (`b[0xa81b40] == 0`) walks to (676, 510) and alternates
  Oscar's stage row 0 (even frames 0-14) with Brian's (odd frames 1-13).
- **Correction to `6090_F09.md`:** the `waitSpeech()` printed after `call_4140f0(1, 91, ...)` in
  `sub_4c25e0` is the dialogue wait loop `0x4c2580` (`0x4c268a` -> `0x4c2692`), not
  hook_waitSpeech `0x4c1090`. The latter runs the idle driver, not the talk step.

**The 704-byte F09/H09 entry is an occluder.** RESOURCE.F09 and RESOURCE.H09 slot 4 (byte-identical)
are `u16 n` = 117 then 117 `{x, y, length}` spans, the foreground boulder. F09's loader `0x4c2150`
stores its offset at `0x68f0b0`, and the hook `0x4c0810` calls `0x432ea0` on it at `0x4c0987`
(H09: loader `0x5337e0`, hook `0x530190`, call `0x530326`). `size == 2 + 6*u16@0` holds for 43 of
1,062 entries in 21 rooms.

---

## 10. Universal Rules for Scene Authoring

Implementing scenes across chapters (B02, B06, B08, D03, E05, F09) revealed consistent architectural rules compiled into `Runaway.exe`:

### 10.1 Chapter SFX and Music Archives
- Sound effects (`RESOURCE.S01` .. `RESOURCE.S06`) and music (`RESOURCE.M01` .. `RESOURCE.M06`) are opened per **chapter**, determined by `chapter = scene / 1000 - 2` (where 2000-3999 is Chapter 1).
- DirectSound channels 0-2 play music (`M0n`), channels 5 and 6 play sound effects (`S0n`, `playSound(3, 0, channel, id, volume)`).

### 10.2 Door Transitions and Black Screen Hold
- Exit hotspots (verb `kVerbGo`) play an exit sound effect (e.g. SFX 47 in B02/B08, SFX 58 in D03) and assign `scene = targetScene`.
- The dispatcher cuts the screen to black (`0x404e90`) and displays the CD loading icon (`0x436850`).
- The target scene entry sequence polls the channel 5/6 busy byte (`0x679fb2`, `0x679ff0`) in a loop capped at 5 steps of the 250 ms counter (`0xa33f04`, at most ~1.25 s; `0x455f10`, `0x44ec32`). Redraw is withheld, holding the screen completely black until the previous room's exit sound ends.
- The new room entry sequence then plays its entrance/closing sound (e.g. SFX 48 in B02/B08, SFX 59 in D03), places the actor, scripts the entrance walk, and displays the new scene.

### 10.3 Costumes, Walk Groups, and Light Bytes
- The skin loader `0x40e6f0` selects Brian's costume and walk group based on `scene / 1000`:
  - **Chapters 1 & 2** (scenes 2000..4999): Costume 1 (`costumeBase = 0`, palette slot 45). Walk group base = 10 (`0x0a`).
  - **Chapter 3** (scenes 5000..5999): Costume 2 (`costumeBase = 72`, palette slot 79). Walk group base = 44 (`0x2c`).
  - **Chapters 4..6** (scenes 6000..8999): Costume 3 (`costumeBase = 144`, palette slot 113). Walk group base = 78 (`0x4e`).
  - **Light Byte (`0x94f400`)**: If the room's loader sets light byte = 1, it adds +17 (`0x11`) to the walk group. Thus, walk groups across all scenes are always:
    - Ch 1/2: 10 (normal) or 27 (light = 1)
    - Ch 3: 44 (normal) or 61 (light = 1)
    - Ch 4-6: 78 (normal) or 95 (light = 1)

### 10.4 Scale Hook Formulas (`hook_scale` at `0xac5a04`)
- Each scene loader configures actor scaling via `0xac5a04` and float parameters at `0xa0baa0`, `0xa0baa4`, `0x8906c0`, `0x8906c4`:
  1. **1D Linear in Y** (e.g. B08, F13): `height = y * a + b`
  2. **1D Piecewise Linear in Y** (e.g. F09, split at \(y = 510\)): if \(y < y_0\) `y * a1 + b1`; else `y * a2 + b2`
  3. **1D Piecewise Linear in X** (e.g. D03, split at \(x = 459\)): if \(x < x_0\) `y * a1 + b1`; else `y * a2 + b2`
  4. **2D Bilinear perspective** (e.g. E05): `height = 0.5 * ((x * ax + bx) + (y * ay + by))`
- The calculated height is normalized by Brian's nominal height (455 px):
  `percent = CLIP((int)(height * 100.0f / 455.0f + 0.5f), 1, 100)`

### 10.5 Hotspot String Array Layout (`0x92af91`)
- In each scene's text function (`0x58....`), hotspot names are written into a contiguous buffer starting at `0x92af91`.
- The stride is **exactly 81 bytes (`0x51`)** per hotspot:
  `address = 0x92af91 + (hotspot - 1) * 81`
- This layout is invariant across all 82 scenes in `Runaway.exe`.

### 10.6 Scene Dimensions & Non-600px Heights (`0x7fc576`, `0x7fc56e`)
- Each scene loader configures the scene width and height in BSS globals:
  - Width: `mov word ptr [0x7fc576], width` (e.g. `0x400` = 1024, `0x4b0` = 1200, `0x578` = 1400, `0x6c0` = 1728, `0xa20` = 2592)
  - Height: `mov word ptr [0x7fc56e], height` (usually `0x258` = 600)
- Scenes throughout the game with non-standard heights:
  - `RESOURCE.D03` (Scene 4030): $1200 \times 670$ (`0x4b0 \times 0x29e`, 804,000 px, 1,608,000 bytes)
  - `RESOURCE.D06` (Scene 4060): $1760 \times 1200$ (`0x6e0 \times 0x4b0`, 2,112,000 px, 4,224,000 bytes)
  - `RESOURCE.E10` (Scene 5100): $1880 \times 700$ (`0x758 \times 0x2bc`, 1,316,000 px, 2,632,000 bytes)
  - `RESOURCE.F27` (Scene 6270): $1024 \times 700$ (`0x400 \times 0x2bc`, 716,800 px; loader sets `height = 700`, matching its 716,800 px mask). **Its raster is only $1024 \times 600$** (1,228,800 bytes): the exe reads a raster by its stored size, and the scene's last 100 rows have no picture. Brian starts at y 699, below it, and walks up into view
  - `RESOURCE.G01` (Scene 7010): $1400 \times 760$ (`0x578 \times 0x2f8`, 1,064,000 px, 2,128,000 bytes)
  - `RESOURCE.G02` (Scene 7020): $1640 \times 800$ (`0x668 \times 0x320`, 1,312,000 px, 2,624,000 bytes)
  - `RESOURCE.G03` (Scene 7030): $1520 \times 900$ (`0x5f0 \times 0x384`, 1,368,000 px, 2,736,000 bytes)
- Background rasters (entry 0) have size $W \times H \times 2$ bytes (RGB565), except F27's (above).
- Zone masks (entry 1) have run-length encoded streams totalling exactly $W \times H$ pixels.
- Both the background decoder and mask decoder must decode using the scene's exact configured dimensions rather than assuming a standard 600px height, otherwise scanlines drift by $(W_{calc} - W_{actual})$ on each row, causing diagonal shearing.



