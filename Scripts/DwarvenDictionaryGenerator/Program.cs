using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;

var defaultDwarfSource = "https://raw.githubusercontent.com/DF-Wiki/DFRawFunctions/master/raws/v50/language_DWARF.txt";
var repoRoot = FindRepoRoot();
var dataDir = Path.Combine(repoRoot, "LegendsViewer.Backend", "Data");
var defaultDwarfOutput = Path.Combine(dataDir, "dwarven-dictionary.json");
var defaultRootMapOutput = Path.Combine(dataDir, "english-root-map.json");

var arguments = ParseArguments(args);
var dwarfSource = arguments.TryGetValue("source", out var sourceArg) ? sourceArg : defaultDwarfSource;
var wordsSource = arguments.TryGetValue("words-source", out var ws) ? ws : DeriveWordsSource(dwarfSource);
var outputPath = arguments.TryGetValue("output", out var outputArg) ? outputArg : defaultDwarfOutput;
var rootMapOutputPath = arguments.TryGetValue("output-root-map", out var rom) ? rom : defaultRootMapOutput;
var dryRun = arguments.ContainsKey("dry-run");
var validateOnly = arguments.ContainsKey("validate-only") || arguments.ContainsKey("validate");

Console.WriteLine("Dwarven dictionary generator");
Console.WriteLine($"Dwarf source : {dwarfSource}");
Console.WriteLine($"Words source : {wordsSource}");
Console.WriteLine($"Dictionary   : {outputPath}");
Console.WriteLine($"Root map     : {rootMapOutputPath}");
Console.WriteLine(dryRun ? "Mode   : DRY RUN" : validateOnly ? "Mode   : VALIDATE" : "Mode   : WRITE");
Console.WriteLine();

var rawDwarfText = await ReadSourceAsync(dwarfSource);
var translationMap = ParseTranslations(rawDwarfText);
var expectedCount = translationMap.Count;

var rawWordsText = await ReadSourceAsync(wordsSource);
var rootMap = ParseLanguageWords(rawWordsText);

if (translationMap.Count == 0)
{
    Console.WriteLine("No translations were discovered. Aborting.");
    return 1;
}

if (rootMap.Count == 0)
{
    Console.WriteLine("No root map entries were discovered. Aborting.");
    return 1;
}

var json = JsonSerializer.Serialize(translationMap, new JsonSerializerOptions { WriteIndented = true });
var rootMapJson = JsonSerializer.Serialize(rootMap, new JsonSerializerOptions { WriteIndented = true });

Console.WriteLine($"Entries parsed from dwarf source: {expectedCount}");
Console.WriteLine($"Entries parsed from words source: {rootMap.Count}");

int? existingCount = null;
if (File.Exists(outputPath))
{
    existingCount = await CountDictionaryEntriesAsync(outputPath);
    Console.WriteLine($"Entries in existing dictionary: {existingCount}");
}
else if (validateOnly)
{
    Console.WriteLine($"Validation requires an existing dictionary at {outputPath}.");
    return 1;
}

if (validateOnly)
{
    if (existingCount != expectedCount)
    {
        Console.WriteLine($"Validation failed. Expected {expectedCount} entries but found {existingCount}.");
        return 1;
    }

    int? existingRootMapCount = null;
    if (File.Exists(rootMapOutputPath))
    {
        existingRootMapCount = await CountDictionaryEntriesAsync(rootMapOutputPath);
        if (existingRootMapCount != rootMap.Count)
        {
            Console.WriteLine($"Root map validation failed. Expected {rootMap.Count} entries but found {existingRootMapCount}.");
            return 1;
        }
    }

    Console.WriteLine("Validation succeeded. Counts match.");
    return 0;
}

