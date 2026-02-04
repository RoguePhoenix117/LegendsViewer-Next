using System.Collections.Generic;

namespace LegendsViewer.Backend.Legends.Translations;

public interface IDwarvenDictionary
{
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Entries { get; }

    bool TryGetTranslations(string englishWord, out IReadOnlyCollection<string> translations);
}
