using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Parser;
using System.Text.Json.Serialization;

namespace LegendsViewer.Backend.Legends;

public abstract class WorldObject : DwarfObject
{
    public string Icon { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public virtual string Type { get; set; } = string.Empty;
    public virtual string Subtype { get; set; } = string.Empty;

    public int PreviousId { get; set; }
    public int NextId { get; set; }

    [JsonIgnore]
    public List<WorldEvent> Events { get; set; } = [];

    [JsonIgnore]
    public List<EventCollection> EventCollections { get; set; } = [];

    [JsonIgnore]
    public World? World { get; }

    [JsonIgnore]
    public IReadOnlyList<string> SearchAliases => _searchAliases;

    /// <summary>
    /// Raw Dwarven translation with accents for display. Search aliases use normalized (accent-stripped) form.
    /// </summary>
    public string? DwarvenDisplayName { get; set; }

    private readonly List<string> _searchAliases = [];

    public int EventCount => Events.Count;
    public int EventCollectionCount => EventCollections.Count;

    protected WorldObject()
    {
        Id = -1;
    }

    protected WorldObject(World world) : this()
    {
        World = world;
    }

    protected WorldObject(List<Property> properties, World world) : this(world)
    {
        foreach (Property property in properties)
        {
            switch (property.Name)
            {
                case "id": Id = Convert.ToInt32(property.Value); break;
            }
        }
    }

    public override string ToLink(bool link = true, DwarfObject? pov = null, WorldEvent? worldEvent = null)
    {
        return "";
    }

    public void SetSearchAliases(IEnumerable<string> aliases)
    {
        _searchAliases.Clear();
        foreach (var alias in aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                _searchAliases.Add(alias);
            }
        }
    }
}
