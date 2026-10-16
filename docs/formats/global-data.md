# Global and UI data

Shared global assets — fonts, cursor art, interface icons, phrase lookup tables, lip-sync visemes, the
character sprite library, and *Hollywood Monsters*' shared colours — reside in numbered global resources
(`RESOURCE.000` through `RESOURCE.004`). The numbering is not stable across the games: what `RESOURCE.001`
and `RESOURCE.004` hold in *Runaway 1* is not what they hold in *Hollywood Monsters* (see below).

## Containers and formats

### 1. `RESOURCE.000` — UI atlas and font assets

Contains global textures and localized typography.

#### Layout

```text
0x000: byte[20] Header          -- begins: 03 01 06 01 01 01 ...
0x014: u32[500] Offsets         -- absolute byte offsets
0x7E4: u32[500] Sizes           -- byte sizes
0xFB4: <entry data>
```

The `u32` at `0x014` is entry 0's offset, and because entry 0 begins immediately after the table it
always reads as `0xFB4` (4,020). That is also a valid *Runaway 2* `table_half_bytes`, so a reader that
sniffs for the *Runaway 2* header before recognising this layout takes the file apart into hundreds of
plausible fragments instead of its 151 real entries — and whether it does so depends on how many header
bytes it happened to read, since the *Runaway 2* branch needs 8,064 of them. Recognise `0x014 == 0xFB4`
as *Runaway 1* first.

#### Hollywood Monsters layout

*Hollywood Monsters* puts a single byte in front of the table and uses 100 slots, so entry 0 begins at 801:

```text
0x000: u8          Header
0x001: u32[100]    Offsets     -- absolute byte offsets
0x191: u32[100]    Sizes       -- byte sizes
0x321: <entry data>
```

Its entries are UI and font art, plus the shared 80-colour palette block that completes every scene's
colour table — see [palettes.md](palettes.md).

#### Known payload slots (*Runaway 1*) (**verified**)

Quoting these as raw file offsets hides that every one of them is simply the start of a slot, so
they are listed by slot index here. The offsets are the ones a 500-slot table yields on the
shipped Spanish Steam build.

| Slot | Offset | Size | Purpose | Encoding |
|---|---|---|---|---|
| 10–111 | `0x0000FB4`… | ~53.9 MB total | Player character: 6 groups × 17 slots (walk/head tables, palettes, run data; see §2) | Character sprite codec |
| 112–115 | `0x35EB4EC`… | 976,320 each | Mouse cursor atlas, 904×540 | Raw RGB565 over key `0x6841` |
| 116, 117 | `0x39A4BEC`, `0x3A789EC` | 867,840 each | Inventory item icons, 904×480 | Raw RGB565, same key |
| 125 | `0x3D7CF4C` | 50,719 | Regular font glyph bitmap, 181 glyphs | Outline/fill shade, see below |
| 126 | `0x3D8956B` | 905 | Glyph table for slot 125 | 181 × 5-byte records |
| 127–130 | `0x3D898F4`… | 103,600 each | UI sprite strips, 700×74 | Raw RGB565 |
| 131 | `0x3DEEBB4` | 1,228,800 | Inventory screen, 1024×600 | Raw RGB565 |
| 132 | `0x3F1ABB4` | 1,228,800 | Main menu / options panel, 1024×600 | Raw RGB565 |
| 143–147, 151, 158 | `0x50E107C`… | 1,228,800 each | Full-screen item close-ups, 1024×600 | Raw RGB565 |
| 148 | `0x56BD07C` | 39,848 | Bold font glyph bitmap, 181 glyphs | Outline/fill shade, see below |
| 149 | `0x56C6C24` | 905 | Glyph table for slot 148 | 181 × 5-byte records |
| 161 | `0x692FCF5` | 15,842 | Small font glyph bitmap, 150 glyphs | Outline/fill shade, see below |
| 162 | `0x6933AD7` | 750 | Glyph table for slot 161 | 150 × 5-byte records |

The remaining slots in 112–159, identified 2026-09-30 by rendering them and by their
loaders in `Runaway.exe` (addresses are VAs, image base 0x400000):

| Slot | Size | Purpose | How established |
|---|---|---|---|
| 118 | 4,608 | 48×48 RGB565 badge, "runaway evaluation version" | Rendered. Loaded at `0x406150` into `0xa24c60`, whose first pixel becomes the key colour `0xa838c4`; nothing else reads `0xa24c60`, so retail loads it and never draws it (inferred: demo leftover) |
| 133 | 884,736 | 768×576, a byte-exact copy of slot 131's region (12,12)-(780,588) | 100% equal pixels; UI init `0x40d451` pastes it into the inventory buffer at (12,12). Probably the clean backing for erasing the item grid (inferred) |
| 150 | 4,978 | Sound effect, 16,000 Hz mono s16, 2,489 samples (~156 ms) | Sound init `0x4089ee`-`0x408b42` builds a DirectSound buffer {PCM, 1 ch, 16000 Hz, 16 bit} sized to this slot. Lag-1 autocorrelation 0.887 as s16 vs 0.007 shuffled. Which event plays it is not traced |
| 152, 154 | 4,696,000 each | **End credits**, 400×5870 RGB565, the two byte-identical | Rendered. Credits routine `0x4365d0` scrolls 5,270 = 5870 - 600 steps, draws 400 wide at x = 312 and fades the top and bottom 30 rows. Not padding |
| 153 | 5,160,960 | Filler, never used as data | 8.00 bits/byte, lag-1 autocorrelation 0.000 at every width. The exe reads slots 153+154 as one 9,856,960-byte block into `[0x94f4e8]`, overwrites it with the screen and 30 × 65,536 u16 fade tables (`0x436300`), and reads 152 over 154's copy |
| 157 | 4,608 | 48×48 RGB565 CD disc icon, the room-loading indicator | Rendered. Loaded at `0x4061f0` into `0xa65ae0`; `0x405180` draws it at output (0, 720), bottom left of the 4:3 frame. Room loaders call `0x436850` (show) first and `0x436860` (hide) last |
| 159 | 39,564 | "CAPÍTULO 1".."CAPÍTULO 6" labels: scene-sprite format, 6 frames of 115×28 at (843,142) | Rendered, tiles exactly. Loaded at `0x40d48f`; `0x412550(ch)` draws frame ch-1 on the inventory page under the logo |

