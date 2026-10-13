using RunawayExplorer.Core.FileSystem;

namespace RunawayExplorer.Core.Formats;

/// <summary>Result of <see cref="RasterDecoder.Detect"/>: the geometry of a headerless raster, without decoding it.</summary>
public readonly record struct RasterInfo(int Width, int Height, double Sharpness, bool IsMask);

/// <summary>
/// Format 1 of the scene archives: a headerless block of exactly <c>W × H × 2</c> bytes of RGB565.
/// The width is not stored anywhere. It is recovered as the stride at which vertically adjacent pixels
/// agree best -- <c>score(W) = mean |g[i] − g[i+W]|</c> over the green channel has a razor-sharp minimum
/// at the true width -- and must then divide the pixel count exactly.
/// </summary>
public static class RasterDecoder
{
    public const int WidthMin = 100;
    public const int WidthMax = 4000;

    /// <summary>Entries smaller than this are never rasters (the smallest real one is 204×120).</summary>
    public const int MinBytes = 8_000;

    /// <summary>Hollywood Monsters' screen, in 8-bit indexed pixels. Every one of its backgrounds is exactly this.</summary>
    public const int IndexedScreenWidth = 1024;

    /// <inheritdoc cref="IndexedScreenWidth"/>
    public const int IndexedScreenHeight = 480;

    /// <summary>Pixels used for the coarse stride sweep. ~60 rows of a 1024-wide image is plenty for the minimum to stand out.</summary>
    private const int CoarseSamplePixels = 60_000;

    /// <summary>Pixels used to re-score the handful of candidate widths the coarse sweep surfaced.</summary>
    private const int FineSamplePixels = 400_000;

    /// <summary>sharpness = median(sweep) / best. Real images score 5–17; bordered rasters ~2.4; non-images ~1.1.</summary>
    public const double SharpnessMin = 2.4;

    /// <summary>
    /// Mask layers (walk-behind / hotspot indices) have index values in the pixel slots, so neighbouring
    /// pixels land in unrelated colours. Artwork scores 0–10 here, masks 77+.
    /// </summary>
    public const double MaskDisagreementMin = 60.0;

    /// <summary>
    /// Works out whether <paramref name="data"/> is a raster and, if so, its size. Cheap enough to run
    /// at scan time; nothing is decoded.
    /// </summary>
    public static RasterInfo? Detect(ReadOnlySpan<byte> data, GameVersion gameVersion = GameVersion.Runaway1)
    {
        if (data.Length < MinBytes || data.Length > 8_500_000 || data.Length % 2 != 0)
            return null;

        int total = data.Length / 2;

        (int screenW, int screenH) = gameVersion switch
        {
            GameVersion.Runaway3 => (1280, 720),
            _ => (Rgb565.ScreenWidth, Rgb565.ScreenHeight),
        };
        int screen = screenW * screenH;

        // Exactly one full screen needs no detection (Runaway 1/2: 1024x600, Runaway 3: 1280x720).
        if (total == screen)
        {
            return new RasterInfo(screenW, screenH, 99.0, IsMaskLayer(data, screenW, screenH));
        }

        // Known fixed-size rasters: Runaway 1 museum lab monitor feeds (106,032 bytes = 282×188)
        // and chapter selection thumbnails (48,960 bytes = 204×120).
        if (total == 282 * 188)
        {
            return new RasterInfo(282, 188, 99.0, false);
        }
        if (total == 204 * 120)
        {
            return new RasterInfo(204, 120, 99.0, false);
        }

        // Non-standard scene dimensions configured by the executable's scene loader:
        // RESOURCE.D06 (Scene 4060): 1760×1200 (2,112,000 px, 4,224,000 bytes)
        // RESOURCE.D03 (Scene 4030): 1200×670 (804,000 px, 1,608,000 bytes)
        // RESOURCE.E10 (Scene 5100): 1880×700 (1,316,000 px, 2,632,000 bytes)
        // RESOURCE.F27: 1024×700 (716,800 px, 1,433,600 bytes)
        // RESOURCE.G01: 1400×760 (1,064,000 px, 2,128,000 bytes)
        // RESOURCE.G02: 1640×800 (1,312,000 px, 2,624,000 bytes)
        // RESOURCE.G03: 1520×900 (1,368,000 px, 2,736,000 bytes)
        if (total == 1760 * 1200) return new RasterInfo(1760, 1200, 99.0, false);
        if (total == 1200 * 670) return new RasterInfo(1200, 670, 99.0, false);
        if (total == 1880 * 700) return new RasterInfo(1880, 700, 99.0, false);
        if (total == 1024 * 700) return new RasterInfo(1024, 700, 99.0, false);
        if (total == 1400 * 760) return new RasterInfo(1400, 760, 99.0, false);
        if (total == 1640 * 800) return new RasterInfo(1640, 800, 99.0, false);
        if (total == 1520 * 900) return new RasterInfo(1520, 900, 99.0, false);

        (int w0, double sharp)? detected = DetectStride(data);
        if (detected is { } d && d.sharp >= SharpnessMin)
        {
            // The detector can be off by a pixel or two; the true width divides the pixel count.
            foreach (int w in CandidatesAround(d.w0))
            {
                if (w >= WidthMin && total % w == 0 && total / w >= 4)
                {
                    int fundamentalW = ResolveFundamentalWidth(data, w, total);
                    int h = total / fundamentalW;
                    return new RasterInfo(fundamentalW, h, d.sharp, IsMaskLayer(data, fundamentalW, h));
                }
            }
        }

        // Fallback for one or more stacked full screens that are too flat to defeat stride detection (e.g. solid title cards).
        if (total % screen == 0)
        {
            int h = total / screenW;
            return new RasterInfo(screenW, h, 99.0, IsMaskLayer(data, screenW, h));
        }

        return null;
    }

