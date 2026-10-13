using System.Text;
using RunawayExplorer.Core.FileSystem;

namespace RunawayExplorer.Core.Formats;

/// <summary>
/// Exports in-game dialogue, subtitles, and script cues into structured transcripts (.txt, .srt, .csv).
/// </summary>
public static class DialogueTranscriptWriter
{
    public sealed record DialogueEntry(
        int SequenceIndex,
        string Name,
        string DisplayName,
        string Text,
        int VoiceClipIndex,
        string? StageName);

    /// <summary>
    /// Flattens a hierarchy of <see cref="FsNode"/>s (scenes, stages, or lines) into a sequential list of dialogue entries.
    /// </summary>
    public static List<DialogueEntry> ExtractEntries(IEnumerable<FsNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var entries = new List<DialogueEntry>();
        int seq = 1;

        void Traverse(FsNode node)
        {
            if (node.Kind == EntryKind.Dialogue || !string.IsNullOrWhiteSpace(node.Subtitle))
            {
                string text = node.Subtitle ?? node.DisplayName;
                string? stageName = node.Parent?.DisplayName;
                entries.Add(new DialogueEntry(
                    SequenceIndex: seq++,
                    Name: node.Name,
                    DisplayName: node.DisplayName,
                    Text: text,
                    VoiceClipIndex: node.VoiceClipIndex,
                    StageName: stageName));
            }

            foreach (FsNode child in node.Children)
                Traverse(child);
        }

        foreach (FsNode n in nodes)
            Traverse(n);

        return entries;
    }

    /// <summary>
    /// Formats the extracted dialogue into a clean human-readable script document.
    /// </summary>
    public static string WriteText(IEnumerable<FsNode> nodes, string? title = null)
    {
        var entries = ExtractEntries(nodes);
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(title))
        {
            sb.AppendLine($"=== {title} ===");
            sb.AppendLine($"Total lines: {entries.Count} (Voiced clips: {entries.Count(e => e.VoiceClipIndex >= 0)})");
            sb.AppendLine();
        }

        foreach (var entry in entries)
        {
            string voiceTag = entry.VoiceClipIndex >= 0 ? $" [VOICE_{entry.VoiceClipIndex:00000}]" : "";
            sb.AppendLine($"[{entry.SequenceIndex:0000}]{voiceTag} {entry.DisplayName}:");
            sb.AppendLine($"  \"{entry.Text}\"");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats the extracted dialogue entries into a standard SubRip (.srt) subtitle file.
    /// </summary>
    public static string WriteSrt(IEnumerable<FsNode> nodes, double secondsPerLine = 3.0)
    {
        var entries = ExtractEntries(nodes);
        var sb = new StringBuilder();
        double currentTime = 0.0;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            double startTime = currentTime;
            double endTime = startTime + secondsPerLine;
            currentTime = endTime + 0.5; // half-second gap between lines

            sb.AppendLine((i + 1).ToString());
            sb.AppendLine($"{FormatTimestamp(startTime)} --> {FormatTimestamp(endTime)}");

            string voicePrefix = entry.VoiceClipIndex >= 0 ? $"[Voice #{entry.VoiceClipIndex}] " : "";
            sb.AppendLine($"{voicePrefix}{entry.Text}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats the dialogue entries as comma-separated values (CSV) for localization and spreadsheets.
    /// </summary>
    public static string WriteCsv(IEnumerable<FsNode> nodes)
    {
        var entries = ExtractEntries(nodes);
        var sb = new StringBuilder();
        sb.AppendLine("Index,Name,Stage,VoiceClip,Text");

        foreach (var entry in entries)
        {
            string cleanText = entry.Text.Replace("\"", "\"\"");
            string voiceStr = entry.VoiceClipIndex >= 0 ? entry.VoiceClipIndex.ToString() : "";
            sb.AppendLine($"{entry.SequenceIndex},\"{entry.Name}\",\"{entry.StageName}\",\"{voiceStr}\",\"{cleanText}\"");
        }

        return sb.ToString();
    }

    private static string FormatTimestamp(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00},{ts.Milliseconds:000}";
    }
}
