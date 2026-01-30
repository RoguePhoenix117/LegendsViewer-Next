using LegendsViewer.Backend.Extensions;
using LegendsViewer.Backend.Legends.Bookmarks;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Maps;
using Microsoft.AspNetCore.Mvc;

namespace LegendsViewer.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookmarkController(
    ILogger<BookmarkController> logger,
    IWorld worldDataService,
    IWorldMapImageGenerator worldMapImageGenerator,
    IBookmarkService bookmarkService,
    IConfiguration configuration) : ControllerBase
{
    public const string FileIdentifierLegendsXml = "-legends.xml";

    public const string FileIdentifierWorldHistoryTxt = "-world_history.txt";
    public const string FileIdentifierWorldMapBmp = "-world_map.bmp";
    public const string FileIdentifierWorldSitesAndPops = "-world_sites_and_pops.txt";
    public const string FileIdentifierLegendsPlusXml = "-legends_plus.xml";
    private readonly IWorld _worldDataService = worldDataService;
    private readonly IWorldMapImageGenerator _worldMapImageGenerator = worldMapImageGenerator;
    private readonly IBookmarkService _bookmarkService = bookmarkService;
    private readonly string _dataDirectory = configuration["DataDirectory"] ?? "/app/data";

    [HttpGet]
    [ProducesResponseType<List<Bookmark>>(StatusCodes.Status200OK)]
    public ActionResult<List<Bookmark>> Get()
    {
        var bookmarks = _bookmarkService.GetAll();
        return Ok(bookmarks);
    }

    [HttpGet("{filePath}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Bookmark> Get([FromRoute] string filePath)
    {
        var item = _bookmarkService.GetBookmark(filePath);
        if (item == null)
        {
            return NotFound();
        }
        return Ok(item);
    }

    [HttpDelete("{filePath}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Delete([FromRoute] string filePath)
    {
        // Check admin authorization
        if (!HttpContext.IsAdmin(configuration))
        {
            return Unauthorized("Admin access required to delete bookmarks.");
        }

        // Delete the entire bookmark entry (not just a timestamp)
        if (!_bookmarkService.DeleteBookmark(filePath))
        {
            return NotFound($"Bookmark not found for file path: {filePath}");
        }

        return NoContent();
    }

    [HttpGet("isAdmin")]
    [ProducesResponseType<bool>(StatusCodes.Status200OK)]
    public ActionResult<bool> IsAdmin()
    {
        return Ok(HttpContext.IsAdmin(configuration));
    }

    [HttpPost("loadByFileName")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Bookmark>> ParseWorldXml([FromBody] string fileName)
    {
        // Check admin authorization
        if (!HttpContext.IsAdmin(configuration))
        {
            return Unauthorized("Admin access required to load worlds.");
        }

        // Validate filename to prevent path traversal
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.Contains("..") ||
            fileName.Contains("/") ||
            fileName.Contains("\\"))
        {
            return BadRequest("Invalid file name.");
        }

        var filePath = Path.Combine(_dataDirectory, fileName);

        if (!System.IO.File.Exists(filePath))
        {
            return BadRequest($"File not found: {fileName}");
        }

        FileInfo fileInfo = new(filePath);
        string regionId;
        if (fileInfo.Name.Contains(FileIdentifierLegendsXml))
        {
            regionId = fileInfo.Name.Replace(FileIdentifierLegendsXml, "");
        }
        else if (fileInfo.Name.Contains(FileIdentifierLegendsPlusXml))
        {
            regionId = fileInfo.Name.Replace(FileIdentifierLegendsPlusXml, "");
        }
        else
        {
            return BadRequest($"Invalid file name.\n{fileInfo.Name}");
        }

        var (RegionName, Timestamp) = BookmarkService.GetRegionNameAndTimestampByRegionId(regionId, _worldDataService);

        var xmlFileName = Directory.EnumerateFiles(_dataDirectory, regionId + FileIdentifierLegendsXml).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(xmlFileName))
        {
            return BadRequest("Invalid XML file");
        }
        var xmlPlusFileName = Directory.EnumerateFiles(_dataDirectory, regionId + FileIdentifierLegendsPlusXml).FirstOrDefault();
        var historyFileName = Directory.EnumerateFiles(_dataDirectory, regionId + FileIdentifierWorldHistoryTxt).FirstOrDefault();
        var sitesAndPopsFileName = Directory.EnumerateFiles(_dataDirectory, regionId + FileIdentifierWorldSitesAndPops).FirstOrDefault();
        var mapFileName = Directory.EnumerateFiles(_dataDirectory, regionId + FileIdentifierWorldMapBmp).FirstOrDefault();

        try
        {
            _worldDataService.Clear();
            _worldMapImageGenerator.Clear();

            logger.LogInformation($"Start loading world '{regionId}' from '{_dataDirectory}'");

            await _worldMapImageGenerator.LoadExportedWorldMapAsync(mapFileName);
            await _worldDataService.ParseAsync(xmlFileName, xmlPlusFileName, historyFileName, sitesAndPopsFileName, mapFileName);

            logger.LogInformation(_worldDataService.Log.ToString());

            var bookmark = AddBookmark(filePath, RegionName, Timestamp);

            return Ok(bookmark);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error parsing the XML file: {ex.Message}");
        }
    }

    // Keep old endpoints for backward compatibility, but redirect to new method
    [HttpPost("loadByFullPath")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Bookmark>> ParseWorldXmlByFullPath([FromBody] string filePath)
    {
        // Extract filename from path and use new method
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("Invalid file path.");
        }
        return await ParseWorldXml(fileName);
    }

    [HttpPost("loadByFolderAndFile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Bookmark>> ParseWorldXmlByFolderAndFile([FromBody] string folderPath, string fileName)
    {
        // Ignore folderPath, only use fileName from data directory
        return await ParseWorldXml(fileName);
    }

    private Bookmark AddBookmark(string filePath, string regionName, string timestamp)
    {
        var imageData = _worldMapImageGenerator.GenerateMapByteArray(WorldMapImageGenerator.DefaultTileSizeMin);
        var bookmark = new Bookmark
        {
            FilePath = BookmarkService.ReplaceLastOccurrence(filePath, timestamp, BookmarkService.TimestampPlaceholder),
            WorldName = _worldDataService.Name,
            WorldAlternativeName = _worldDataService.AlternativeName,
            WorldRegionName = regionName,
            WorldTimestamps = [timestamp],
            WorldWidth = _worldDataService.Width,
            WorldHeight = _worldDataService.Height,
            WorldMapImage = imageData,
            State = BookmarkState.Loaded,
            LoadedTimestamp = timestamp,
            LatestTimestamp = timestamp
        };

        return _bookmarkService.AddBookmark(bookmark);
    }
}
