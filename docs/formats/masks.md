# Scene masks and walkboxes

Scene masks partition backgrounds into interactive zones, walkboxes (character pathfinding meshes), depth planes (walk-behind occluders), and material/acoustics boundaries.

## Purpose

Entry 1 (`e01`) and occasional secondary entries define spatial properties for scenes. Rather than testing polygon geometry, the engine samples these pixel maps at runtime to determine:
- If a mouse click hit a clickable item/hotspot (`Hotspot` layer).
- If a character can walk on a specific floor point (`Walk` layer).
- Which foreground elements occlude a character (`Depth` / `Occluder` layer).
- What footsteps sound to trigger (`Material` layer).

## Encodings

### 1. 3-byte continuous RLE (*Hollywood Monsters*, *Runaway 1–3*)

A flat stream of 3-byte run-length encoded records:

```text
N × {
    u8  Id                      -- zone identifier (0–255)
    u16 Length                  -- run length in pixels (little-endian)
}
```

#### Scanline bounding rule, and where it does not hold

Runs are often **scanline-bounded**: the sum of run lengths in any row equals the scene width
(e.g. 1024, 1280 or 1380) with zero remainder, and no run wraps across a scanline.

*Runaway 1* mixes both encodings, freely, within the same game. Of its 58 scene masks, **36 are
scanline-bounded and 22 are continuous** -- their runs wrap onto the next row and only the grand
total is meaningful. In all cases, the pixel total matches the scene's configured dimensions
(`width * height`). Note that several scenes have heights other than 600 px configured by the
executable's scene loader (`[0x7fc56e]`) and must be decoded using their scene-specific geometry:
- `RESOURCE.D03`: $1200 \times 670$ (804,000 px)
- `RESOURCE.D06`: $1760 \times 1200$ (2,112,000 px)
- `RESOURCE.E10`: $1880 \times 700$ (1,316,000 px)
- `RESOURCE.F27`: $1024 \times 700$ (716,800 px; loader sets `[0x7fc56e] = 700` (`0x2bc`), which explains why its 769 runs total exactly 716,800 px despite the shipped entry 0 background raster being 1024x600)
- `RESOURCE.G01`: $1400 \times 760$ (1,064,000 px)
- `RESOURCE.G02`: $1640 \times 800$ (1,312,000 px)
- `RESOURCE.G03`: $1520 \times 900$ (1,368,000 px)

So the scanline rule cannot be used to *recognise* a *Runaway 1* mask; it misses more than a third of them.

#### A mask may paint the whole screen as one zone

`RESOURCE.D07` and `RESOURCE.E13` are ten runs of a single id totalling exactly 1024x600. They are
legal masks -- a scene the actor cannot walk in and nothing in it to click -- but they satisfy
neither the scanline rule nor a "at least two zones" sanity check, so a decoder that applies either
as a *recognition* test reads them as anonymous data and the scene loses its mask entirely. Both
were read that way here until the total-pixel rule below replaced the guard. A zone count is a
property of a mask, never a test for one.

Two practical consequences:

- **Decode continuously always.** Fill the id map in reading order and let runs wrap. A mask that
  does respect scanlines decodes identically that way, because its runs land on the row
  boundaries of their own accord. There is no need to detect which kind you have.
- **Detect by the pixel total.** The test that holds for every mask in the game is that the run
  lengths sum to exactly `width * height`, with no zero-length run. That is also enough to tell a
  mask from the sprite or raster that sits in the same slot in the archives that carry no mask.

The mask, where a scene has one, is entry 1 and the attribute table entry 2, in every *Runaway 1*
archive that has them. 18 archives have neither -- their entry 1 is a sprite -- and those are the
cutscene and close-up archives.

#### Ambiguity with palette blocks (*Hollywood Monsters*)

A *Hollywood Monsters* palette block is a whole number of 3-byte records too, and its bytes can satisfy the
scanline rule, so the classifier tests for a palette block ([palettes.md](palettes.md)) **before** it tests
for a mask. With that order reversed, a scene's colour table is read as a garbage mask.

