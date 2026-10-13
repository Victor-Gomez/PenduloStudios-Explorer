using System.Text;

namespace RunawayExplorer.Core.Formats;

/// <summary>
/// A single timed subtitle cue for an in-game cinematic video.
/// </summary>
/// <param name="Text">The spoken dialogue / subtitle text.</param>
/// <param name="X">Horizontal screen position in pixels (-1 if centered/default).</param>
/// <param name="Y">Vertical screen position in pixels (-1 if default).</param>
/// <param name="BeginFrame">Video frame number where this subtitle begins.</param>
/// <param name="EndFrame">Video frame number where this subtitle ends.</param>
public sealed record SubtitleCue(string Text, int X, int Y, int BeginFrame, int EndFrame);

/// <summary>
/// Parser for <i>Yesterday</i>'s cutscene subtitle scripts (<c>Datav/*.001</c>).
/// Pairs 1:1 with corresponding Bink cutscenes (<c>Datav/*.000</c>).
/// </summary>
public static class CutsceneSubtitles
{
    public static List<SubtitleCue> Parse(string content)
    {
        var cues = new List<SubtitleCue>();
        if (string.IsNullOrWhiteSpace(content))
            return cues;

        string[] lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        string currentText = "";
        int x = -1, y = -1, beginFrame = -1, endFrame = -1;
        bool inBlock = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("//"))
            {
                if (inBlock && !string.IsNullOrEmpty(currentText))
                {
                    cues.Add(new SubtitleCue(currentText, x, y, beginFrame, endFrame));
                    x = -1; y = -1; beginFrame = -1; endFrame = -1;
                }

                currentText = line[2..].Trim();
                inBlock = true;
                continue;
            }

            if (!inBlock)
                continue;

            if (line.Equals("X", StringComparison.OrdinalIgnoreCase) && i + 1 < lines.Length)
            {
                int.TryParse(lines[++i].Trim(), out x);
            }
            else if (line.Equals("Y", StringComparison.OrdinalIgnoreCase) && i + 1 < lines.Length)
            {
                int.TryParse(lines[++i].Trim(), out y);
            }
            else if (line.Equals("B", StringComparison.OrdinalIgnoreCase) && i + 1 < lines.Length)
            {
                int.TryParse(lines[++i].Trim(), out beginFrame);
            }
            else if (line.Equals("E", StringComparison.OrdinalIgnoreCase) && i + 1 < lines.Length)
            {
                int.TryParse(lines[++i].Trim(), out endFrame);
            }
        }

        if (inBlock && !string.IsNullOrEmpty(currentText))
        {
            cues.Add(new SubtitleCue(currentText, x, y, beginFrame, endFrame));
        }

        return cues;
    }

    /// <summary>
    /// Formats subtitle cues into a clean readable transcript.
    /// </summary>
    public static string ToSummary(IReadOnlyList<SubtitleCue> cues)
    {
        var sb = new StringBuilder();
        foreach (var c in cues)
        {
            string timing = c.BeginFrame >= 0
                ? (c.EndFrame >= 0 ? $"[{c.BeginFrame} - {c.EndFrame}] " : $"[{c.BeginFrame}] ")
                : "";
            sb.AppendLine($"{timing}{c.Text}");
        }
        return sb.ToString().TrimEnd();
    }
}