Slot 131 is the **inventory** screen — the leather wall with the logo and the character portrait
oval — and slot 132 is the **menu**, which is easy to get backwards: 131 looks like a title
backdrop, but 132 carries the volume knob, the brightness lever and the two columns of five
setting holes that the shipped menu draws its LOAD / SAVE / ERASE / PLAY / EXIT column over.

#### Fonts (**verified, byte-exact on all three atlases**)

A font is **two** slots: a glyph bitmap and, in the slot immediately after it, a glyph table. The
table is what makes the bitmap readable, and looking for it is what turns the font from
"~184 glyphs separated by blank rows" into an exact decode.

```text
Glyph table (905 bytes = 181 records, or 750 = 150 for the small font):
    Record[N] × {
        u16 Offset      -- byte offset into the glyph bitmap
        u8  Top         -- blank rows above the glyph on its line
        u8  Height
        u8  Width
    }
```

The records **tile the glyph bitmap exactly**: each one ends where the next begins and the last ends
on the final byte of the entry. That is the format check, and it holds on all three *Runaway 1*
fonts (181 + 181 + 150 glyphs, 50,719 + 39,848 + 15,842 bytes).

`Top + Height` is the baseline and is near enough constant per font — 23 for the regular face, 22
for the bold one — so `Top` positions a glyph on the line and the line height is `max(Top +
Height)`.

##### The glyph bitmap is not run-length encoded, and it is not an alpha map

There is no RLE and no scanline marker. The bitmap is **one byte per pixel**, `Width` bytes per
row, `Height` rows:

```text
byte 0x00 .. 0x10: Shade, 17 steps along a ramp from the OUTLINE colour (0x00)
                   to the FILL colour (0x10). All of these are opaque.
byte 0x11:         Transparent — hugs the glyph and fills its counters
byte 0x12:         Transparent — the space beyond
```

The glyphs are **outlined, not antialiased**, so drawing one takes two colours rather than one.
Reading the byte as an alpha value and blending towards the background throws the outline away:
`0x00` is the darkest ink, not "invisible". A bold `O` makes it plain — `0x00` traces both the
outer and the inner edge of the ring, `0x10` fills the stroke, `0x11` sits in the counter and in
a one-pixel band all round, and `0x12` is everything further out:

```text
12 12 12 12 12 12 11 11 11 11 11 11 11 12 12 12 12 12
12 12 12 12 12 11 00 00 00 00 00 00 00 11 11 12 12 12
12 12 12 11 11 00 01 05 07 07 07 06 02 00 00 11 12 12
12 12 11 00 00 07 0f 10 10 10 10 10 10 0a 01 00 11 12
12 11 00 01 0d 10 10 10 10 10 10 10 10 10 0d 01 00 11
...
```

`0x11` and `0x12` are both transparent when drawing; the distinction is presumably what the
original renderer used to lay the outline down.

Reading `0x11` as "end of scanline" is the other trap. It decodes with no unexpected bytes —
every byte is a valid code either way — and produces a tall narrow column that segments into
hundreds of fragments instead of glyphs, so the error looks like a segmentation problem rather
than a wrong premise. There is nothing to segment: the table already gives every glyph's width
and height.

##### Glyph order

The glyphs are ordered by the **Spanish alphabet**, not by any character set, so a character maps
to an index only through a table:

