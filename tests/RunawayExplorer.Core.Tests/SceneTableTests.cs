using RunawayExplorer.Core.FileSystem;
using RunawayExplorer.Core.Formats;
using Xunit;

namespace RunawayExplorer.Core.Tests;

public class SceneTableTests
{
    [Fact]
    public void SyntheticSceneTable_ParsesCorrectly()
    {
        byte[] tabBytes = SyntheticAssets.SceneTab([
            (101, 1920, 1080, [(662f, 916f), (1200f, 800f)]),
            (201, 2020, 1080, [(500f, 400f)]),
            (401, 2920, 1080, [(1500f, 600f), (1800f, 700f)]),
        ]);

        List<SceneRecord> records = SceneTable.Parse(tabBytes);
        Assert.Equal(3, records.Count);

        Assert.Equal(101, records[0].SceneId);
        Assert.Equal("RESOURCE.A01", records[0].ArchiveName);
        Assert.Equal(1920, records[0].Width);
        Assert.Equal(1080, records[0].Height);
        Assert.Equal(2, records[0].CameraPoints.Count);
        Assert.Equal(662f, records[0].CameraPoints[0].X);
        Assert.Equal(916f, records[0].CameraPoints[0].Y);

        Assert.Equal(201, records[1].SceneId);
        Assert.Equal("RESOURCE.B01", records[1].ArchiveName);
        Assert.Equal(2020, records[1].Width);
        Assert.Equal(1080, records[1].Height);
        Assert.Single(records[1].CameraPoints);

        Assert.Equal(401, records[2].SceneId);
        Assert.Equal("RESOURCE.D01", records[2].ArchiveName);
        Assert.Equal(2920, records[2].Width);
        Assert.Equal(1080, records[2].Height);
        Assert.Equal(2, records[2].CameraPoints.Count);
    }

    [Fact]
    public void RealInstall_TNBT_SceneTable_ParsesAllScenes()
    {
        if (!Directory.Exists(TheNextBigThingTests.TnbtSteamDir))
            return;

        string tabPath = Path.Combine(TheNextBigThingTests.TnbtSteamDir, "Resource", "RESOURCE.TAB");
        if (!File.Exists(tabPath))
            return;

        byte[] bytes = File.ReadAllBytes(tabPath);
        List<SceneRecord> scenes = SceneTable.Parse(bytes);

        Assert.Equal(63, scenes.Count);
        foreach (SceneRecord s in scenes)
        {
            if (s.SceneId is not (0 or 1 or 90))
            {
                Assert.True(s.Width >= 1920, $"Scene {s.SceneId} width {s.Width} < 1920");
                Assert.True(s.Height >= 1080, $"Scene {s.SceneId} height {s.Height} < 1080");
            }
            Assert.NotEmpty(s.ArchiveName);
        }
    }

    [Fact]
    public void RealInstall_Yesterday_SceneTable_ParsesAllScenes()
    {
        if (!Directory.Exists(YesterdayTests.YesterdaySteamDir))
            return;

        string tabPath = Path.Combine(YesterdayTests.YesterdaySteamDir, "Resource", "RESOURCE.TAB");
        if (!File.Exists(tabPath))
            return;

        byte[] bytes = File.ReadAllBytes(tabPath);
        List<SceneRecord> scenes = SceneTable.Parse(bytes);

        Assert.Equal(53, scenes.Count);
        foreach (SceneRecord s in scenes)
        {
            Assert.True(s.Width >= 1024, $"Scene {s.SceneId} width {s.Width} < 1024");
            Assert.True(s.Height >= 576, $"Scene {s.SceneId} height {s.Height} < 576");
            Assert.NotEmpty(s.ArchiveName);
        }
    }
}
