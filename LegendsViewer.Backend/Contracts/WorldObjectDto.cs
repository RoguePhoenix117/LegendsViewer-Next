using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Utilities;

namespace LegendsViewer.Backend.Contracts;

public class WorldObjectDto(WorldObject worldObject, DwarfObject? pointOfView = null)
{
    public int Id { get; set; } = worldObject.Id;
    public string StartDate { get; set; } = (worldObject as EventCollection)?.StartDate ?? string.Empty;
    public string EndDate { get; set; } = (worldObject as EventCollection)?.EndDate ?? string.Empty;
    public string Name { get; set; } = worldObject.Name;
    public string? Type { get; set; } = worldObject.Type;
    public string? Subtype { get; set; } = worldObject.Subtype;
    public string Html { get; set; } = worldObject.ToLink(true, pointOfView);
    public int EventCount { get; set; } = worldObject.EventCount;
    public int EventCollectionCount { get; set; } = worldObject.EventCollectionCount;

    /// <summary>
    /// Dwarven translation for display (with accents). Falls back to first Dwarven search alias if not set.
    /// </summary>
    public string? DwarvenAlias => worldObject.DwarvenDisplayName
        ?? worldObject.SearchAliases
            .FirstOrDefault(a => !string.Equals(a, Formatting.NormalizeForSearch(worldObject.Name), StringComparison.Ordinal));
}