| Index | Glyphs |
|---|---|
| 0–25 | `A`–`Z` |
| 26–39 | `a`–`n` |
| 40 | `ñ` |
| 41–52 | `o`–`z` |
| 53–57 | `á é í ó ú` |
| 58–67 | `0`–`9` |
| 68–79 | `(` `)` `-` `+` `=` `<` `>` `/` `\` `'` `;` `.` |
| 80–95 | `:` `"` `¡` `!` `¿` `?` `Ñ` `[` `]` `#` `´` `▶` `` ` `` `ß` `Ç` `ç` |
| 96–111 | `à è ì ò ù` `Á É Í Ó Ú` `©` `¯` `ü Ü` `«` `»` |
| 112–118 | `$` `&` `®` `▲` `▼` `●` `@` |
| 119–180 | Czech, Hungarian and Polish letters, `Œ`/`œ`, the remaining accented vowels, ending on an empty box |

`ñ` sitting between `n` and `o` rather than after `z` is the giveaway that this is a collation
order and not a code page. All three fonts share it — the small font is the same list truncated at
150 — so it is a property of the engine, not of one atlas. The bold face is caps-only: its
lower-case slots draw capitals.

The empty box at the end of the full tables is the obvious fallback for an unmapped character.

##### Which font goes where

The shipped menu is set in the **bold** face — which is the caps-only one, and every label there
is caps. Sampled off a screenshot of the original, it draws the fill white (254, 253, 253) over a
black outline (12, 0, 0), and an entry that is unavailable in white's place gets a muted warm grey
(118, 102, 100), outline and all.

The glyphs carry no advance width, and laying the bold face out with zero letter spacing
reproduces the original's label widths, so the engine simply butts the glyphs together. A space
has no glyph at all.

##### Pre-rendered strings

`FORMATS.md` describes the ~46 MB language-varying region from `0x3D7FA41` as pre-rendered string
bitmaps in "the font RLE format". That offset falls **inside slot 125**, so it is not the start of
anything; the claim is about the region of slots that differs between localised builds, not about
a distinct payload. With the glyph tables decoded, a reader can compose strings from the fonts
directly and does not need those bitmaps.

##### Runaway 2 and Runaway 3: the same fonts with a wider offset (**verified**)

The later two games keep the format exactly, in the same paired slots, and change one field: the
record is **7 bytes** because the offset is a `u32`.

```text
Record[N] x {
    u32 Offset      -- byte offset into the glyph bitmap
    u8  Top
    u8  Height
    u8  Width
}
```

That is the whole difference, and the reason for it is plain in the sizes: *Runaway 3*'s largest
glyph bitmap is 107,900 bytes, well past what a `u16` offset can reach. The tiling check is
unchanged and is what identifies the table, so the two record widths cannot be confused -- a table
read at the wrong width fails to tile.

| Game | Font slots (bitmap, table) | Glyphs |
|---|---|---|
| *Runaway 2* | 123/124, 125/126, 146/147, 161/162 | 247, 191, 191, 150 |
| *Runaway 3* | 123/124, 125/126, 138/139, 159/160, 161/162 | 247, 191, 191, 191, 150 |

An earlier note here said these games had no glyph table at all. They do; the search had been run
with *Runaway 1*'s 5-byte record, which no *Runaway 2* or *Runaway 3* table satisfies.

#### Cursor atlas (*Runaway 1* and *Runaway 2*) (**verified**)

Slot 112 is a 904×540 RGB565 raster holding every mouse cursor, over the key colour `0x6841`
(RGB 104, 8, 8), and it carries no index — the sprites have to be found by scanning.

Rows that are entirely key colour separate horizontal bands; only the first band (y 8–53) holds
cursors, and the bands below it hold other interface art. Within the band, columns that are
entirely key colour separate the sprites, but **not every gap is a separator**: gaps inside a
sprite (between the arms of the crosshair) are 1 px and gaps between sprites are 12 px or more, so
runs closer together than a threshold anywhere in 3–9 px belong to the same sprite. Every
threshold in that range yields the same **15** cursors, 23–46 px wide: crosshair, magnifier, hand,
speech balloon, four diagonal exit arrows, then a run of morph frames.

Hotspots are not in the atlas; they live in the executable.

*Runaway 2* stores the same thing the same way, at the same indices: slots 112–117 are six atlases of
exactly this size and key colour (*Runaway 1* has four, 112–115), and they scan into cursors on the same
rule. It also repeats the 1024×600 screens and the 700×74 UI strips, though not *Runaway 1*'s 904×480
icon sheets, and the art at any given slot is its own — only the shape is shared.

#### Interface rasters (**verified**)

The raw rasters of *Runaway 1* and *Runaway 2* carry no dimensions, so a slot becomes a picture in one of
two ways.

- **By size.** A whole number of screens (1,228,800 bytes = 1024×600×2) or one of the fixed interface
  geometries above.
- **By stride sweep.** Anything else goes through `RasterDecoder.Detect`, the same green-channel
  autocorrelation the scene backgrounds use ([rasters.md](rasters.md)). It finds the art that is its own
  odd size -- *Runaway 2*'s slots 155 and 156, for instance, are 264×104.

A size match is necessary but not sufficient. *Runaway 2* stores one long data stream in slots 289-291,
cut into pieces of exactly 1024×600×2 bytes -- the stream runs straight across the slot boundaries and on
into e292, which is a different size only because it is the remainder. Read as pixels those pieces are
noise, so a raster is confirmed by its pixels as well: artwork's three channels move together (the
`ChannelDisagreement` score is 5-9 for the real screens) where those chunks score 115+. This is the one
place in the reader where the pixels get a vote, and it is needed because a size can be a coincidence.

An entry that passes is named for its width: a full-screen one (1024 or 1280 px) is a `screen`, the known
interface geometries keep their names (`cursors`, `icons`, `ui-strip`), and anything the sweep recovered is
a plain `raster`.

#### Animations (**verified**)

The global archive is not only interface art. Some of its slots are the scene archives' sprite format
verbatim -- a `u16` frame count, a table of 14-byte frame records, then the frames -- self-contained in
one slot rather than split over the header/data pair that *Runaway 2*'s scenes use. In a hex view the
record table reads as stripes: the same five coordinates repeating every 14 bytes, with only the frame
offset changing.

| Game | Slots | Which |
|---|---|---|
| *Runaway 1* | 11 | 135-142 (Brian's inventory talking head), 155 (volume knob), 156 (brightness slider), 159 (the six chapter labels) |
| *Runaway 2* | 34 | 87-94, 105-107, 150, 152, 264-266, 270-287 |
| *Runaway 3* | 47 | 15-34, 51-63, 67, 176-181, 256, 260, 313-318 |

A sprite of one frame is an overlay rather than an animation, exactly as in a scene archive, which is why
*Runaway 2* shows 33 animations and *Runaway 3* 46. All 92 of them walk byte-exact and paint frames. They are recognised by that walk, not by their size:
a parse that tiles the entry exactly is proof, which is the same rule the scene classifier uses.

#### Interface art of the later games (**verified**)

The three games after *Runaway 2* keep no raw rasters for their interface at all.

| Game | Where | Format |
|---|---|---|
| *Runaway 3* | `RESOURCE.000`, 20 slots | Whole Windows BMP files, 24/32-bit `BI_RGB`, bottom-up |
| *The Next BIG Thing* | `RESOURCE.IFZ`, 24 of 95 slots | Whole PNG files, plus a few JPEGs |
| *Yesterday* | `RESOURCE.IFZ`, 61 of 69 slots | Whole PNG files |
| *Hollywood Monsters* | `RESOURCE.000` slot 42 | 1024×480 indexed pixels, one byte each |

*Runaway 3*'s bitmaps run from 48×40 panel widgets up to the 1280×720 title and pause screen, and
1280×1024 or 960×n sheets for the inventory. Several of the sheets are **two halves side by side**: the
artwork on the left and its greyscale matte on the right, which is why the BMP's own alpha channel does
not describe the shape. Nothing in the file marks which sheets are paired that way, so the decoder shows
the stored image as it is rather than guessing.

`RESOURCE.IFZ` is a plain offset table with no size half — the first slot's value is the table's own byte
length, which also gives the slot count — and an entry runs to the next **distinct** offset. That last
part matters: *Yesterday* points 48 consecutive slots at one PNG, so sizing an entry by the gap to the
next slot rather than to the next distinct offset empties most of the archive.

*Hollywood Monsters*' menu panel — the save-slot list, the preview window and the four setting sliders —
is the one screen-sized entry in its global archive. Like everything else in that game it is indexed, and
it takes its colours the usual way: the nearest palette block at or before it, completed from the shared
block (see [palettes.md](palettes.md)).

Not found, in any of the three: a keyed cursor atlas after *Runaway 2*. Those games' cursors are
somewhere else, and nothing here says where. Their fonts are not missing -- see the glyph tables above.

### 2. `Resource.001` — the character sprite library (*Runaway 1*)

*Runaway 1*'s characters are not in the scene archives and are not in the scene sprite format. They live
in `Resource.001`, a 108 MB library of motion cycles, as palette-indexed run streams behind a 32-byte
frame record. The player character is not in it; he has his own layout in `RESOURCE.000` (below).

#### Layout

```text
u32 offset[216]     at 0
u32 size[216]       at 864          -- 1,728 bytes of table in all
```

168 slots are live. An entry is **a fixed 3,200-byte frame table** (100 records of 32 bytes, the unused
tail zeroed) followed by `size` bytes of frame data, so it occupies `3200 + size` and **`size` excludes
the table**. Read that way the entries tile the file exactly — no gap, no overlap, ending on the last
byte, 108,761,688. That is the format test. Read without the 3,200 the entries appear to be separated by
168 identical 3,200-byte gaps, which is the tell.

There are 2,166 frames over the 168 entries, 11 to 20 each, so the file is one animation set for a few
characters rather than 168 separate sprites — the same shape as the voice shards.

#### Frame record

32 bytes. It is *Hollywood Monsters*' actor sprite descriptor with one extra count:

```text
u32 dataOffset       -- from the end of the 3,200-byte table; frame 0 is always 0
u32 opaqueRunCount   -- runs in the body stream
u32 edgeCountA       -- \ these two sum to the coverage stream's run count
u32 edgeCountB       -- /
u32 anchorX, anchorY -- the feet, measured from the frame box origin
u32 width, height    -- the frame box, about 167 × 420
```

#### Two streams per frame

The **body stream** is `opaqueRunCount` runs of

```text
u16 x, u16 y, u16 count, u8 index[count]
```

and each byte is a palette index — see [palettes.md](palettes.md). It ends exactly where the coverage
stream begins.

The **coverage stream** fills the rest of the frame, up to the next frame's `dataOffset`, and has a
7-byte header:

```text
u16 x, u16 y, u8 kind, u16 count
kind == 1  ->  count bytes follow
kind == 0  ->  none do
```

Its bytes are **coverage, not colour**: every value in the stream is a multiple of 255/63, a 6-bit alpha
widened to 8 bits. Putting them through the palette instead paints a hard white line around the
character, which is the quickest way to notice the mistake.

The stream is not an outline. It is the sprite's whole **footprint** — the silhouette together with the
ground shadow it casts — as an alpha mask over the same canvas the body draws on: `kind == 0` is a run of
full coverage, `kind == 1` carries the per-pixel coverage of the boundary, where the art is anti-aliased.
Measured over every frame of the library, no body pixel falls outside it, and the part of the footprint
the body does not fill is confined to the bottom tenth of the frame — the shadow under the feet.

So the two streams compose: the body gives the colour, the coverage stream gives the alpha. Where a pixel
is painted its alpha is the coverage byte; where it is covered but unpainted it is shadow, drawn as black
at `coverage * ShadowAlpha / 255`. That constant is the decoder's choice, not the engine's — the engine
pushes the framebuffer under the shadow through a darker colour table, and that table is in no
container.

Both streams ending exactly on their boundaries is the per-frame format test.

#### Run coordinates are canvas coordinates

The record's `width`/`height` are the run **extent** and `anchorX`/`anchorY` are the feet measured from
the frame box origin — but that origin is **not stored**. The run coordinates in both streams are
positions in a larger sprite canvas, and the box origin is the smallest coordinate the frame's runs use,
taken over the body and coverage streams together. The coverage stream always starts further left and
higher than the body, so it has to be included in that minimum.

To draw: shift every run by `-min`, then place the frame at `pos - anchor`.

Over the whole library — all 2,166 frames of the 168 live entries — `minX + anchorX` is **168** and
`minY + anchorY` is **434** in every frame, so the character's feet sit on one fixed canvas pixel
throughout, and the run extent equals `width` × `height` with no exception.

Reading the runs as box-relative draws the sprite 91 px right and 28 px low. It still looks like a
character standing in the scene, so it does not show up as an obvious rendering fault; it was caught by
clamping the runs to the declared box, which made most of the character disappear.

#### Which palette an entry wants

The library's 216 slots are **three blocks of 72**, one per palette: slots 0-71 take `RESOURCE.000`
slot 11, 72-143 take slot 45, 144-215 take slot 79. Nothing in the file says so; the partition was found
by comparing each entry's histogram of palette indices against the three tables. Own-block cosine
similarity is 0.92-1.00 and cross-block is 0.03-0.06 for all 168 live entries, with no entry ambiguous
and no group straddling a block boundary — so the mapping is structural, not a per-entry guess.
`CharacterSpriteArchive.PaletteSlotFor` is that arithmetic, and a real-install test re-measures the
margin rather than trusting a recorded answer.

#### The player character, in `RESOURCE.000`

The rest of the cast is in `Resource.001`; the character the player moves is not. He is in
`RESOURCE.000`, in the same codec but a different layout: **six groups of 17 consecutive slots**, at
slots 10, 27, 44, 61, 78 and 95, so slots 10–111 are his and nothing else is. Slot 10 starts at
`0x0000FB4` immediately following the archive header, and slot 111 ends at `0x35EB4EC` where the cursor
atlas at slot 112 begins.

| Group `g` | Slots | Chapters | Scenes | Lighting / Palette slot | Run slots (Walk / Head) |
|---|---|---|---|---|---|
| Group 0 (`g=10`) | 10–26 | 1–2 | 2xxx–4xxx | Lit from right (`slot 11`) | Walk `12–19`, Head `20–25` (Head table `e026`) |
| Group 1 (`g=27`) | 27–43 | 1–2 | 2xxx–4xxx | Lit from left (`slot 28`) | Walk `29–36`, Head `37–42` (Head table `e043`) |
| Group 2 (`g=44`) | 44–60 | 3 | 5xxx | Lit from right (`slot 45`) | Walk `46–53`, Head `54–59` (Head table `e060`) |
| Group 3 (`g=61`) | 61–77 | 3 | 5xxx | Lit from left (`slot 62`) | Walk `63–70`, Head `71–76` (Head table `e077`) |
| Group 4 (`g=78`) | 78–94 | 4–6 | 6xxx–9xxx | Lit from right (`slot 79`) | Walk `80–87`, Head `88–93` (Head table `e094`) |
| Group 5 (`g=95`) | 95–111 | 4–6 | 6xxx–9xxx | Lit from left (`slot 96`) | Walk `97–104`, Head `105–110` (Head table `e111`) |

Within each 17-slot group:

| Slot | Size | Contents |
|---|---|---|
| `g` | 4,864 | walk frame table, **8 rows of 19** records (32 bytes each) |
| `g+1` | 512 | the palette, `u16 colour[256]` RGB565 |
| `g+2` .. `g+9` | ~790 KB — ~1 MB | walk run data, one slot per facing (0: Back to 7: Up-Right) |
| `g+10` .. `g+15` | ~366 KB — ~389 KB | head run data, one slot per head |
| `g+16` | 7,680 | head frame table, **6 rows of 40** records (e.g. `e026`, `e043`, `e060`, `e077`, `e094`, `e111`) |

The records have the identical layout to `Resource.001`'s, and **a row of a table is one animation whose
runs are the matching slot**: walk row *d* against slot `g+2+d`, head row *h* against slot `g+10+h`.
Each row's first `dataOffset` is 0 and its last frame runs to the end of that slot, so the exact tiling
is the format test here just as it is in the library.

The executable picks a group by the chapter and by a key-light flag the room loader sets, which is why
they come in pairs: (10, 27), (44, 61) and (78, 95) are the same costume lit from opposite sides, and
their palettes are byte-identical. Groups 10/27 serve scenes 2xxx-4xxx, 44/61 serve 5xxx and 78/95 serve
6xxx-9xxx — see [executable.md](executable.md). The palettes are covered in [palettes.md](palettes.md).

##### Run streams and Frame 0 geometry sharing

Like `RESOURCE.001`, each frame is an opaque body stream of `{u16 x, u16 y, u16 count, u8 index[count]}` runs
followed by coverage runs. Frame 0 of each walk facing represents Brian's idle standing pose for that direction.

Because characters are rarely rendered in pure profile when standing idle in isometric/oblique adventure scenes:
- **Facing 2 (East / right) and Facing 3 (South-East / down-right)** share identical frame geometry: both use
  the exact same 32-byte frame descriptor in slot 10 (`143×427 at (91, 402)`) and the **exact same 665 body run headers
  `(x, y, count)`**, starting at $Y=32$ (the crown of hair). Facing 2 reuses Facing 3's standing pose with adjusted
  shading (29.4% byte differences in palette indices reflecting directional lighting).
- **Facing 5 (South-West) and Facing 6 (West)** symmetrically share their idle Frame 0 geometry and run headers.

##### A head's record describes the body, not the head

This is the one place the two readings genuinely differ, and it is what kept this layout unread. A head
is a patch painted **over** a walk frame rather than a frame drawn on its own, and its record is the
body's:

- **Its box and anchor are the walk frame's.** Head 0 of group 10 states 168×418 at anchor (89, 402),
  which is walk facing 0's frame 0 exactly. The patch itself covers 111×116. A reader that sizes the
  frame from the record gets a canvas four times too tall with the head in one corner.
- **Only `edgeCountB` counts its coverage runs.** For a body frame the count is `edgeCountA +
  edgeCountB`; for a head it is `edgeCountB` alone, in all 1,440 head frames of the game. Reading a head
  with the body's rule overshoots, the frame does not walk, and the slot is rejected — which is what
  happened when this was first looked at, and why the conclusion then was that no assignment of data
  bases worked. One slot per row was right; the coverage rule was the missing half.
- The runs are in the **same canvas** as the body, so the patch needs no offset of its own: head 0's
  runs span y 32-147 inside the body's y 32-449, which is where the head is.

`edgeCountA` is then unexplained for a head. It is not the kind-0 run count and it is not constant — it
ranges over 25-39 across the game. Nothing reads it.

##### What this adds

912 walk frames and 1,440 head frames, 2,352 in all, that were a hex dump before. The heads are the
lip-sync in visible form: frames 0-5 are the six viseme mouth shapes of `RESOURCE.004` (§5) in the same
0-5 encoding, and 6-11 repeat them with the eyes closed for a blink.

#### Known unknowns

- **The split of `edgeCountA` / `edgeCountB`.** Only their *sum* is established; it is the coverage
  stream's run count exactly in all 2,166 frames, but it is not the split by run kind. Frame 0 of entry 0
  has 908 kind-0 and 1,459 kind-1 runs against the record's 288 and 2,079. The decoder needs only the
  sum, so this costs nothing.
- **The darker colour table** the shadow is drawn through has not been found in *Runaway 1*; the decoder
  substitutes a flat `ShadowAlpha` of 128.
- **What `edgeCountA` means in a head record** (above). Nothing reads it.
- **The scale factor.** The characters are authored at one size and the runtime resamples them; the
  factor is in the executable, not in any container ([executable.md](executable.md)).

#### Decoders & tests

`CharacterSpriteArchive` reads the table — the exact tiling is the format test — and
`CharacterSpriteAsset.Parse` walks the frames, returning `null` unless every frame's two streams tile it
exactly. It implements `IAnimationAsset`, so the animation viewer and `ApngWriter` serve it and the scene
sprite codec through the same interface. Palettes come from `IndexedPalette.TryParseRgb565`, resolved per
entry by `VirtualFileSystem.CharacterPaletteFor`; the library is its own `Characters` folder in the tree.
Tests are in `Runaway1Tests.cs`, synthetic builders in `SyntheticAssets.cs`, and four real-install tests
re-verify the container, the frames, the palette locations and the block partition against the shipped
game.

The player character's animations go through the same decoder by a second `CharacterSpriteAsset.Parse`
overload that takes the records and the runs separately, with a `CharacterSpriteGeometry` saying whether
a record describes its own frame or the body underneath. `ActorArchive` owns the slot arithmetic: an
animation is named by the slot its runs are in, and its group, table slot, row, frame count, palette and
geometry all follow from that, so the tree carries the slot alone and the loader recovers the rest. A
group is taken only when all seventeen of its slots are there at the sizes the layout requires.

The explorer surfaces these animations in two places:
1. **Under `Characters > Actor`**: 84 named animations grouped into the 6 costume groups with facing names
   and head indices.
2. **Under `Global Data > RESOURCE.000`**: selecting any of the 84 raw run slots (`e012`–`e019`, `e020`–`e025`,
   `e029`–`e036`, etc.) decodes and plays the animation directly with its matching palette and frame table,
   with playback controls, scrubbing, zoom/pan, and APNG / sprite sheet export.

Because `ResourceLoader` resolves the actor frame table and palette on demand, the underlying nodes keep their
raw `GlobalData` kind in the virtual filesystem tree, leaving `ScanCache` and shipped cache hashes untouched.
`Runaway1ActorTests` covers the layout, both geometries and the shipped game, where it walks all 2,352 frames.

### 3. `RESOURCE.003` — dialogue phrase tables

The name covers two unrelated layouts. *Runaway 2* and *3* store a flat list of phrase records, which the
explorer decodes; *Runaway 1* stores a scene-indexed table that it does not.

#### Runaway 2 and Runaway 3 — phrase records

```text
u32                     PhraseCount     -- 10,454 in Runaway 2; 7,228 in Runaway 3
u32[PhraseCount]        PhraseIds       -- e.g. 100, 110, 120 ... 90230000
byte[PhraseCount][401]  Records         -- XOR-chained, NUL-terminated, Windows-1252
```

Each record is exactly 401 bytes and the text starts at byte 0, recovered with an index-seeded XOR chain:

```text
seed     = recordIndex & 0xff
plain[0] = raw[0] ^ seed
plain[i] = raw[i] ^ raw[i - 1]
```

Phrase index `k` pairs 1:1 with voice clip `k` in `Dataa/Dataaa.000`, so no lookup table is needed.
Read by `PhraseArchive`.

#### Runaway 1 — scene-indexed script and voice cues (**verified**)

*Runaway 1* uses the container shape its engine inherited from *Hollywood Monsters*: a 321-byte leading key block,
then 1,021 scene offsets, followed by per-scene 5-byte cue records and enciphered CP850 text rows.

```text
0x000: byte[321]  DecodeKey       -- subtractive key (identical to large row width)
0x141: u32[1021]  SceneOffsets    -- indexed by scene number / 10 (e.g. 100 for RESOURCE.A00)
...
Per-scene block:
    byte[0x186a0] SpeechCueDescriptors -- 20,000 x 5-byte cue records
    u8            SmallRowCount        -- 0 in Runaway 1 (hotspot labels moved to executable)
    u16           LargeRowCount        -- number of spoken dialogue lines
    byte[LargeRowCount][321] LargeRows -- 321-byte rows enciphered with DecodeKey, in CP850