#### What the ids mean (*Hollywood Monsters*)

In *Hollywood Monsters* the run stream is not a mask per zone type but a single **region map**: entry
`e02` expands to one byte for every pixel of the 1024×480 screen, and that byte is an index into seven
256-entry lookup tables packed into entry `e03` (1,792 bytes, `7 × 256`):

| Offset | Page | Meaning of `table[id]` |
|---|---|---|
| `0x000` | Region map | Walkable-region number; a value above the scene's maximum is not walkable |
| `0x100` | Colour -> item | Scene item (hotspot) index under the cursor, 0 for none |
| `0x200` | Colour -> actor depth class | Whether the actor draws in front of or behind scenery there |
| `0x300` | Colour -> palette delta class | Selects a brightness delta for the actor's colours |
| `0x400` | Colour -> palette adjustment class | Selects a recolouring set for the actor's colours |
| `0x500` | Colour -> footstep sound | Surface material under the actor's feet |
| `0x600` | Presentation palette remap | Index remap applied when a presentation replaces the scene |

So one map serves hotspots, walkability, depth sorting, footstep audio and the actor's lighting at once;
the layers the explorer lets you filter are views of the same pixels through different pages. This also
explains why the run values cluster in a narrow range: they are region ids, not colour indices, and they
are unrelated to the scene palette.