    /// <summary>
    /// Works out whether <paramref name="data"/> is one of Hollywood Monsters' 8-bit indexed rasters.
    /// Supports full-screen rasters (1024x480), travel screen tiles (200x93), bottom action panel (1024x189),
    /// and dialogue menu panels (640x45).
    /// </summary>
    public static RasterInfo? DetectIndexed(ReadOnlySpan<byte> data)
    {
        const int screen = IndexedScreenWidth * IndexedScreenHeight;
        if (data.Length >= screen && data.Length % screen == 0)
        {
            int h = data.Length / IndexedScreenWidth;
            return new RasterInfo(IndexedScreenWidth, h, 99.0, false);
        }

        // Travel destination tiles (RESOURCE.I04 chunks 2-6): 200x93 = 18,600 bytes
        if (data.Length == 200 * 93)
        {
            return new RasterInfo(200, 93, 99.0, false);
        }

        // Bottom action panel (RESOURCE.000 entry 47): 1024x189 = 193,536 bytes
        if (data.Length == 1024 * 189)
        {
            return new RasterInfo(1024, 189, 99.0, false);
        }

        // Dialogue menu panel (RESOURCE.000 entry 48): 640x45 = 28,800 bytes
        if (data.Length == 640 * 45)
        {
            return new RasterInfo(640, 45, 99.0, false);
        }

        return null;
    }

    /// <summary>Decodes an 8-bit indexed raster through <paramref name="palette"/>.</summary>
    public static DecodedImage DecodeIndexed(ReadOnlySpan<byte> data, int width, int height, IndexedPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (data.Length != width * height)
            throw new ArgumentException($"Expected {width * height} bytes for {width}x{height} indexed, got {data.Length}.");

        var pixels = new byte[width * height * 4];
        palette.CopyRow(data, 0, pixels, 0, width * height);
        return new DecodedImage(width, height, pixels);
    }

    /// <summary>Decodes a raster whose geometry <see cref="Detect"/> already established.</summary>
    public static DecodedImage Decode(ReadOnlySpan<byte> data, int width, int height)
    {
        if (data.Length != width * height * 2)
            throw new ArgumentException($"Expected {width * height * 2} bytes for {width}x{height}, got {data.Length}.");

        var pixels = new byte[width * height * 4];
        Rgb565.CopyRow(data, 0, pixels, 0, width * height);
        return new DecodedImage(width, height, pixels);
    }