```

Across the game, this yields **54 stages and 5,125 spoken dialogue lines**. Each stage index $s$ maps
directly to its scene archive:
- Chapter 1 (A): Stage 100 $\to$ `RESOURCE.A00` (Intro, Gina's hospital room)
- Chapter 2 (B): Stages 201..208 $\to$ `RESOURCE.B01`..`B08` (Chicago museum)
- Chapter 3 & 4 (D): Stages 401..414 $\to$ `RESOURCE.D01`..`D14` (Desert & Douglasville)
- Chapter 5 (E): Stages 502..517 $\to$ `RESOURCE.E02`..`E17` (Hopi village)
- Chapter 6 (F): Stages 608..642 $\to$ `RESOURCE.F08`..`F42`
- Final areas (G, H): Stages 701..705 and 801..802 $\to$ `RESOURCE.G*` and `RESOURCE.H*`

##### Speech cue continuation and voice pairing

Unlike *Hollywood Monsters* (which addressed cues as a grid with one utterance per row at frame 0), *Runaway 1*
packs sequential cue utterances across columns:
- A cue record is `{ u16 TextRecordId, u8 ContinuationCount, u16 VoiceSampleId }`.
- Consecutive records within a row are read until terminated by `TextRecordId == 0` or `0xFFFF`.
- `ContinuationCount` expands to consecutive dialogue lines: `ContinuationCount` sequential line IDs
  (`TextRecordId + k`) pair 1:1 with consecutive voice samples (`VoiceSampleId + k`).
- This maps **4,635 out of 5,125 spoken lines with zero conflicts**, spanning clips from 467 (*"¡Hola!"*) to
  5591 (the very last active clip in `VoiceArchive`).
- In the explorer, `VirtualFileSystem` uses this mapping to populate subtitle strings and previews for voice
  clips in the `Voice` tree and organizes `RESOURCE.003` under the `Dialogue` category.

### 4. `RESOURCE.003` in *Hollywood Monsters* — script text

The same container holds *Hollywood Monsters*' entire script: every line of narration and dialogue, plus
the inventory item names. The text is **obfuscated**, which is why a raw dump shows nothing readable.

#### Obfuscation

Each row is enciphered with a per-column subtractive key, and the key is simply **the first 321 bytes of
the file itself**:

```text
plain[column] = (cipher[column] - key[column]) & 0xff      key = file[0 .. 0x140]
```

The key length equals the large-row length (`0x141` = 321), so column `c` of every row always uses key
byte `c`. There is no per-row salt and nothing depends on position in the file.

#### Layout

```text
0x000:  byte[0x141]  DecodeKey        -- also the cipher key (see above)
0x141:  u32[1021]    StageOffsets     -- indexed by StageIndex; 0 means "no such stage"

