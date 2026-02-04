using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegendsViewer.Backend.Legends.Translations;

public class DwarvenDictionary : IDwarvenDictionary
{
    private const string DefaultRelativePath = "Data/dwarven-dictionary.json";
    private const string RootMapFileName = "english-root-map.json";
    private readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> _entries;
    private readonly IReadOnlyDictionary<string, string> _rootMap;

    public DwarvenDictionary(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<DwarvenDictionary> logger)
    {
        var configuredPath = configuration["DwarvenDictionary:Path"];
        var dictionaryPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(hostEnvironment.ContentRootPath, DefaultRelativePath)
            : Path.GetFullPath(configuredPath);
        var rootMapPath = Path.Combine(Path.GetDirectoryName(dictionaryPath) ?? string.Empty, RootMapFileName);

        if (!File.Exists(dictionaryPath))
        {
            logger.LogWarning("Dwarven dictionary file not found at {Path}. Search aliases will be unavailable until the file is generated.", dictionaryPath);
            _entries = CreateReadOnlyDictionary(new Dictionary<string, IReadOnlyCollection<string>>());
            _rootMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        try
        {
            using var stream = File.OpenRead(dictionaryPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Dictionary file at '{dictionaryPath}' must contain a JSON object.");
            }

            var comparer = StringComparer.OrdinalIgnoreCase;
            var rawEntries = new Dictionary<string, IReadOnlyCollection<string>>(comparer);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var translations = property.Value
                    .EnumerateArray()
                    .Select(element => element.ValueKind == JsonValueKind.String ? element.GetString() : null)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                rawEntries[property.Name] = translations;
            }

            _entries = CreateReadOnlyDictionary(rawEntries);
            logger.LogInformation("Loaded {EntryCount} dwarven translations from {Path}", _entries.Count, dictionaryPath);

            _rootMap = LoadRootMap(rootMapPath, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load dwarven dictionary from {Path}.", dictionaryPath);
            _entries = CreateReadOnlyDictionary(new Dictionary<string, IReadOnlyCollection<string>>());
            _rootMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Entries => _entries;

    public bool TryGetTranslations(string englishWord, out IReadOnlyCollection<string> translations)
    {
        if (string.IsNullOrWhiteSpace(englishWord))
        {
            translations = Array.Empty<string>();
            return false;
        }

        if (_entries.TryGetValue(englishWord, out translations!))
        {
            return true;
        }

        if (_rootMap.TryGetValue(englishWord, out var root) && _entries.TryGetValue(root, out translations!))
        {
            return true;
        }

        translations = Array.Empty<string>();
        return false;
    }

    private static IReadOnlyDictionary<string, string> LoadRootMap(string path, ILogger logger)
    {
        if (!File.Exists(path))
        {
            logger.LogDebug("English root map not found at {Path}. Inflected forms will not be resolved.", path);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var comparer = StringComparer.OrdinalIgnoreCase;
            var map = new Dictionary<string, string>(comparer);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    map[property.Name] = value;
                }
            }
            logger.LogInformation("Loaded {Count} root map entries from {Path}", map.Count, path);
            return map;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load root map from {Path}. Inflected forms will not be resolved.", path);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> CreateReadOnlyDictionary(
        IDictionary<string, IReadOnlyCollection<string>> entries)
    {
        return new ReadOnlyDictionary<string, IReadOnlyCollection<string>>(
            new Dictionary<string, IReadOnlyCollection<string>>(entries, StringComparer.OrdinalIgnoreCase));
    }
}