    /// <summary>Detect + decode in one step. <see langword="null"/> when the bytes are not a raster.</summary>
    public static (DecodedImage Image, RasterInfo Info)? TryDecode(ReadOnlySpan<byte> data, GameVersion gameVersion = GameVersion.Runaway1)
    {
        if (Detect(data, gameVersion) is not { } info)
            return null;
        return (Decode(data, info.Width, info.Height), info);
    }

    private static IEnumerable<int> CandidatesAround(int w0)
    {
        yield return w0;
        for (int delta = 1; delta <= 3; delta++)
        {
            yield return w0 - delta;
            yield return w0 + delta;
        }
    }

    /// <summary>
    /// The lag at which vertically adjacent pixels agree best, and how sharply. A coarse sweep over
    /// every lag with a small sample finds the neighbourhood; the near-minimum candidates are then
    /// re-scored with a large sample so the pick is as reliable as a full sweep would be, at a fraction
    /// of the cost (the full sweep is 760 M subtractions per image, which is too slow for a scan).
    /// </summary>
    public static (int Width, double Sharpness)? DetectStride(ReadOnlySpan<byte> data)
    {
        int totalPixels = data.Length / 2;
        int start = FindActiveRegionStart(data, totalPixels, FineSamplePixels);
        int nFine = Math.Min(totalPixels - start, FineSamplePixels);
        if (nFine < 2000)
            return null;

        var g = new short[nFine];
        for (int i = 0; i < nFine; i++)
            g[i] = (short)Rgb565.Green(data, (start + i) * 2);

        int wmax = Math.Min(WidthMax, Math.Max(WidthMin + 1, totalPixels / 40));
        if (wmax <= WidthMin)
            return null;

        // The coarse sample is three chunks spread over the fine range rather than its first rows:
        // the top of a scene is often flat sky, which would make every lag score low and the
        // sharpness ratio meaningless.
        int lagCount = wmax - WidthMin + 1;
        int chunkLen = Math.Min(nFine, CoarseSamplePixels / CoarseChunks);
        var chunkStarts = new int[CoarseChunks];
        for (int c = 0; c < CoarseChunks; c++)
            chunkStarts[c] = CoarseChunks == 1 ? 0 : (int)((long)(nFine - chunkLen) * c / (CoarseChunks - 1));

        var coarse = new double[lagCount];
        for (int li = 0; li < lagCount; li++)
        {
            long sum = 0, count = 0;
            foreach (int cs in chunkStarts)
                AccumulateAbsLagDiff(g, cs, chunkLen, WidthMin + li, ref sum, ref count);
            coarse[li] = count == 0 ? double.MaxValue : sum / (double)count;
        }

        double best = double.MaxValue;
        foreach (double s in coarse)
            best = Math.Min(best, s);
        if (best <= 0 || best == double.MaxValue)
            return null;

        var sorted = (double[])coarse.Clone();
        Array.Sort(sorted);
        double median = sorted[sorted.Length / 2];

        // Re-score everything within 2% of the coarse minimum with the big sample, then pick the
        // smallest score (ties: smallest width), as the reference implementation does.
        int pick = -1;
        double pickScore = double.MaxValue;
        for (int li = 0; li < lagCount; li++)
        {
            if (coarse[li] > best * 1.02)
                continue;
            long sum = 0, count = 0;
            AccumulateAbsLagDiff(g, 0, nFine, WidthMin + li, ref sum, ref count);
            double fine = count == 0 ? double.MaxValue : sum / (double)count;
            if (fine < pickScore)
            {
                pickScore = fine;
                pick = WidthMin + li;
            }
        }

        if (pick < 0 || pickScore == double.MaxValue)
            return null;

        return (pick, median / best);
    }

    private const int CoarseChunks = 3;

    private static int FindActiveRegionStart(ReadOnlySpan<byte> data, int totalPixels, int sampleLength)
    {
        // If the entry begins with extensive uniform rows (e.g. solid black padding before end credits),
        // skip past them so the stride detector samples regions with actual artwork signal.
        ushort first = Rgb565.Read(data, 0);
        int flatCount = 0;
        for (int i = 0; i < totalPixels; i++)
        {
            if (Rgb565.Read(data, i * 2) != first)
                break;
            flatCount++;
        }

        if (flatCount >= 2000)
        {
            return Math.Max(0, Math.Min(flatCount, totalPixels - sampleLength));
        }

        return 0;
    }