Per stage, at StageOffsets[StageIndex]:
    byte[0x186a0] SpeechCueDescriptors -- 20,000 x 5-byte cue records
    u8            SmallRowCount
    u16           LargeRowCount
    byte[SmallRowCount][0x29]  SmallRows   -- 41-byte rows, enciphered
    byte[LargeRowCount][0x141] LargeRows   -- 321-byte rows, enciphered
```

`StageIndex` is the **scene number divided by 10**, so scene 1010 reads index 101 and scene 2010 reads
index 201. Indexing by anything else lands on the zeros that fill most of the table -- which is the trap
that makes the file look empty. Index `0x32` (50) holds the inventory-owner rows shared by every scene.

Rows are NUL-terminated within their fixed width:

- **Small rows (41 bytes)** are inventory item names and dialogue-menu labels, each stored with a leading
  space (`" escaleras"`, `" caseta de perro"`, `" coche"`).
- **Large rows (321 bytes)** are spoken lines (`"Conducen a la mansión."`).

A cue's `textRecordId` selects between the two tables: ids below 500 index the shared inventory-owner
rows, and ids from 500 up index the stage's own large rows at `id - 500`.

#### Speech cues

A cue record is 5 bytes, and the tables are addressed as fixed-width grids rather than lists:

```text
{
    u16 TextRecordId            -- 0 means "no cue"
    u8  ContinuationCount       -- further rows belonging to the same line
    u16 VoiceSampleId           -- slot in RESOURCE.004, the voice bank
}
```

Stage cues are indexed `(rowIndex * 100 + frameIndex) * 5`, static speech cues `(rowIndex * 10 +
frameIndex) * 5`. `VoiceSampleId` is what ties a written line to its recording.

**Only frame 0 of a row carries a usable pairing.** A row is one utterance and its later frames are
continuations, so the grid is densely populated with records that name a line without being the cue that
plays it. Reading all 20,000 records per stage raises apparent coverage from 42% to 92% of lines, but at
that point two thirds of the lines are ambiguous: 197,660 records disagree with an earlier record about
the same line, and no majority breaks the tie -- every candidate is attested exactly once. At frame 0 the
mapping is near-unique instead. Of the 2,369 distinct line ids cued there in the shipped game only 196
carry more than one voice id, and ids advance in lockstep with the recordings:

```text
stage 101  line 500 -> clip 2227     stage 201  line 500 -> clip 1805
           line 501 -> clip 2228                line 502 -> clip 1807
           line 502 -> clip 2229                line 504 -> clip 1809
