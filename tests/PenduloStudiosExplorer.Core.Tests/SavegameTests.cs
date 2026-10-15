using PenduloStudiosExplorer.Core.FileSystem;
using PenduloStudiosExplorer.Core.Formats;
using Xunit;

namespace PenduloStudiosExplorer.Core.Tests;

public class SavegameTests
{
    [Fact]
    public void SyntheticSavegame_DecodesThumbnailAndMetadata()
    {
        byte[] saveBytes = SyntheticAssets.Savegame(
            name: "Test Save 1",
            action: "EXAMINAR LIBRO",
            sceneId: 7,
            viewX: 500,
            viewY: 0,
            actorX: 1100,
            actorY: 580,
            invCount: 5);

        Assert.Equal(SavegameArchive.FileSize, saveBytes.Length);

        // Thumbnail
        DecodedImage thumb = SavegameArchive.DecodeThumbnail(saveBytes);
        Assert.Equal(SavegameArchive.ThumbnailWidth, thumb.Width);
        Assert.Equal(SavegameArchive.ThumbnailHeight, thumb.Height);
        Assert.Equal(SavegameArchive.ThumbnailWidth * SavegameArchive.ThumbnailHeight * 4, thumb.Pixels.Length);

        // Metadata
        SavegameInfo info = SavegameArchive.ReadMetadata(saveBytes, 1);
        Assert.Equal(1, info.SlotNumber);
        Assert.Equal("Test Save 1", info.Name);
        Assert.Equal("EXAMINAR LIBRO", info.Action);
        Assert.Equal(7, info.SceneId);
        Assert.Equal("RESOURCE.B02", info.SceneArchiveName);
        Assert.Equal(500, info.ViewX);
        Assert.Equal(0, info.ViewY);
        Assert.Equal(1100, info.ActorX);
        Assert.Equal(580, info.ActorY);
        Assert.Equal(5, info.InventoryCount);
    }

    [Fact]
    public void SyntheticSavegame_ScoreThumbnailMatch_IdentifiesMatchingScene()
    {
        int bgW = 1024;
        int bgH = 600;
        byte[] bgMatching = SyntheticAssets.Raster(bgW, bgH);
        byte[] bgOther = SyntheticAssets.Raster(bgW, bgH, (x, y) => (ushort)0xFFFF); // solid white

        // Create downscaled 204x120 thumbnail from bgMatching at viewX=0
        var thumb = new byte[SavegameArchive.ThumbnailBytes];
        for (int ty = 0; ty < SavegameArchive.ThumbnailHeight; ty++)
        {
            for (int tx = 0; tx < SavegameArchive.ThumbnailWidth; tx++)
            {
                int bx = tx * 5;
                int by = ty * 5;
                ushort p = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bgMatching.AsSpan((by * bgW + bx) * 2));
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(thumb.AsSpan((ty * SavegameArchive.ThumbnailWidth + tx) * 2), p);
            }
        }

        double matchScore = SavegameArchive.ScoreThumbnailMatch(thumb, 0, bgMatching, bgW, bgH);
        double otherScore = SavegameArchive.ScoreThumbnailMatch(thumb, 0, bgOther, bgW, bgH);

        Assert.Equal(0.0, matchScore);
        Assert.True(otherScore > 20.0);

        var candidates = new (string ArchiveName, byte[] BackgroundRgb565, int Width, int Height)[]
        {
            ("RESOURCE.OTHER", bgOther, bgW, bgH),
            ("RESOURCE.MATCH", bgMatching, bgW, bgH),
        };

        var match = SavegameArchive.MatchSceneArchive(thumb, 0, candidates);
        Assert.NotNull(match);
        Assert.Equal("RESOURCE.MATCH", match.Value.ArchiveName);
        Assert.Equal(0.0, match.Value.Score);
    }

    private const string R1SteamDir = @"F:\Games\Steam\steamapps\common\Runaway A Road Adventure";

    [Fact]
    public void RealInstall_Savegames_LoadedAndDecoded()
    {
        if (!Directory.Exists(R1SteamDir))
            return;

        string saveDir = Path.Combine(R1SteamDir, "SAVEGAME");
        if (!Directory.Exists(saveDir))
            return;

        List<string> saves = SavegameArchive.FindSaves(saveDir);
        Assert.NotEmpty(saves);

        foreach (string path in saves)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.Equal(SavegameArchive.FileSize, bytes.Length);

            int slot = SavegameArchive.GetSlotNumber(path);
            Assert.True(slot > 0);

            SavegameInfo info = SavegameArchive.ReadMetadata(bytes, slot);
            Assert.True(info.SceneId > 0);

            DecodedImage thumb = SavegameArchive.DecodeThumbnail(bytes);
            Assert.Equal(204, thumb.Width);
            Assert.Equal(120, thumb.Height);

            // Check that thumbnail has non-empty pixels
            bool hasNonZero = false;
            for (int i = 0; i < thumb.Pixels.Length; i += 4)
            {
                if (thumb.Pixels[i] != 0 || thumb.Pixels[i + 1] != 0 || thumb.Pixels[i + 2] != 0)
                {
                    hasNonZero = true;
                    break;
                }
            }
            Assert.True(hasNonZero, $"Thumbnail for {path} was completely black");
        }
    }
}