    internal static int ResolveFundamentalWidth(ReadOnlySpan<byte> data, int detectedW, int totalPixels)
    {
        // Harmonic / octave check, in both directions: the detected lag can be a multiple of the true
        // width (octave doubling) or a sub-harmonic of it (a horizontally tiled sheet).
        int start = FindActiveRegionStart(data, totalPixels, FineSamplePixels);
        int nSample = Math.Min(totalPixels - start, 100_000);

        foreach (int factor in new[] { 2, 3 })
        {
            if (detectedW % factor != 0)
                continue;

            int subW = detectedW / factor;
            if (subW < 1000 || totalPixels % subW != 0)
                continue;

            double scoreSub = ScoreStride(data, start, nSample, subW);
            double scoreDetected = ScoreStride(data, start, nSample, detectedW);

            if (scoreSub <= scoreDetected * 1.35)
                return subW;
        }

        // The mirror case. A sheet tiled across repeats at the frame pitch, so the sweep can settle on a
        // sub-harmonic of the row: Runaway 2's RESOURCE.000 entry 158 is twenty-four 64-wide frames laid
        // out twelve across, and lag 128 -- two frames along one scanline -- wins the coarse sweep over
        // the real 768-pixel row. What marks the row out is not that it scores best among the multiples
        // of the pitch but that it scores *sharply* better than the multiples either side of it, so each
        // candidate is judged against its own neighbours. Only multiples that divide the pixel count are
        // scored, which is a handful.
        double scoreAtDetected = ScoreStride(data, start, nSample, detectedW);
        double bestScore = double.MaxValue;
        int bestW = detectedW;

        for (int k = 2; (long)detectedW * (k + 1) <= WidthMax; k++)
        {
            int w = detectedW * k;
            if (totalPixels % w != 0 || totalPixels / w < 4)
                continue;

            double score = ScoreStride(data, start, nSample, w);
            double below = k == 2 ? scoreAtDetected : ScoreStride(data, start, nSample, w - detectedW);
            double above = ScoreStride(data, start, nSample, w + detectedW);
            if (below == double.MaxValue || above == double.MaxValue)
                continue;

            if (score <= Math.Min(below, above) * SubHarmonicDipMax && score < bestScore)
            {
                bestScore = score;
                bestW = w;
            }
        }

        return bestW;
    }

    /// <summary>
    /// How much better than the multiples either side of it a multiple of the detected width has to
    /// agree vertically before it is taken as the row rather than the pitch of a sheet.
    /// <para>
    /// The dip at a real row is steep: Runaway 2's dial sheet scores 4.41 at its 768-pixel row against
    /// 6.98 and 7.51 on either side, a ratio of 0.63. A width that is already the row has no dip at all,
    /// because comparing rows two apart instead of one costs almost nothing -- across the eight scene
    /// backgrounds in the games whose best multiple is a doubling, the shallowest ratio is 0.89. This
    /// threshold sits between the two with room on both sides.
    /// </para>
    /// </summary>
    private const double SubHarmonicDipMax = 0.8;

    internal static double ScoreStride(ReadOnlySpan<byte> data, int start, int length, int stride)
    {
        int n = length - stride;
        if (n <= 0)
            return double.MaxValue;

        long sum = 0;
        for (int i = 0; i < n; i++)
        {
            int g1 = Rgb565.Green(data, (start + i) * 2);
            int g2 = Rgb565.Green(data, (start + i + stride) * 2);
            sum += Math.Abs(g1 - g2);
        }

        return (double)sum / n;
    }

    private static void AccumulateAbsLagDiff(short[] g, int start, int length, int lag, ref long sum, ref long count)
    {
        int n = length - lag;
        if (n <= 0)
            return;

        ReadOnlySpan<short> a = g.AsSpan(start, n);
        ReadOnlySpan<short> b = g.AsSpan(start + lag, n);
        long s = 0;
        for (int i = 0; i < n; i++)
            s += Math.Abs(a[i] - b[i]);
        sum += s;
        count += n;
    }