```

The off-diagonal candidate for stage 101's line 501 is clip 1567, which also turns up as stage 201's line
501 -- the same stale value in two unrelated scenes, which is what gives the noise away. The explorer
therefore reads frame 0 only and leaves a little over half the lines with no clip: an unlinked line is
obvious to the reader, a line linked to the wrong recording is not.

#### Text encoding

Bytes are **CP850/CP437**, not Windows-1252: `0x82` is `é`, `0xa0` `á`, `0xa1` `í`, `0xa2` `ó` and `0xad` the
opening `¡`. Decoding as CP1252 turns every accent into punctuation. The font's own character map, in
`RESOURCE.000` entry `0xb0`, maps these bytes to glyph indices.

Verified by decoding the shipped Spanish first edition: stage 101 yields *"Bien, ya estamos en la mansión
Hannover. Veamos qué puedo averiguar."*, stage 201 *"¡Vaya choza!..."*.

#### Which slots are real

The offset table is mostly zeros and stale values, so a stage counts only when its rows actually decipher
to text. Judge that on the **large rows alone**: a stage may carry no labels at all (scene 7020, 12 lines
and no labels) or lead with an empty one (scene 1070), and gating on the labels rejects both even though
their dialogue decodes cleanly.

That rule yields 80 stages from the Spanish first edition -- 589 labels and 3,430 lines. Cross-checked
against the scene list in the ScummVM `hollywood` engine, the 74 playable ones account for every playable
scene it knows of except 2060, 3110 and 5130, whose offset slots are zero; the remaining 6 are cutscene
stages (9010, 9100-9130, 9200), which are likewise scenes in its registry. Nothing decodes that is not a
scene, and nothing is missing that is not absent from the file.

The game executable references the string `"PHRASE NOT FOUND"` when attempting to read an empty marker.

### 5. `RESOURCE.004` — lip-sync visemes

Encodes mouth posture keyframes synchronized with spoken voice lines.

#### Layout

```text
0x0000: { u32 Offset, u16 Size }[6000]  -- 36,000-byte index table
0x8CA0: <viseme stream data>
```

Each entry is a stream of bytes with values from `0` to `5`, corresponding to six phoneme mouth shapes (closed, slightly open, wide, round, teeth, open wide). Clip `k` pairs with dialogue audio clip `k`.

### 6. Character sprites and player costumes in *Runaway 2*

#### Chapter character sprite libraries (`RESOURCE.SP1`–`RESOURCE.SP5`)

In *Runaway 2*, character animations are distributed across chapter archives named `RESOURCE.SP1` through
`RESOURCE.SP5` ("SP" = Sprites, Chapters 1 through 5).

- **546 animations across 1,092 paired entries**:
  - `RESOURCE.SP1`: 84 animations (168 entries)
  - `RESOURCE.SP2`: 126 animations (252 entries)
  - `RESOURCE.SP3`: 168 animations (336 entries)
  - `RESOURCE.SP4`: 84 animations (168 entries)
  - `RESOURCE.SP5`: 84 animations (168 entries)
- **Container structure**:
  - Entry $2k$: 3,200-byte frame table (`CharacterFrameRecord[100]`, exactly 100 frames $\times$ 32 bytes).
  - Entry $2k+1$: Stream payload for both body runs and coverage runs.
- **Pixel codec**:
  - **Direct 16-bit RGB565 pixels** (`bytesPerPixel = 2`): Runaway 1 used 8-bit palette indices referencing
    external 512-byte palettes in `RESOURCE.000`. Runaway 2 eliminates the palette dependency by encoding
    little-endian `ushort` RGB565 pixels directly in the body runs.
  - **Coverage stream**: Uses the identical run format and alpha blending behavior as *Runaway 1*.
  - Every animation tiles byte-exact across all five archives with zero leftover bytes.

#### Player character costumes in `RESOURCE.000` (slots 168–263)

The player character (Brian Bradley) in *Runaway 2* has costumes stored in `RESOURCE.000` across slots 168 to 263.

- **48 animations across 96 paired slots**: 6 costume sets $\times$ 8 animations per set (one per walking/standing direction).
- **Format**:
  - Slot $i$: Header and frame record table (`2 + FrameCount * 14` bytes).
  - Slot $i+1$: Segment data stream using Runaway 2's 6-byte segment headers (`u16 X, u16 Y, u8 Flag, u8 Count`).
- When paired, all 48 animations walk and tile byte-exact with valid non-empty frames.

### 7. *Yesterday* 24-bit outlined fonts (`RESOURCE.FNT`) and cutscene subtitles (`Datav/*.001`)

#### `RESOURCE.FNT` (784,141 bytes)

Unlike earlier games that used 8-bit or 16-bit packed fonts in `RESOURCE.000`, *Yesterday* packs its localized font atlases into `RESOURCE.FNT`:

```text
Header (36 bytes):
  u32 Magic                   -- 0x00000000
  u32 Font0GlyphCount         -- 196
  u32 Font0GlyphTableOffset   -- 36
  u32 Font0BitmapOffset       -- 1,408
  u32 Font0BitmapSize         -- 358,071 (119,357 px * 3)
  u32 Font1GlyphCount         -- 191
  u32 Font1GlyphTableOffset   -- 359,479
  u32 Font1BitmapOffset       -- 360,816
  u32 Font1BitmapSize         -- 423,325 (141,108 px * 3)
```

- **Glyph table records**: Uses the 7-byte record layout (`u16 Offset, u16 Top, u16 Height, u8 Width`) also seen in *Runaway 2* and *The Next BIG Thing*.
- **Bitmap encoding**: 3 bytes per pixel (24-bit RGB):
  - Channels 0 and 2 are identical multiples of 17 (0 to 15 expanded to 0..255) defining outline opacity.
  - Channel 1 defines glyph body fill opacity.
  - Decoded into clean BGRA32 rasters with black outlines and white text fill (`FontDecoder.DecodeRgb24`).

#### Cutscene subtitles (`Datav/*.001`)

In *Yesterday*, video files retain `.000` extensions, while accompanying `.001` files are plain-text timed subtitle scripts:
- Lines starting with `//` specify dialogue text.
- Positioning and timing lines: `X <pos>`, `Y <pos>`, `B <begin_frame>`, `E <end_frame>`.

### 8. *Runaway 3* Chapter Interaction & Action Database (`RESOURCE.005`)

`RESOURCE.005` (478,152 bytes) is a compiled interaction database covering all 7 chapters of *Runaway 3*:

```text
Header:
  u32 TotalRecords            -- 2,173
  u32 ChapterCount            -- 7
  ChapterEntry[7]             -- 12 bytes each: { u32 ChapterId, u32 RecordCount, u32 Offset }
Records:
  Record[2,173]               -- 220 bytes each:
                                  u32 ChapterId
                                  u32 SceneId
                                  u32 ObjectId
                                  u32 ActionId
                                  u8  Payload[204]
```

Total size: $8 + 7 \times 12 + 2,173 \times 220 = 478,152$ bytes.

#### Payload XOR-Chain Decryption & Object Labels

The 204-byte payload of each record is enciphered with the same XOR-chain algorithm used by `PhraseArchive` (`RESOURCE.003`), keyed on the chapter-relative record index `r & 0xFF`:

```csharp
plain[0] = (byte)(raw[0] ^ (recordInChapter & 0xFF));
for (int j = 1; j < 204; j++)
    plain[j] = (byte)(raw[j] ^ raw[j - 1]);
```

The decrypted buffer begins with a null-terminated Spanish string naming the object or interaction target (e.g. `URNA`, `GUANTE`, `LINTERNA`, `CESTA DE LA COLECTA`, `RADIAL`, `ENFRIAR @`, `CORTAR CABLE PARABIOTÉRMICO`), followed by state trigger flags and parameter bytes.

#### Verb Actions & Scene Trigger Graph

The engine's action IDs map directly to canonical adventure game verbs:
- `0`: Examine / Look (*Mirar*)
- `1`: Take / Pick up (*Coger*)
- `2`: Use / Interact (*Usar*)
- `3`: Talk To (*Hablar con*)
- `4`: Go To (*Ir a*)
- `5`: Drop / Leave (*Dejar*)
- `6`: Show Inventory (*Mostrar inventario*)
- `20`: Combine / Use With (*Usar con*)

`ChapterInteractionDatabase` structures these records into an interactive tree grouped hierarchically by **Chapter → Scene → Object → Action**, providing formatted ASCII interaction graphs and exportable Mermaid flowcharts (`flowchart TD`).

## Decoders

- [`CharacterSpriteAsset`](../../src/PenduloStudiosExplorer.Core/Formats/CharacterSpriteDecoder.cs)
- [`RouteDecoder`](../../src/PenduloStudiosExplorer.Core/Formats/RouteDecoder.cs)
- [`YesterdayFontArchive`](../../src/PenduloStudiosExplorer.Core/Formats/YesterdayFontArchive.cs)
- [`CutsceneSubtitles`](../../src/PenduloStudiosExplorer.Core/Formats/CutsceneSubtitles.cs)
- [`ChapterInteractionArchive`](../../src/PenduloStudiosExplorer.Core/FileSystem/ChapterInteractionArchive.cs)
- [`PhraseArchive`](../../src/PenduloStudiosExplorer.Core/FileSystem/PhraseArchive.cs)
- [`VisemeArchive`](../../src/PenduloStudiosExplorer.Core/FileSystem/Archives.cs)
- [`GlobalArchive`](../../src/PenduloStudiosExplorer.Core/FileSystem/Archives.cs)
- Tests: [`ContainerTests`](../../tests/PenduloStudiosExplorer.Core.Tests/ContainerTests.cs), [`Runaway1Tests`](../../tests/PenduloStudiosExplorer.Core.Tests/Runaway1Tests.cs), [`Runaway2ContainerTests`](../../tests/PenduloStudiosExplorer.Core.Tests/Runaway2Tests.cs), [`Runaway3Tests`](../../tests/PenduloStudiosExplorer.Core.Tests/Runaway3Tests.cs), [`YesterdayTests`](../../tests/PenduloStudiosExplorer.Core.Tests/YesterdayTests.cs), [`PhraseArchiveTests`](../../tests/PenduloStudiosExplorer.Core.Tests/PhraseArchiveTests.cs), [`HollywoodMonstersTests`](../../tests/PenduloStudiosExplorer.Core.Tests/HollywoodMonstersTests.cs)
