namespace PenduloStudiosExplorer.Core.Formats;

/// <summary>
/// Tells a sprite <em>atlas</em> -- a set of unrelated pictures stored in the animation format, like
/// Runaway 2's inventory icons and close-ups -- from a real animation. Nothing in the file says which
/// one an entry is: an atlas has the same record table as a walk cycle. What gives it away is that its
/// frames do not follow on from each other.
/// <para>
/// Two consecutive frames are compared pixel by pixel in the animation's own space: a pixel
/// <em>matches</em> when both frames paint it in nearly the same colour, and the pair's similarity is
/// the matches over every pixel either frame paints. Consecutive frames of an animation share most of
/// their pixels -- a talking head moves its mouth, a dial moves its needle -- while an atlas's frames are
/// different objects that happen to sit in the same box, and match almost nowhere.
/// </para>
/// <para>
/// A frame counts as standalone when it is unlike both of its neighbours, and an asset is an atlas when
/// nearly every frame is standalone. "Nearly": an atlas can hold the same object twice in a row (two
/// states of one button), and that pair should not turn the whole set back into an animation.
/// Empty frames carry no picture and are left out of the sequence.
/// </para>
/// <para>
/// This is a heuristic, which the rest of the classifier avoids. It decides only how an animation is
/// first <em>shown</em> -- as a grid or played -- and the viewer can switch either way, so a wrong answer
/// costs one click, not a mislabelled entry.
/// </para>
/// </summary>
public static class SpriteAtlasDetector
{
    /// <summary>Below this similarity two frames are taken to be different pictures.</summary>
    public const double StandaloneSimilarity = 0.2;

    /// <summary>The share of frames that must be standalone for the asset to be an atlas.</summary>
    public const double StandaloneShare = 0.75;

    /// <summary>Largest per-channel difference at which two painted pixels still count as the same.</summary>
    public const int ChannelTolerance = 24;

    /// <summary>
    /// Whether <paramref name="asset"/> is a set of unrelated pictures rather than an animation.
    /// Decodes every frame; a single-frame asset is never an atlas.
    /// </summary>
    public static bool LooksLikeAtlas(IAnimationAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var frames = new List<SpriteFrame>(asset.FrameCount);
        for (int i = 0; i < asset.FrameCount; i++)
        {
            SpriteFrame frame = asset.DecodeFrame(i);
            if (!frame.IsEmpty)
                frames.Add(frame);
        }

        return LooksLikeAtlas(frames);
    }

    /// <summary>The decision on frames already decoded. Empty frames are skipped.</summary>
    public static bool LooksLikeAtlas(IReadOnlyList<SpriteFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var painted = frames.Where(f => !f.IsEmpty).ToList();
        if (painted.Count < 2)
            return false;

        var similarity = new double[painted.Count - 1];
        for (int i = 0; i < similarity.Length; i++)
            similarity[i] = Similarity(painted[i], painted[i + 1]);

        int standalone = 0;
        for (int i = 0; i < painted.Count; i++)
        {
            double prev = i > 0 ? similarity[i - 1] : 0;
            double next = i < similarity.Length ? similarity[i] : 0;
            if (Math.Max(prev, next) < StandaloneSimilarity)
                standalone++;
        }

        return standalone >= StandaloneShare * painted.Count;
    }

    /// <summary>
    /// The share of the pixels painted by either frame that both paint in nearly the same colour: 1 for
    /// identical frames, 0 for frames with nothing in common. 0 when either frame is empty.
    /// </summary>
    public static double Similarity(SpriteFrame a, SpriteFrame b)
    {
        if (a.Image is not { } ia || b.Image is not { } ib)
            return 0;

        long paintedA = CountPainted(ia), paintedB = CountPainted(ib);

        // Only the overlap of the two boxes can hold pixels both frames paint.
        int x0 = Math.Max(a.X, b.X), y0 = Math.Max(a.Y, b.Y);
        int x1 = Math.Min(a.X + ia.Width, b.X + ib.Width), y1 = Math.Min(a.Y + ia.Height, b.Y + ib.Height);

        long both = 0, matches = 0;
        for (int y = y0; y < y1; y++)
        {
            int rowA = ((y - a.Y) * ia.Width - a.X) * 4;
            int rowB = ((y - b.Y) * ib.Width - b.X) * 4;
            for (int x = x0; x < x1; x++)
            {
                int pa = rowA + x * 4, pb = rowB + x * 4;
                if (ia.Pixels[pa + 3] == 0 || ib.Pixels[pb + 3] == 0)
                    continue;

                both++;
                if (Math.Abs(ia.Pixels[pa] - ib.Pixels[pb]) <= ChannelTolerance
                    && Math.Abs(ia.Pixels[pa + 1] - ib.Pixels[pb + 1]) <= ChannelTolerance
                    && Math.Abs(ia.Pixels[pa + 2] - ib.Pixels[pb + 2]) <= ChannelTolerance)
                {
                    matches++;
                }
            }
        }

        long union = paintedA + paintedB - both;
        return union == 0 ? 0 : (double)matches / union;
    }

    private static long CountPainted(DecodedImage image)
    {
        long n = 0;
        byte[] px = image.Pixels;
        for (int i = 3; i < px.Length; i += 4)
        {
            if (px[i] != 0)
                n++;
        }
        return n;
    }
}
