using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Maps;
using LegendsViewer.Backend.Legends.WorldObjects;
using Microsoft.AspNetCore.Mvc;

namespace LegendsViewer.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorldMapController(IWorld worldDataService, IWorldMapImageGenerator worldMapImageGenerator) : ControllerBase
{
    private readonly IWorld _worldDataService = worldDataService;
    private readonly IWorldMapImageGenerator _worldMapImageGenerator = worldMapImageGenerator;

    [HttpGet("world/{size}")]
    [ResponseCache(Duration = 3600, VaryByQueryKeys = new[] { "size" })] // Cache for 1 hour
    public ActionResult<byte[]?> GetWorldMap(MapSize size = MapSize.Default)
    {
        var imageData = _worldMapImageGenerator.GenerateMapByteArray(GetTileSizeByEnum(size));
        if (imageData == null)
        {
            return NotFound();
        }
        return imageData;
    }

    [HttpGet("underworld/{size}/{depth}")]
    [ResponseCache(Duration = 3600, VaryByQueryKeys = new[] { "size", "depth" })] // Cache for 1 hour
    public ActionResult<byte[]?> GetUnderworldMap([FromRoute] MapSize size = MapSize.Default, [FromRoute] int? depth = null)
    {
        var imageData = _worldMapImageGenerator.GenerateMapByteArray(GetTileSizeByEnum(size), depth);
        if (imageData == null)
        {
            return NotFound();
        }
        return imageData;
    }

    [HttpGet("landmass/{id}/{size}")]
    public ActionResult<byte[]?> GetLandmassMap(int id, MapSize size = MapSize.Default)
    {
        return GetWorldObjectMap(GetTileSizeByEnum(size), _worldDataService.GetLandmass(id));
    }

    [HttpGet("mountainpeak/{id}/{size}")]
    public ActionResult<byte[]?> GetMountainPeakMap(int id, MapSize size = MapSize.Default)
    {
        return GetWorldObjectMap(GetTileSizeByEnum(size), _worldDataService.GetMountainPeak(id));
    }

    [HttpGet("region/{id}/{size}")]
    public ActionResult<byte[]?> GetRegionMap(int id, MapSize size = MapSize.Default)
    {
        return GetWorldObjectMap(GetTileSizeByEnum(size), _worldDataService.GetRegion(id));
    }

    [HttpGet("river/{id}/{size}")]
    public ActionResult<byte[]?> GetRiverMap(int id, MapSize size = MapSize.Default)
    {
        return GetWorldObjectMap(GetTileSizeByEnum(size), _worldDataService.GetRiver(id));
    }

    [HttpGet("construction/{id}/{size}")]
    public ActionResult<byte[]?> GetWorldConstructionMap(int id, MapSize size = MapSize.Default)
    {
        return GetWorldObjectMap(GetTileSizeByEnum(size), _worldDataService.GetWorldConstruction(id));
    }

    [HttpGet("undergroundregion/{id}/{size}")]
    public ActionResult<byte[]?> GetUndergroundRegionMap(int id, MapSize size = MapSize.Default)
    {
        UndergroundRegion? undergroundRegion = _worldDataService.GetUndergroundRegion(id);
        return GetWorldObjectMap(GetTileSizeByEnum(size), undergroundRegion, undergroundRegion?.Depth);
    }

    [HttpGet("site/{id}/{size}")]
    public ActionResult<byte[]?> GetSiteMap(int id, MapSize size = MapSize.Default)
    {
        return GetWorldObjectMap(GetTileSizeByEnum(size), _worldDataService.GetSite(id));
    }

    [HttpGet("structure/{id}/{size}")]
    public ActionResult<byte[]?> GetStructureMap(int id, MapSize size = MapSize.Default)
    {
        return GetWorldObjectMap(GetTileSizeByEnum(size), _worldDataService.GetStructure(id));
    }

    [HttpGet("entity/{id}/{size}")]
    public ActionResult<byte[]?> GetEntityMap(int id, MapSize size = MapSize.Default)
    {
        return GetWorldObjectMap(GetTileSizeByEnum(size), _worldDataService.GetEntity(id));
    }

    [HttpGet("artifact/{id}/{size}")]
    public ActionResult<byte[]?> GetArtifactMap(int id, MapSize size = MapSize.Default)
    {
        return GetWorldObjectMap(GetTileSizeByEnum(size), _worldDataService.GetArtifact(id));
    }

    private ActionResult<byte[]?> GetWorldObjectMap(int tileSize, WorldObject? worldObject, int? depth = null)
    {
        if (worldObject is not IHasCoordinates item)
        {
            return NotFound();
        }
        var imageData = _worldMapImageGenerator.GenerateMapByteArray(tileSize, depth, item);
        if (imageData == null)
        {
            return NotFound();
        }
        return imageData;
    }

    private static int GetTileSizeByEnum(MapSize size)
    {
        return size switch
        {
            MapSize.Small => WorldMapImageGenerator.DefaultTileSizeMin,
            MapSize.Large => WorldMapImageGenerator.DefaultTileSizeMax,
            _ => WorldMapImageGenerator.DefaultTileSizeMid,
        };
    }
}
