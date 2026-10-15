using System.Buffers.Binary;
using System.Text;

namespace PenduloStudiosExplorer.Core.Formats;

/// <summary>
/// Audio track metadata parsed from a Smacker video header.
/// </summary>
public readonly record struct SmackerAudioTrack(int TrackIndex, int SampleRate, int BitsPerSample, int Channels, bool IsCompressed);

/// <summary>
/// Parsed 104-byte header of a RAD Game Tools Smacker video (<c>.SMK</c>, signatures <c>SMK2</c> and <c>SMK4</c>).
/// Used by <i>Hollywood Monsters</i> (1997) for its cinematic cutscenes and intro/outro movies.
/// </summary>
public sealed class SmackerVideo
{
    public const int HeaderSize = 104;

    public string Signature { get; }
    public int Width { get; }
    public int Height { get; }
    public int TotalFrames { get; }
    public double FramesPerSecond { get; }
    public double DurationSeconds => FramesPerSecond > 0 ? TotalFrames / FramesPerSecond : 0.0;
    public bool HasRingFrame { get; }
    public IReadOnlyList<SmackerAudioTrack> AudioTracks { get; }

    public SmackerVideo(
        string signature,
        int width,
        int height,
        int totalFrames,
        double fps,
        bool hasRingFrame,
        IReadOnlyList<SmackerAudioTrack> audioTracks)
    {
        Signature = signature;
        Width = width;
        Height = height;
        TotalFrames = totalFrames;
        FramesPerSecond = fps;
        HasRingFrame = hasRingFrame;
        AudioTracks = audioTracks;
    }

    /// <summary>
    /// Checks whether the given byte buffer starts with a Smacker video header (<c>SMK2</c> or <c>SMK4</c>).
    /// </summary>
    public static bool IsSmacker(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
            return false;

        uint sig = BinaryPrimitives.ReadUInt32LittleEndian(data);
        // "SMK2" = 0x324B4D53, "SMK4" = 0x344B4D53
        return sig is 0x324B4D53 or 0x344B4D53;
    }

    /// <summary>
    /// Parses the 104-byte header of a Smacker video file.
    /// </summary>
    public static SmackerVideo? TryParse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize || !IsSmacker(data))
            return null;

        string sig = Encoding.ASCII.GetString(data[..4]);
        int width = (int)BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        int height = (int)BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);
        int frames = (int)BinaryPrimitives.ReadUInt32LittleEndian(data[12..]);
        int rawRate = BinaryPrimitives.ReadInt32LittleEndian(data[16..]);
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(data[20..]);

        if (width <= 0 || height <= 0 || frames < 0)
            return null;

        double fps;
        if (rawRate > 0)
            fps = 1000.0 / rawRate;
        else if (rawRate < 0)
            fps = 100000.0 / (-rawRate);
        else
            fps = 10.0;

        bool hasRing = (flags & 1) != 0;

        // Parse up to 7 audio tracks
        var tracks = new List<SmackerAudioTrack>();
        for (int i = 0; i < 7; i++)
        {
            // Bit (i + 1) in flags indicates if track i exists
            if ((flags & (1 << (i + 1))) != 0)
            {
                uint rateFlags = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(72 + i * 4, 4));
                int sampleRate = (int)(rateFlags & 0x00FFFFFF);
                bool is16Bit = (rateFlags & 0x04000000) != 0;
                bool isStereo = (rateFlags & 0x08000000) != 0;
                bool isCompressed = (rateFlags & 0x01000000) != 0;

                tracks.Add(new SmackerAudioTrack(
                    TrackIndex: i,
                    SampleRate: sampleRate > 0 ? sampleRate : 22050,
                    BitsPerSample: is16Bit ? 16 : 8,
                    Channels: isStereo ? 2 : 1,
                    IsCompressed: isCompressed));
            }
        }

        return new SmackerVideo(sig, width, height, frames, fps, hasRing, tracks);
    }

    /// <summary>
    /// Generates a human-readable summary of the video stream.
    /// </summary>
    public string FormatSummary()
    {
        var sb = new StringBuilder();
        sb.Append($"Smacker Video ({Signature}): {Width}×{Height} px, {FramesPerSecond:F2} fps, {TotalFrames:N0} frames ({DurationSeconds:F1}s)");
        if (AudioTracks.Count > 0)
        {
            sb.Append($", {AudioTracks.Count} audio track(s): ");
            for (int i = 0; i < AudioTracks.Count; i++)
            {
                if (i > 0) sb.Append("; ");
                SmackerAudioTrack t = AudioTracks[i];
                sb.Append($"#{t.TrackIndex} ({t.SampleRate} Hz, {t.BitsPerSample}-bit, {(t.Channels == 2 ? "stereo" : "mono")})");
            }
        }
        else
        {
            sb.Append(", no audio");
        }
        return sb.ToString();
    }
}
