namespace LegendsViewer.Backend.Legends.Bookmarks;

using LegendsViewer.Backend.Controllers;
using LegendsViewer.Backend.Legends.Interfaces;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;

public class BookmarkService : IBookmarkService
{
    public const string TimestampPlaceholder = "{TIMESTAMP}";
    private const string BookmarkFileName = "bookmarks.json";

    private readonly Dictionary<string, Bookmark> _bookmarks;
    private readonly string _bookmarkFilePath;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

    public BookmarkService(IConfiguration configuration)
    {
        // Use data directory for persistent storage (works with Docker volumes)
        string dataDirectory = configuration["DataDirectory"] ?? "/app/data";
        Directory.CreateDirectory(dataDirectory); // Ensure directory exists

        _bookmarkFilePath = Path.Combine(dataDirectory, BookmarkFileName);

        // Load bookmarks from disk if the file exists
        if (File.Exists(_bookmarkFilePath))
        {
            string json = File.ReadAllText(_bookmarkFilePath);
            _bookmarks = JsonSerializer.Deserialize<Dictionary<string, Bookmark>>(json) ?? [];
            foreach (var bookmark in _bookmarks.Values)
            {
                ResetBookmark(bookmark);
            }
        }
        else
        {
            _bookmarks = [];
        }
    }

    private static void ResetBookmark(Bookmark bookmark)
    {
        bookmark.State = BookmarkState.Default;
        bookmark.LoadedTimestamp = null;
        bookmark.LatestTimestamp = bookmark.WorldTimestamps.Order().LastOrDefault();
    }

    public Bookmark AddBookmark(Bookmark bookmark)
    {
        ResetAllBookmarks();
        if (_bookmarks.TryGetValue(bookmark.FilePath, out var existingBookmark))
        {
            foreach (var timestamp in bookmark.WorldTimestamps)
            {
                if (!existingBookmark.WorldTimestamps.Contains(timestamp))
                {
                    existingBookmark.WorldTimestamps.Add(timestamp);
                }
            }
            existingBookmark.WorldName = bookmark.WorldName;
            existingBookmark.WorldAlternativeName = bookmark.WorldAlternativeName;
            existingBookmark.WorldMapImage = bookmark.WorldMapImage;
            existingBookmark.State = BookmarkState.Loaded;
            existingBookmark.LoadedTimestamp = bookmark.LoadedTimestamp;
            existingBookmark.LatestTimestamp = bookmark.LatestTimestamp;
            SaveBookmarksToFile();
            return existingBookmark;
        }
        else
        {
            _bookmarks[bookmark.FilePath] = bookmark;
            SaveBookmarksToFile();
            return bookmark;
        }
    }

    private void ResetAllBookmarks()
    {
        foreach (var bookmark in _bookmarks.Values) { ResetBookmark(bookmark); }
    }

    public List<Bookmark> GetAll()
    {
        return [.. _bookmarks.Values];
    }

