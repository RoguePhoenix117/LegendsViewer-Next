using System;
using System.Collections.Generic;
using System.Linq;
using LegendsViewer.Backend.Utilities;

namespace LegendsViewer.Backend.Legends.Translations;

public static class SearchAliasGenerator
{
    private static readonly char[] TokenDelimiters = [' ', '-', '_', '\'', '"'];

    public static (IReadOnlyCollection<string> Aliases, string? DwarvenDisplay) BuildAliases(string? name, IDwarvenDictionary? dictionary)
    {
        HashSet<string> aliases = new(StringComparer.Ordinal);

        var normalizedBase = Formatting.NormalizeForSearch(name);
        if (!string.IsNullOrEmpty(normalizedBase))
        {
            aliases.Add(normalizedBase);
        }

        string? dwarvenDisplay = null;
        if (dictionary != null)
        {
            var dwarven = TranslateToDwarven(name, dictionary);
            if (!string.IsNullOrEmpty(dwarven))
            {
                dwarvenDisplay = dwarven;
                var normalizedDwarven = Formatting.NormalizeForSearch(dwarven);
                if (!string.IsNullOrEmpty(normalizedDwarven))
                {
                    aliases.Add(normalizedDwarven);
                }
            }
        }

        return (aliases.ToList(), dwarvenDisplay);
    }

    private static string? TranslateToDwarven(string? name, IDwarvenDictionary dictionary)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var tokens = Tokenize(name);
        if (tokens.Count == 0)
        {
            return null;
        }

        var translatedTokens = new List<string>(tokens.Count);
        var anyTranslated = false;
        var i = 0;

        while (i < tokens.Count)
        {
            var token = tokens[i];

            if (IsDeterminer(token) && i + 4 <= tokens.Count &&
                IsOf(tokens[i + 2]) &&
                TryTranslateToken(tokens[i + 1], dictionary, out var xTrans) &&
                TryTranslateToken(tokens[i + 3], dictionary, out var yTrans))
            {
                translatedTokens.Add(xTrans + " " + yTrans);
                anyTranslated = true;
                i += 4;
                continue;
            }

            if (!IsDeterminer(token) && i + 3 <= tokens.Count &&
                IsOf(tokens[i + 1]) &&
                TryTranslateToken(token, dictionary, out var xTrans2) &&
                TryTranslateToken(tokens[i + 2], dictionary, out var yTrans2))
            {
                translatedTokens.Add(xTrans2 + " " + yTrans2);
                anyTranslated = true;
                i += 3;
                continue;
            }

            if (IsDeterminer(token) && i + 3 <= tokens.Count &&
                TryTranslateToken(tokens[i + 1], dictionary, out var adjTrans) &&
                TryTranslateToken(tokens[i + 2], dictionary, out var nounTrans))
            {
                translatedTokens.Add(adjTrans + nounTrans);
                anyTranslated = true;
                i += 3;
                continue;
            }

            if (TryTranslateToken(token, dictionary, out var translated))
            {
                translatedTokens.Add(translated);
                anyTranslated = true;
            }
            else
            {
                translatedTokens.Add(token);
            }

            i++;
        }

        return anyTranslated ? string.Join(' ', translatedTokens) : null;
    }

    private static bool IsDeterminer(string token) =>
        token.Equals("the", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("a", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("an", StringComparison.OrdinalIgnoreCase);

    private static bool IsOf(string token) =>
        token.Equals("of", StringComparison.OrdinalIgnoreCase);

    private static bool TryTranslateToken(string token, IDwarvenDictionary dictionary, out string translated)
    {
        translated = string.Empty;

        if (dictionary.TryGetTranslations(token, out var translations) && translations.Count > 0)
        {
            translated = translations.First();
            return true;
        }

        if (TryTranslateCompound(token, dictionary, out translated))
        {
            return true;
        }

        return false;
    }

    private static bool TryTranslateCompound(string token, IDwarvenDictionary dictionary, out string translated)
    {
        translated = string.Empty;
        if (token.Length < 4)
        {
            return false;
        }

        for (var split = 2; split <= token.Length - 2; split++)
        {
            var left = token[..split];
            var right = token[split..];

            if (!dictionary.TryGetTranslations(left, out var leftTrans) || leftTrans.Count == 0)
            {
                continue;
            }

            if (dictionary.TryGetTranslations(right, out var rightTrans) && rightTrans.Count > 0)
            {
                translated = leftTrans.First() + rightTrans.First();
                return true;
            }

            if (TryTranslateCompound(right, dictionary, out var rightTranslated))
            {
                translated = leftTrans.First() + rightTranslated;
                return true;
            }
        }

        return false;
    }

    private static List<string> Tokenize(string value)
    {
        return value
            .Split(TokenDelimiters, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .ToList();
    }
}