    /// <summary>
    /// Mean over horizontally adjacent pixel pairs of (max − min) of the three per-channel absolute
    /// differences. Artwork changes all channels together; an index layer does not.
    /// </summary>
    internal static double ChannelDisagreement(ReadOnlySpan<byte> data, int width, int height, int rowStep = 1)
    {
        long sum = 0;
        long pairs = 0;
        for (int y = 0; y < height; y += rowStep)
        {
            int row = y * width * 2;
            for (int x = 0; x + 1 < width; x++)
            {
                ushort p = Rgb565.Read(data, row + x * 2);
                ushort q = Rgb565.Read(data, row + (x + 1) * 2);
                int dr = Math.Abs(((p >> 11) << 3) - ((q >> 11) << 3));
                int dg = Math.Abs((((p >> 5) & 0x3F) << 2) - (((q >> 5) & 0x3F) << 2));
                int db = Math.Abs(((p & 0x1F) << 3) - ((q & 0x1F) << 3));
                sum += Math.Max(dr, Math.Max(dg, db)) - Math.Min(dr, Math.Min(dg, db));
                pairs++;
            }
        }
        return pairs == 0 ? 0 : sum / (double)pairs;
    }

    private static bool IsMaskLayer(ReadOnlySpan<byte> data, int width, int height) =>
        ChannelDisagreement(data, width, height) >= MaskDisagreementMin;

    /// <summary>
    /// Whether pixels of the given size change colour the way artwork does -- the same test
    /// <see cref="Detect"/> uses to spot an index layer, offered to callers that already know the
    /// geometry. A size match alone is not proof that an entry is a picture: Runaway 2 cuts one long
    /// data stream into pieces of exactly 1024x600x2 bytes, and read as pixels those score 115+ here
    /// where its real screens score 5-9.
    /// </summary>
    public static bool LooksLikeArtwork(ReadOnlySpan<byte> data, int width, int height)
    {
        if (data.Length < (long)width * height * 2)
            return false;

        // A sample decides this as well as the whole image does -- the two populations are an order of
        // magnitude apart -- and the global archives are large enough that reading every row costs.
        int rowStep = Math.Max(1, height / ArtworkSampleRows);
        return ChannelDisagreement(data, width, height, rowStep) < MaskDisagreementMin;
    }

    /// <summary>Rows <see cref="LooksLikeArtwork"/> measures, spread over the image.</summary>
    private const int ArtworkSampleRows = 120;

    /// <summary>
    /// The same test without knowing the geometry: neighbouring pixels are read as one long row, which
    /// costs one wrong pair per line and decides nothing on its own. It is the cheap gate in front of
    /// the stride sweep, which is the expensive part of classifying a global archive.
    /// </summary>
    public static bool LooksLikeArtwork(ReadOnlySpan<byte> data)
    {
        int pixels = data.Length / 2;
        if (pixels < 2)
            return false;

        int step = Math.Max(1, pixels / ArtworkSampleLinearPairs);
        long sum = 0, pairs = 0;
        for (int i = 0; i + 1 < pixels; i += step)
        {
            ushort p = Rgb565.Read(data, i * 2);
            ushort q = Rgb565.Read(data, (i + 1) * 2);
            int dr = Math.Abs(((p >> 11) << 3) - ((q >> 11) << 3));
            int dg = Math.Abs((((p >> 5) & 0x3F) << 2) - (((q >> 5) & 0x3F) << 2));
            int db = Math.Abs(((p & 0x1F) << 3) - ((q & 0x1F) << 3));
            sum += Math.Max(dr, Math.Max(dg, db)) - Math.Min(dr, Math.Min(dg, db));
            pairs++;
        }

        return pairs > 0 && sum / (double)pairs < MaskDisagreementMin;
    }

    /// <summary>Pixel pairs the geometry-free <see cref="LooksLikeArtwork(ReadOnlySpan{byte})"/> measures.</summary>
    private const int ArtworkSampleLinearPairs = 20_000;
}