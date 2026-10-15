using RunawayExplorer.Core.FileSystem;
using RunawayExplorer.Core.Formats;

namespace RunawayExplorer.Services;

/// <summary>
/// A selectable "show only this kind of entry" category for the tree's type filter. Runaway's entries
/// have no extensions, so the filter works on <see cref="FsNode.Kind"/> or a custom predicate.
/// <see cref="Kinds"/> and <see cref="CustomPredicate"/> are <see langword="null"/> for the "All Types" entry.
/// </summary>
public sealed record ResourceTypeFilter(
    string Label,
    EntryKind[]? Kinds,
    string? IconResourceKey = null,
    Func<FsNode, bool>? CustomPredicate = null)
{
    public static readonly ResourceTypeFilter All = new("All Types", null, "FolderClosedTypeIcon");
    public static readonly ResourceTypeFilter AllEs = new("Todos los tipos", null, "FolderClosedTypeIcon");

    /// <summary>
    /// Identifies whether an <see cref="FsNode"/> is a precomputed navigation mesh or scene route table.
    /// </summary>
    public static bool IsNavigationMesh(FsNode node)
    {
        if (node.Kind == EntryKind.SceneTable)
            return true;

        if (node.Kind == EntryKind.Data)
        {
            if (node.Size is RouteDecoder.TotalSizeR1
                or RouteDecoder.TotalSizeR2
                or RouteDecoder.TotalSizeTnbtWaypoints
                or RouteDecoder.TotalSizeTnbtHops
                or CorridorSpanDecoder.TotalSize)
            {
                return true;
            }
        }

        return false;
    }

    public static readonly IReadOnlyList<ResourceTypeFilter> Categories =
    [
        All,
        new("Backgrounds", [EntryKind.Background], "ImageTypeIcon"),
        new("Scene masks", [EntryKind.Mask], "ImageTypeIcon"),
        new("Overlays", [EntryKind.Overlay], "ImageTypeIcon"),
        new("Animations", [EntryKind.Animation, EntryKind.CharacterAnimation, EntryKind.ActorAnimation], "AnimationTypeIcon"),
        new("All images", [EntryKind.Background, EntryKind.Mask, EntryKind.Overlay, EntryKind.Animation, EntryKind.CharacterAnimation, EntryKind.ActorAnimation, EntryKind.Savegame], "ImageTypeIcon"),
        new("Navigation meshes", null, "ModelTypeIcon", IsNavigationMesh),
        new("Saved games", [EntryKind.Savegame], "ImageTypeIcon"),
        new("Fonts", [EntryKind.Font], "ImageTypeIcon"),
        new("Music", [EntryKind.Music], "SoundTypeIcon"),
        new("Ambient & SFX", [EntryKind.Ambient], "SoundTypeIcon"),
        new("Cinematic audio", [EntryKind.Cinematic], "SoundTypeIcon"),
        new("Voice", [EntryKind.Voice], "SoundTypeIcon"),
        new("Video", [EntryKind.Video], "VideoTypeIcon"),
        new("Lip-sync", [EntryKind.Viseme], "DataTypeIcon"),
        new("Dialogue", [EntryKind.Dialogue], "DataTypeIcon"),
        new("Data", [EntryKind.Data, EntryKind.GlobalData, EntryKind.RawFile, EntryKind.SceneTable], "RawFileTypeIcon"),
    ];

    public static IReadOnlyList<ResourceTypeFilter> GetCategories(string? language = null)
    {
        bool es = RunawayExplorer.Core.Metadata.SceneCatalog.IsSpanish(language);
        if (!es)
            return Categories;

        return
        [
            AllEs,
            new("Fondos", [EntryKind.Background], "ImageTypeIcon"),
            new("Máscaras de escena", [EntryKind.Mask], "ImageTypeIcon"),
            new("Capas", [EntryKind.Overlay], "ImageTypeIcon"),
            new("Animaciones", [EntryKind.Animation, EntryKind.CharacterAnimation, EntryKind.ActorAnimation], "AnimationTypeIcon"),
            new("Todas las imágenes", [EntryKind.Background, EntryKind.Mask, EntryKind.Overlay, EntryKind.Animation, EntryKind.CharacterAnimation, EntryKind.ActorAnimation, EntryKind.Savegame], "ImageTypeIcon"),
            new("Mallas de navegación", null, "ModelTypeIcon", IsNavigationMesh),
            new("Partidas guardadas", [EntryKind.Savegame], "ImageTypeIcon"),
            new("Fuentes", [EntryKind.Font], "ImageTypeIcon"),
            new("Música", [EntryKind.Music], "SoundTypeIcon"),
            new("Sonido ambiente y efectos", [EntryKind.Ambient], "SoundTypeIcon"),
            new("Audio de cinemáticas", [EntryKind.Cinematic], "SoundTypeIcon"),
            new("Voces", [EntryKind.Voice], "SoundTypeIcon"),
            new("Vídeos", [EntryKind.Video], "VideoTypeIcon"),
            new("Sincronización labial", [EntryKind.Viseme], "DataTypeIcon"),
            new("Diálogos", [EntryKind.Dialogue], "DataTypeIcon"),
            new("Datos", [EntryKind.Data, EntryKind.GlobalData, EntryKind.RawFile, EntryKind.SceneTable], "RawFileTypeIcon"),
        ];
    }

    public bool Matches(FsNode node)
    {
        if (CustomPredicate is not null)
            return CustomPredicate(node);
        if (Kinds is null)
            return true;
        return Array.IndexOf(Kinds, node.Kind) >= 0;
    }

    public override string ToString() => Label;
}