if (dryRun)
{
    Console.WriteLine("Dry run requested; first 500 characters of generated dictionary JSON:");
    Console.WriteLine(json[..Math.Min(json.Length, 500)]);
    Console.WriteLine();
    Console.WriteLine("First 500 characters of generated root map JSON:");
    Console.WriteLine(rootMapJson[..Math.Min(rootMapJson.Length, 500)]);
    return 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
await File.WriteAllTextAsync(outputPath, json);

var writtenCount = await CountDictionaryEntriesAsync(outputPath);
if (writtenCount != expectedCount)
{
    Console.WriteLine($"Warning: wrote dictionary but counts mismatch (expected {expectedCount}, file has {writtenCount}).");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(rootMapOutputPath)!);
await File.WriteAllTextAsync(rootMapOutputPath, rootMapJson);

var writtenRootMapCount = await CountDictionaryEntriesAsync(rootMapOutputPath);
if (writtenRootMapCount != rootMap.Count)
{
    Console.WriteLine($"Warning: wrote root map but counts mismatch (expected {rootMap.Count}, file has {writtenRootMapCount}).");
    return 1;
}

Console.WriteLine($"Wrote {expectedCount} entries to {outputPath}");
Console.WriteLine($"Wrote {rootMap.Count} entries to {rootMapOutputPath}");
return 0;

static async Task<string> ReadSourceAsync(string source)
{
    if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
    {
        using var client = new HttpClient();
        return await client.GetStringAsync(uri);
    }

    var fullPath = Path.GetFullPath(source);
    if (!File.Exists(fullPath))
    {
        throw new FileNotFoundException($"Could not find source file at '{fullPath}'");
    }

    return await File.ReadAllTextAsync(fullPath);
}

static bool IsWordFormTag(string tag)
{
    return tag.Equals("NOUN", StringComparison.OrdinalIgnoreCase)
        || tag.Equals("VERB", StringComparison.OrdinalIgnoreCase)
        || tag.Equals("ADJ", StringComparison.OrdinalIgnoreCase)
        || tag.Equals("PREFIX", StringComparison.OrdinalIgnoreCase);
}

static string DeriveWordsSource(string dwarfSource)
{
    const string dwarfFileName = "language_DWARF.txt";
    const string wordsFileName = "language_words.txt";

    if (dwarfSource.EndsWith(dwarfFileName, StringComparison.OrdinalIgnoreCase))
    {
        return dwarfSource[..^dwarfFileName.Length] + wordsFileName;
    }

    var dir = Path.GetDirectoryName(dwarfSource);
    return string.IsNullOrEmpty(dir) ? wordsFileName : Path.Combine(dir, wordsFileName);
}

static SortedDictionary<string, string> ParseLanguageWords(string rawText)
{
    var comparer = StringComparer.OrdinalIgnoreCase;
    var result = new SortedDictionary<string, string>(comparer);
    string? currentRoot = null;

    foreach (var line in rawText.Split('\n'))
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            continue;
        }

        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
        {
            continue;
        }

        var inner = trimmed.TrimStart('[').TrimEnd(']');
        var colonIndex = inner.IndexOf(':');
        if (colonIndex < 0)
        {
            continue;
        }

        var tag = inner[..colonIndex];
        var payload = inner[(colonIndex + 1)..];

        if (tag.Equals("WORD", StringComparison.OrdinalIgnoreCase))
        {
            currentRoot = payload.Trim();
            continue;
        }

        if (string.IsNullOrEmpty(currentRoot) || !IsWordFormTag(tag))
        {
            continue;
        }

        var forms = payload.Split(':');
        foreach (var form in forms)
        {
            var f = form.Trim();
            if (f.Length > 0 && !char.IsDigit(f[0]) && !result.ContainsKey(f))
            {
                result[f] = currentRoot;
            }
        }
    }

    return result;
}

static SortedDictionary<string, List<string>> ParseTranslations(string rawText)
{
    var comparer = StringComparer.OrdinalIgnoreCase;
    var result = new SortedDictionary<string, List<string>>(comparer);
    var buffer = new SortedDictionary<string, SortedSet<string>>(comparer);

    foreach (var line in rawText.Split('\n'))
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith("[T_WORD:", StringComparison.Ordinal))
        {
            continue;
        }

        var inner = trimmed.TrimStart('[').TrimEnd(']');
        var parts = inner.Split(':', StringSplitOptions.None);
        if (parts.Length < 3 || !parts[0].Equals("T_WORD", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var english = parts[1].Trim();
        var dwarven = parts[2].Trim();

        if (english.Length == 0 || dwarven.Length == 0)
        {
            continue;
        }

        if (!buffer.TryGetValue(english, out var translations))
        {
            translations = new SortedSet<string>(StringComparer.Ordinal);
            buffer[english] = translations;
        }

        translations.Add(dwarven);
    }

    foreach (var (english, translations) in buffer)
    {
        result[english] = translations.ToList();
    }

    return result;
}

static Dictionary<string, string> ParseArguments(string[] args)
{
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i < args.Length; i++)
    {
        var current = args[i];
        if (!current.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = current[2..];
        var value = string.Empty;

        if ((i + 1) < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = args[++i];
        }

        dict[key] = value;
    }

    return dict;
}

static async Task<int> CountDictionaryEntriesAsync(string path)
{
    await using var stream = File.OpenRead(path);
    using var document = await JsonDocument.ParseAsync(stream);
    if (document.RootElement.ValueKind != JsonValueKind.Object)
    {
        throw new InvalidOperationException("Dictionary JSON must be an object of key/value pairs.");
    }

    return document.RootElement.EnumerateObject().Count();
}

static string FindRepoRoot()
{
    var current = Directory.GetCurrentDirectory();
    while (!string.IsNullOrEmpty(current))
    {
        var solutionPath = Path.Combine(current, "LegendsViewer.sln");
        if (File.Exists(solutionPath))
        {
            return current;
        }

        var parent = Directory.GetParent(current);
        if (parent == null)
        {
            break;
        }

        current = parent.FullName;
    }

    return Directory.GetCurrentDirectory();
}