    public Bookmark? GetBookmark(string filePath)
    {
        string timestamp = string.Empty;
        if (filePath.Contains(BookmarkController.FileIdentifierLegendsXml))
        {
            var fileName = Path.GetFileName(filePath);
            string regionId = fileName.Replace(BookmarkController.FileIdentifierLegendsXml, "");
            var (_, Timestamp) = GetRegionNameAndTimestampByRegionId(regionId);
            timestamp = Timestamp;
        }
        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return null;
        }
        return _bookmarks.TryGetValue(filePath.Replace(timestamp, TimestampPlaceholder), out var bookmark) ? bookmark : null;
    }

    public bool DeleteBookmarkTimestamp(string filePath)
    {
        string timestamp = string.Empty;
        if (filePath.Contains(BookmarkController.FileIdentifierLegendsXml))
        {
            var fileName = Path.GetFileName(filePath);
            string regionId = fileName.Replace(BookmarkController.FileIdentifierLegendsXml, "");
            var (_, Timestamp) = GetRegionNameAndTimestampByRegionId(regionId);
            timestamp = Timestamp;
        }
        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return false;
        }

        string key = ReplaceLastOccurrence(filePath, timestamp, TimestampPlaceholder);
        if (!_bookmarks.TryGetValue(key, out var bookmark))
        {
            return false;
        }
        bookmark.WorldTimestamps.Remove(timestamp);
        if (bookmark.WorldTimestamps.Count == 0)
        {
            _bookmarks.Remove(key);
        }
        else
        {
            bookmark.LatestTimestamp = bookmark.WorldTimestamps.Order().LastOrDefault();
        }
        SaveBookmarksToFile();
        return true;
    }

    public bool DeleteBookmark(string filePath)
    {
        // Try exact match first
        if (_bookmarks.ContainsKey(filePath))
        {
            _bookmarks.Remove(filePath);
            SaveBookmarksToFile();
            return true;
        }

        // Extract filename from path if it's a full path
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = filePath;
        }

        // Extract timestamp from filename if it contains a timestamp
        string timestamp = string.Empty;
        string regionId = string.Empty;
        
        if (fileName.Contains(BookmarkController.FileIdentifierLegendsXml))
        {
            regionId = fileName.Replace(BookmarkController.FileIdentifierLegendsXml, "");
            var (_, Timestamp) = GetRegionNameAndTimestampByRegionId(regionId);
            timestamp = Timestamp;
        }
        else if (fileName.Contains(BookmarkController.FileIdentifierLegendsPlusXml))
        {
            regionId = fileName.Replace(BookmarkController.FileIdentifierLegendsPlusXml, "");
            var (_, Timestamp) = GetRegionNameAndTimestampByRegionId(regionId);
            timestamp = Timestamp;
        }

        if (!string.IsNullOrWhiteSpace(timestamp))
        {
            // Replace timestamp with placeholder to match bookmark key format
            string keyWithPlaceholder = ReplaceLastOccurrence(filePath, timestamp, TimestampPlaceholder);
            if (_bookmarks.ContainsKey(keyWithPlaceholder))
            {
                _bookmarks.Remove(keyWithPlaceholder);
                SaveBookmarksToFile();
                return true;
            }

            // Also try with just the filename
            string fileNameWithPlaceholder = ReplaceLastOccurrence(fileName, timestamp, TimestampPlaceholder);
            if (_bookmarks.ContainsKey(fileNameWithPlaceholder))
            {
                _bookmarks.Remove(fileNameWithPlaceholder);
                SaveBookmarksToFile();
                return true;
            }

            // Try matching by region name (in case the path format is different)
            var (regionName, _) = GetRegionNameAndTimestampByRegionId(regionId);
            if (!string.IsNullOrWhiteSpace(regionName))
            {
                return DeleteBookmarkByRegionId(regionId);
            }
        }

        return false;
    }

    public bool DeleteBookmarkByRegionId(string regionId)
    {
        // Extract region name from regionId (remove timestamp: year-month-day)
        var (regionName, _) = GetRegionNameAndTimestampByRegionId(regionId);
        if (string.IsNullOrWhiteSpace(regionName))
        {
            return false;
        }

        // Find bookmark by matching WorldRegionName (most reliable method)
        // Bookmarks have WorldRegionName field that stores the region name
        var bookmarkKey = _bookmarks.Keys.FirstOrDefault(key =>
        {
            if (_bookmarks.TryGetValue(key, out var bookmark))
            {
                return bookmark.WorldRegionName == regionName;
            }
            return false;
        });

        if (bookmarkKey != null)
        {
            _bookmarks.Remove(bookmarkKey);
            SaveBookmarksToFile();
            return true;
        }

        return false;
    }

    private void SaveBookmarksToFile()
    {
        string json = JsonSerializer.Serialize(_bookmarks, _jsonSerializerOptions);
        File.WriteAllText(_bookmarkFilePath, json);
    }

    public static string ReplaceLastOccurrence(string source, string find, string replace)
    {
        int lastIndex = source.LastIndexOf(find);

        if (lastIndex == -1)
        {
            return source; // The string to replace was not found
        }

        return source.Remove(lastIndex, find.Length).Insert(lastIndex, replace);
    }

    public static (string RegionName, string Timestamp) GetRegionNameAndTimestampByRegionId(string regionId, IWorld? worldDataService = null)
    {
        var array = regionId.Split('-').ToList();
        if (array.Count < 4)
        {
            return ("", "");
        }
        string day = array[^1];
        array.RemoveAt(array.Count - 1);
        string month = array[^1];
        array.RemoveAt(array.Count - 1);
        string year = array[^1];
        array.RemoveAt(array.Count - 1);

        if (worldDataService != null)
        {
            worldDataService.CurrentDay = int.Parse(day);
            worldDataService.CurrentMonth = int.Parse(month);
            worldDataService.CurrentYear = int.Parse(year);
        }

        var regionName = string.Join('-', array);
        var timestamp = $"{year}-{month}-{day}";
        return (regionName, timestamp);
    }
}
