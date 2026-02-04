using System;
using System.Collections.Generic;
using System.Linq;
using LegendsViewer.Backend.Legends.Translations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendsViewer.Backend.Tests.Legends.Translations;

[TestClass]
public class SearchAliasGeneratorTests
{
    [TestMethod]
    public void BuildAliases_AddsNormalizedEnglishAndDwarvenNames()
    {
        var dictionary = new StubDictionary(new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "vomit", new List<string> { "ôggon" } }
        });

        var (aliases, dwarvenDisplay) = SearchAliasGenerator.BuildAliases("The Vomit", dictionary);

        CollectionAssert.Contains(aliases.ToList(), "the vomit");
        CollectionAssert.Contains(aliases.ToList(), "the oggon");
        Assert.AreEqual("The ôggon", dwarvenDisplay);
    }

    [TestMethod]
    public void BuildAliases_ReturnsEnglishOnlyWhenNoTranslationsExist()
    {
        var (aliases, dwarvenDisplay) = SearchAliasGenerator.BuildAliases("Forgotten Beast", dictionary: null);

        Assert.AreEqual(1, aliases.Count);
        Assert.AreEqual("forgotten beast", aliases.First());
        Assert.IsNull(dwarvenDisplay);
    }

    [TestMethod]
    public void BuildAliases_TranslatesCompoundWordsBySplitting()
    {
        var dictionary = new StubDictionary(new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "frigid", new List<string> { "ishol" } },
            { "heart", new List<string> { "zanor" } }
        });

        var (aliases, dwarvenDisplay) = SearchAliasGenerator.BuildAliases("Frigidheart", dictionary);

        CollectionAssert.Contains(aliases.ToList(), "frigidheart");
        CollectionAssert.Contains(aliases.ToList(), "isholzanor");
        Assert.AreEqual("isholzanor", dwarvenDisplay);
    }

    [TestMethod]
    public void BuildAliases_TranslatesTheAdjNounAsConcatenatedCompound()
    {
        var dictionary = new StubDictionary(new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "gored", new List<string> { "ugzol" } },
            { "scars", new List<string> { "urrith" } },
            { "disgusting", new List<string> { "shungmag" } },
            { "shadows", new List<string> { "shedim" } }
        });

        var (aliases, dwarvenDisplay) = SearchAliasGenerator.BuildAliases("Goredscars the Disgusting Shadows", dictionary);

        CollectionAssert.Contains(aliases.ToList(), "goredscars the disgusting shadows");
        Assert.IsTrue(aliases.Any(a => a.Contains("ugzolurrith")), "Expected alias containing compound translation of Goredscars");
        Assert.IsTrue(aliases.Any(a => a.Contains("shungmagshedim")), "Expected alias containing concatenated adj+noun translation");
        Assert.IsTrue(dwarvenDisplay!.Contains("ugzolurrith") && dwarvenDisplay.Contains("shungmagshedim"));
    }

    [TestMethod]
    public void BuildAliases_TranslatesTheXOfY_DropsTheAndOf()
    {
        var dictionary = new StubDictionary(new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "scar", new List<string> { "urrïth" } },
            { "froths", new List<string> { "gulgun" } }
        });

        var (aliases, dwarvenDisplay) = SearchAliasGenerator.BuildAliases("The Scar of Froths", dictionary);

        CollectionAssert.Contains(aliases.ToList(), "the scar of froths");
        Assert.IsTrue(aliases.Any(a => a.Contains("urrith")), "Expected alias with normalized translation");
        Assert.AreEqual("urrïth gulgun", dwarvenDisplay);
    }

    private sealed class StubDictionary : IDwarvenDictionary
    {
        public StubDictionary(Dictionary<string, IReadOnlyCollection<string>> entries)
        {
            Entries = entries;
        }

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Entries { get; }

        public bool TryGetTranslations(string englishWord, out IReadOnlyCollection<string> translations)
        {
            if (Entries.TryGetValue(englishWord, out translations!))
            {
                return true;
            }

            translations = Array.Empty<string>();
            return false;
        }
    }
}
