# Full-screen rasters and backgrounds

The game engine displays backgrounds for rooms, exterior vistas, panoramic scrollers, and cutscenes using either uncompressed RGB565 bitmaps or baseline JPEG images.

## Purpose

Entry 0 (`e00`) in almost every scene archive holds the room's background image. In *Runaway 1*, *Runaway 2*, and *Runaway 3*, these are stored as raw RGB565 rasters without headers. In *The Next BIG Thing* and *Yesterday*, backgrounds are stored as 1080p JPEG images.

## Layouts

### 1. Raw RGB565 rasters (*Runaway 1–3*)

Raw rasters contain no header, no magic number, and no dimension metadata:

```text
Payload:
    u16 Pixel[Width * Height]   -- Little-endian RGB565 pixels, row-major
```

Total byte size is exactly:

```text
byte_size = Width * Height * 2
```

Pixel components in 16-bit RGB565:

```text
Bits:
    15 14 13 12 11   10 09 08 07 06 05   04 03 02 01 00
    [   Red (5)  ]   [   Green (6)   ]   [  Blue (5)  ]
```

Conversion to 8-bit BGRA:

```csharp
byte r = (byte)((pixel >> 11) << 3);
byte g = (byte)(((pixel >> 5) & 0x3F) << 2);
byte b = (byte)((pixel & 0x1F) << 3);
byte a = 255;
```

#### Width recovery via stride sweep

Because width is not stored, the decoder determines the correct image width by measuring vertical autocorrelation on the green channel across candidate strides. The green channel has 6 bits of precision and exhibits the strongest spatial coherence in natural art.

For each candidate stride $W$ that divides the total pixel count:

$$\text{Score}(W) = \frac{1}{N} \sum_{i} |G[i] - G[i + W]|$$

The true stride produces a sharp global minimum in the vertical difference metric. The search performs:
1. **Coarse pass**: Checks candidate strides across three evenly distributed sample chunks.
2. **Fine re-score**: Validates candidate strides against edge artifacts, ensuring exact division of the pixel buffer.

Common scene dimensions across the games:
- **Standard**: 1024×600 (*Runaway 1*), 1280×720 (*Runaway 3*), 1920×1080 (*TNBT*, *Yesterday*).
- **Horizontal scrollers**: 1372×600, 1600×600, 2048×600, 2592×600, 3016×770.
- **Non-standard heights (*Runaway 1*)**: 1200×670 (D03), 1760×1200 (D06), 1880×700 (E10), 1024×700 (F27), 1400×760 (G01), 1640×800 (G02), 1520×900 (G03). See [executable.md](executable.md) §10.6.
- **Tall / vertical scrollers**: 1024×2062 (credits / vertical towers).
- **Insets / cutaways**: 1444×800, 282×188, 204×120.

#### Museum restoration lab monitor feeds (*Runaway 1* `RESOURCE.B01`)

In *Runaway 1* Chapter 2 ("Museum: Restoration Lab", `RESOURCE.B01`), entries `e10` through `e25` are 16 identical-sized rasters of 106,032 bytes:
- **Dimensions**: exactly **282 × 188** pixels ($282 \times 188 \times 2 = 106,032$ bytes).
- **Purpose**: Security camera feeds, forensic x-ray scans, and CRT monitor displays used during the Mayan crucifix restoration minigame (mobster surveillance, dinosaur skeleton corridor camera, laser scanner view, etc.).
- **Padding**: Feeds feature 13 rows of black pixel padding at the top (accounting for monitor bezel offset) resulting in lower edge-to-edge sharpness (`~2.43`), identified deterministically by exact dimension match `282 * 188`.

### 2. Indexed rasters (*Hollywood Monsters*)

Backgrounds in *Hollywood Monsters* are 8-bit palette-indexed bitmaps. Nothing in the entry records the
geometry, so the byte count alone identifies one:

- Dimensions: exactly **1024 × 480** pixels (`RasterDecoder.IndexedScreenWidth` × `IndexedScreenHeight`).
  The game's display is only **640 × 480**: a background is a wide backdrop that the engine scrolls
  horizontally behind a 640-pixel window, which is why sprite coordinates routinely exceed 640.
- Byte size: exactly 491,520 bytes (`1024 * 480 * 1`), or a whole multiple of it for the taller scrolling screens.
- Each byte is an index (0–255) into the scene's colour table; see [palettes.md](palettes.md).

#### Secondary indexed rasters and interface panels

Beyond the standard 1024×480 screen, *Hollywood Monsters* uses exact fixed-size indexed rasters for UI and travel:
- **Travel destination tiles (`RESOURCE.I04` chunks 2–6)**: 5 thumbnails of **200 × 93** pixels ($200 \times 93 = 18,600$ bytes) displayed on the world map for airport and travel destinations.
- **Bottom action panel (`RESOURCE.000` entry 47)**: **1024 × 189** pixels (193,536 bytes) containing the verbs grid, inventory bag arrows, and action bar canvas ($1024 \times \text{0xbd}$).
- **Dialogue menu panel (`RESOURCE.000` entry 48)**: **640 × 45** pixels (28,800 bytes) containing the 3-section conversation options frame ($12 + 21 + 12$ rows $\times 640$).

### 3. JPEG backgrounds (*The Next BIG Thing*, *Yesterday*)

Starting with *The Next BIG Thing*, high-definition 1920×1080 painted backgrounds are stored as standard baseline JPEG images directly within the scene archive.

#### Layout

```text
0x00: 0xFF 0xD8                 -- Start of Image (SOI)
0x02: 0xFF 0xE0                 -- JFIF APP0 marker (or standard EXIF/JPEG segments)
...
SOF0 (0xFF 0xC0 / 0xC2):
    u16 Length
    u8  Precision
    u16 Height                  -- 1080
    u16 Width                   -- 1920
    u8  Components              -- 3 (YCbCr)
...
0xFF 0xD9                       -- End of Image (EOI)
```

The decoder inspects the first two bytes for `0xFF 0xD8` and retrieves width and height directly from the Start of Frame (SOF) marker before decoding to a 32-bit BGRA buffer.

## Known unknowns

- A small number of medium-sized rasters (e.g. 1444×800) have no matching camera views in game scripts.

## Decoders

- [`RasterDecoder`](../../src/RunawayExplorer.Core/Formats/RasterDecoder.cs)
- [`JpegDecoder`](../../src/RunawayExplorer.Core/Formats/JpegDecoder.cs)
- Tests: [`ImageDecoderTests`](../../tests/RunawayExplorer.Core.Tests/ImageDecoderTests.cs), [`HollywoodMonstersTests`](../../tests/RunawayExplorer.Core.Tests/HollywoodMonstersTests.cs), [`TheNextBigThingTests`](../../tests/RunawayExplorer.Core.Tests/TheNextBigThingTests.cs), [`YesterdayTests`](../../tests/RunawayExplorer.Core.Tests/YesterdayTests.cs)