The 1,536-byte attribute table described below is the *Runaway 1*/*2* equivalent of these pages.

### 2. 4-byte RLE (*Yesterday*)

In *Yesterday*, high-resolution 1920×1080 masks use 4-byte records:

```text
N × {
    u16 Id                      -- 16-bit zone identifier
    u16 Length                  -- run length in pixels (summing to 1920 per row)
}
```

### 3. Sparse masks (*Runaway 3*)

Used for intricate depth occluders and antialiased silhouette edges:

```text
Header:
    u16 RunCount                -- number of span records

Runs (RunCount records):
    u16 X                       -- screen column
    u16 Y                       -- screen row
    u8  Flag                    -- 2 = solid run, 4 = antialiased run
    u16 Count                   -- number of pixels in the span
    [u8 Alpha[Count] if Flag == 4]  -- per-pixel alpha bytes for antialiased edges
```

- **Flag 2**: Solid run of `Count` pixels with `Alpha = 255`.
- **Flag 4**: Variable transparency run where each pixel has an explicit 8-bit alpha/weight value.

### 4. Grayscale PNG masks (*The Next BIG Thing*, *Yesterday*)

In *Yesterday*, several scene archives (such as `RESOURCE.C02\e04`, `RESOURCE.D02\e04-06`) store mask layers as standard 8-bit or 24-bit PNG images. The grayscale luminance value of each pixel corresponds directly to the functional zone identifier.

## Zone attribute table (`1536 bytes`)

In *Runaway 1* and *2*, scene archives contain a recurring 1,536-byte data entry: entry 2, beside
the mask at entry 1. It is **six 256-byte pages**, one page per attribute, indexed

```text
table[page * 256 + id]        -- page 0..5, id 0..255
```

which is the same shape as the *Hollywood Monsters* seven-page table above, one page shorter.
`RleMaskDecoder` reads it this way.

> This section previously described the table as 256 records of 6 bytes (`{u8 WalkFlag, u8
> HotspotFlag, u8 DepthPlane, u8 MaterialId, u16 ScriptAction}`), which is wrong, and disagreed
> with the decoder beside it. Pooling all 114 tables in *Runaway 1* settles it: read as pages the
> fields come out tightly bounded (maxima 19, 28, 9, 9, 255 and 7), read as records all six smear
> across the full byte range, which is what slicing across a structure looks like.

Five of the six are established, every one of them by rendering a scene flat by the page and
comparing it against the painting -- see the note below the table on why the statistics found
none of them. Figures are pooled over the 55 *Runaway 1* scenes that carry both a mask and a
table, counting only the ids a scene's mask actually paints:

| Page | Meaning | Evidence |
|---|---|---|
| 0 | **Walkable region**, 0 = not walkable | Set on 45% of a scene's ids, ~4 regions per scene. Tinting lights the ground and nothing else. It numbers the regions; it does **not** sort them front to back -- see the note below the table. |
| 1 | **Scene item / hotspot index**, 0 = nothing | Set on 54% of ids, ~10 items per scene, and groups of zone ids collapse onto one item number, which is how one object owns several zones. In F13 it lights exactly the door, the well, the skull on the stick and the exit strip; probing the picture returns item 1 for the exit, 2 for the door, 3 for the well, 5 for the skull. The resulting 1-based index maps directly to the hotspot definitions in the executable, where hotspot names are stored in a contiguous array at `0x92af91` with a stride of 81 bytes per hotspot (see [executable.md](executable.md) §10.5). |
| 2 | **Depth plane**, larger = nearer, 0 = furthest | The sparse page: set on only 19% of a scene's ids, ~3 values. An actor takes the value of the zone under its feet, and every pixel with a greater value is drawn over it. In F13 the page marks exactly the totem pole (2), the skull on the stick (2) and the well with its frame and bucket (1) -- the three walk-behind objects -- and nothing else. In G04 it marks the near rock wall at the cave mouth (1) and the foreground rock and bushes (2). |
| 3 | **Actor brightness class**, 0..6 | Dense (91% of ids) and unrelated to screen row, because it is a lighting *field* rather than an object map. In B02, the darkened hospital room, the whole scene is one value except a single quadrilateral lying exactly on the pool of moonlight the window throws on the floor. In E03 the page is concentric ellipses centred on the floor -- a lamp's falloff quantised into bands. Neither follows the scenery. The *Hollywood Monsters* page indexes signed deltas added to the actor's palette; *Runaway 1* is 16-bit colour, so how the class becomes a brightness is still open. |
| 4 | **Not a page. Never read it.** | The entry is 1,536 bytes for six pages but only five are written; this slot keeps whatever was in the exporter's buffer -- see the note below the table. |
| 5 | **Footstep surface material**, 0..7 | Dense (98% of ids) and constant across the whole scene in 40 of 56 scenes, which is what one floor material looks like. Where a scene has two, the boundary is the material's edge in the painting and not an object: in F18 the dirt street is 2 and the wooden boardwalk is 3, and the border traces the front edge of the planks. The per-scene sets group by location -- the desert scenes E02, E05, E07 and E08 all use 2 alone. In *Runaway* the exe reads it once (`0x431b49`): the value is a material 1-7 that picks one of two random variants from `RESOURCE.002` slots `19+2m` and `20+2m` (16 kHz mono s16), 0 is silent -- see [executable.md](executable.md) section 8. In *Hollywood Monsters* the same page indexes the scene's resident sound effects, played on the walk cycle's two footfall cels. |

#### Sparse pages classify, dense pages fill

Pages 0, 1 and 2 are sparse: they name what a zone *is*, and a zone can be in several of them at
once, which is what the exit above shows. Pages 3 and 5 are dense fields -- brightness is set on
91% of a scene's ids and footstep surface on 98% -- so they cover ground every other page covers
too. Treating those as memberships of the same kind would make almost every zone in the game a
multi-layer one, which is why `RleMaskDecoder` bands only the sparse three and lets the dense pages
colour a zone when nothing sparse claims it.

#### These pages do not rank with screen row

Rank correlation is the wrong tool for finding the depth page, and it is what kept page 2
unidentified. Page 2 is not a y-sort: an occluder's plane says which scenery an actor passes
behind, not how far down the picture it sits, so a pole at the back of the scene and a rock in
the foreground can share a value. Over the 114 tables the best page scores 59.6%, which is
noise. Rendering a scene tinted by the page settles it in one look.

The same mistake is why page 0 was described here as ranking with the zone's mean screen row
76% of the time. That figure counted the ids the page leaves at zero -- the sky and the walls,
which sit at the top of the picture, so they drag the correlation up for free. Restricted to
the ids page 0 actually sets, it is **40.3%**, i.e. slightly *inverted*. Any test of a page
has to exclude the ids the page does not set.

Page 4 was described here as using the whole byte range and repeating "with a period of four in
the zone id". The period claim was one scene read by eye and is wrong -- measured over used
ids it holds in 0 of 56 scenes. The range claim was right but means the opposite of what it
looked like.

#### Page 4 is not a page

The other five are zero for every id a scene never paints, and their last nonzero entry stops at
the scene's highest live id. Page 4 is nonzero on **64%** of the dead ids and runs to entry
251-255 in all 64 tables, whether the scene uses 1 id or 63. It is a function of no other page
(4-13% of scenes), correlates with nothing geometric (51.7% against screen row, i.e. noise), is
never constant, and read as 4-byte little-endian words it holds recognisable 32-bit Windows
addresses -- `0x0012Fxxx` stack, `0x004xxxxx` image, `0x77Dxxxxx` system DLL -- with 36% of its
nonzero words falling in those ranges.

So the exporter allocates 1,536 bytes for six pages and writes only five, leaving whatever was
in the buffer. The contents are per-file rather than random (the only duplicate page 4s across
the game are the mirrored chapter pairs `F13`/`h13`, `F18`/`h18`, `F26`/`H26`), which just means
it was written once at export time. The *Hollywood Monsters* page at the same offset is the
palette recolouring class, which a 16-bit-colour game has no use for, so the slot kept the
layout and was never filled in.

#### Where the apparent "period of four" came from

`Resource.e06`'s page 4 is the clearest case. Over the ids that scene paints it reads

```text
id   1    2    3    4    5    6    7    8
   249  113    3   68  249  113    3   74
```

which looks like a period of four until id 8 breaks it. Read as little-endian words the
sequence is not periodic at all -- it is two adjacent pointers that happen to share their top
three bytes:

```text
0x0371F900  0x0371F944  0x0040544A  0x00000000  0x033C20C0  0x0371F94C
```

Two stack addresses a few dozen bytes apart, an image address, a heap address. Any page-4 run
of near-identical dwords looks periodic when it is sliced back into bytes, which is why reading
the block per id ever suggested a structure.

#### Worked example: `Resource.e06\e01`

The scene is the Divas' bus in the desert, 1380x600. Its entry 1 is a textbook mask and its
entries 2 and 3 corroborate each other, so it is a good one to check a decoder against.

| | |
|---|---|
| Entry 1, the mask | 14,694 bytes = 4,898 runs exactly, totalling 828,000 px = 1380 x 600. Scanline-bounded in the strong sense: all 600 rows close on exactly 1380 and **no** run crosses a row boundary. 16 ids. |
| Entry 2, the attributes | ids 1-12 are not walkable, 13-16 are; ids 3-12 and 16 carry hotspots 1-9. |
| Entry 3, the routes | 43,659 bytes, three live regions. |

Three things this scene shows that the pooled figures above do not:

- **A walk region can be a corridor, not a floor.** Page 0 sets only ids 13-16, together
  10,882 px -- **1.31%** of the screen. They form a narrow diagonal track running from the
  folding chair at `x 349` down to the right-hand screen edge, while the wide open sand either
  side of it is id 1 and not walkable. "Tinting page 0 lights the ground the player can reach"
  is right, but the ground a player can reach is sometimes a path a hundred pixels wide.
- **Page 2 marks nothing, even with an obvious occluder on screen.** The bus fills the middle
  of the painting and page 2 is zero for every id the scene uses. The depth page is not a
  catalogue of foreground objects; it is written only where the walkable region actually passes
  behind one, and this track never does. Page 3 is likewise a flat 1 everywhere -- no lighting
  variation. A scene with a blank page 2 is not a broken table.
- **One id can be both a walk region and a hotspot.** Id 16, a 86 px patch at
  `(1330,577)-(1379,587)`, is walk region 1 *and* hotspot 1, sitting inside the 50 px-wide
  right-edge exit strip (id 3, also hotspot 1). The exit is the place the actor has to be able
  to stand on *and* click, so the two layers overlap there. Decoders that assume the layers
  partition the ids will mis-handle exits.

  This is not an oddity of this scene. Across the 64 *Runaway 1* tables, **54 scenes carry 948
  ids that are in two or three of the sparse layers** at once: 328 hotspot+depth (a clickable
  object that the actor also walks behind), 244 walk+depth, 208 walk+hotspot and 168 in all
  three. `RleMaskDecoder` used to give each id one colour by a fixed layer priority, so in any
  combined view every membership but the first was invisible -- the exit in this scene rendered
  purely as a hotspot, and the walkable track appeared to stop short of the way out. It now
  bands such a zone between its layers, so each one stays readable; `OverlappingIds` returns the
  set. Zones in a single layer are still painted flat, and a single-layer view is unchanged.

The route table cross-checks the mask exactly. Page 0 numbers the track `3, 2, 1` left to
right (ids 13, 14, 15, with 16 continuing region 1 into the exit), and entry 3 chains them:

```text
1 -> 2  hops [2]     crossing (777, 483)
2 -> 1  hops [1]     crossing (778, 484)
2 -> 3  hops [3]     crossing (607, 469)
3 -> 2  hops [2]     crossing (608, 470)
1 -> 3  hops [2, 3]  no crossings
3 -> 1  hops [2, 1]  no crossings
```

The id boundaries in the mask fall at `x 607|608` and `x 777|778`, so each crossing point sits
one pixel inside the destination region, and the two-hop pairs carry no points of their own -- both
exactly as the format expects. Unused point slots are padded with `(-1, -1)`, not zeroes.

To visualize the navigation mesh on top of the scene, `RleMaskDecoder.ComputeWalkZoneCentroids`
accumulates 2D center-of-mass moments $(\bar{x}, \bar{y})$ for each functional walk region in
$O(\text{runCount})$ time during mask decoding, which `RouteGraphRenderer` renders as circular zone
badges joined across the portal boundary seams.

#### Decoders & tests

`RleMaskDecoder.Detect` tries the scanline rule first, because it is the strongest test available
when nothing else is known about the entry, and falls back to `DetectByTotal` once the scene's own
size is in hand: run lengths all positive and summing to exactly that many pixels. That fallback is
what recognises the continuous masks and the two single-zone ones. Decoding is continuous in every
case -- a scanline-bounded mask lands on its row boundaries of its own accord. Synthetic tests are
in `RleMaskDecoderTests`, and `Runaway1SceneMaskTests` re-checks all 58 masks, the two single-zone
scenes and this worked example against the shipped game.

The earlier `DepthPlane` / `MaterialId` / `ScriptAction` labels came from the record-major
reading. Page 2 turns out to carry what `DepthPlane` claimed and page 5 roughly what
`MaterialId` claimed, but both were checked against the data before being named here;
`ScriptAction` was pointing at the unwritten slot.

The explorer reads this table to allow filtering masks by individual functional layers (`Walk`, `Hotspot`, `Depth`, `Light`, `Material`, `Occluder`). Page 4 is not offered as a layer, because it holds nothing.

## Palette generation

Because IDs are discrete integer indices, the explorer assigns distinct high-contrast colors using golden-angle hue distribution:

$$\text{Hue}(id) = (id \times 137.508^\circ) \bmod 360^\circ$$

Masks can be previewed standalone or composited at 55% opacity over the scene background art (`e00`).

## Decoders

- [`RleMaskDecoder`](../../src/RunawayExplorer.Core/Formats/RleMaskDecoder.cs)
- [`SparseMaskDecoder`](../../src/RunawayExplorer.Core/Formats/SparseMaskDecoder.cs)
- [`PngDecoder`](../../src/RunawayExplorer.Core/Formats/PngDecoder.cs)
- Tests: [`RleMaskDecoderTests`](../../tests/RunawayExplorer.Core.Tests/RleMaskDecoderTests.cs), [`SpanMaskDecoderTests`](../../tests/RunawayExplorer.Core.Tests/SpanMaskDecoderTests.cs), [`HollywoodMonstersTests`](../../tests/RunawayExplorer.Core.Tests/HollywoodMonstersTests.cs)
