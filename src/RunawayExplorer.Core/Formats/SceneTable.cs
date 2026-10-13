using System.Buffers.Binary;
using RunawayExplorer.Core.FileSystem;

namespace RunawayExplorer.Core.Formats;

/// <summary>
/// One scene entry parsed from <c>RESOURCE.TAB</c> in <em>The Next BIG Thing</em> or <em>Yesterday</em>.
/// Contains the scene's dimensions, camera coordinates, aspect ratios, and navigation bounds.
/// </summary>
/// <param name="Index">0-based index in the scene table.</param>
/// <param name="SceneId">Numeric scene ID from the header.</param>
/// <param name="ArchiveName">Mapped archive name (e.g. <c>RESOURCE.A01</c>).</param>
/// <param name="Width">Scene canvas width in pixels (e.g. 1920, 2020, 2920).</param>
/// <param name="Height">Scene canvas height in pixels (e.g. 1080, 1120).</param>
/// <param name="Offset">Offset in <c>RESOURCE.TAB</c>.</param>
/// <param name="Size">Size of the scene definition record in bytes.</param>
/// <param name="CameraPoints">Parsed camera / viewpoint coordinate pairs.</param>
public sealed record SceneRecord(
    int Index,
    int SceneId,
    string ArchiveName,
    int Width,
    int Height,
    int Offset,
    int Size,
    IReadOnlyList<(float X, float Y)> CameraPoints);

/// <summary>
/// Reads and decodes <c>RESOURCE.TAB</c> in <em>The Next BIG Thing</em> and <em>Yesterday</em>.
/// Format:
/// - byte SceneCount
/// - u16 SceneId[SceneCount]
/// - u32 Offset[SceneCount]
/// - Payload per scene: Width (u16), Height (u16), followed by camera &amp; waypoint geometry.
/// </summary>
public static class SceneTable
{
    public static bool IsSceneTableName(string fileName) =>
        string.Equals(Path.GetFileName(fileName), "RESOURCE.TAB", StringComparison.OrdinalIgnoreCase);

    /// <summary>Maps a scene ID (e.g. 101, 201, 810) to its archive name (<c>RESOURCE.A01</c>, <c>RESOURCE.H10</c>).</summary>
    public static string SceneIdToArchive(int sceneId)
    {
        if (sceneId == 0)
            return "RESOURCE.000";
        if (sceneId < 100)
            return $"RESOURCE.A{sceneId:00}";

        int areaIdx = (sceneId / 100) - 1;
        char area = (char)('A' + areaIdx);
        int num = sceneId % 100;
        return $"RESOURCE.{area}{num:00}";
    }

    /// <summary>Parses all scene records from <paramref name="data"/>.</summary>
    public static List<SceneRecord> Parse(ReadOnlySpan<byte> data)
    {
        var records = new List<SceneRecord>();
        if (data.Length < 1)
            return records;

        int count = data[0];
        int headerSize = 1 + count * 2;
        int tableEnd = headerSize + count * 4;

        if (count == 0 || data.Length < tableEnd)
            return records;

        var sceneIds = new ushort[count];
        for (int i = 0; i < count; i++)
            sceneIds[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(1 + i * 2, 2));

        var offsets = new uint[count];
        for (int i = 0; i < count; i++)
            offsets[i] = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(headerSize + i * 4, 4));

        for (int i = 0; i < count; i++)
        {
            uint off = offsets[i];
            long end = i + 1 < count ? offsets[i + 1] : data.Length;
            int size = (int)(end - off);

            if (off >= data.Length || size <= 0)
                continue;

            ReadOnlySpan<byte> entryData = data.Slice((int)off, Math.Min(size, data.Length - (int)off));
            int width = entryData.Length >= 2 ? BinaryPrimitives.ReadUInt16LittleEndian(entryData) : 0;
            int height = entryData.Length >= 4 ? BinaryPrimitives.ReadUInt16LittleEndian(entryData[2..]) : 0;

            var cameraPoints = new List<(float X, float Y)>();
            // If the record has camera coordinates starting around offset +20:
            // inspect 4-byte little-endian IEEE-754 floats
            for (int p = 20; p + 8 <= entryData.Length && p < 120; p += 8)
            {
                float x = BinaryPrimitives.ReadSingleLittleEndian(entryData[p..]);
                float y = BinaryPrimitives.ReadSingleLittleEndian(entryData[(p + 4)..]);
                if (float.IsFinite(x) && float.IsFinite(y) && x >= 0 && x <= 4000 && y >= 0 && y <= 3000)
                {
                    cameraPoints.Add((x, y));
                }
            }

            records.Add(new SceneRecord(
                Index: i,
                SceneId: sceneIds[i],
                ArchiveName: SceneIdToArchive(sceneIds[i]),
                Width: width,
                Height: height,
                Offset: (int)off,
                Size: size,
                CameraPoints: cameraPoints));
        }

        return records;
    }
}
