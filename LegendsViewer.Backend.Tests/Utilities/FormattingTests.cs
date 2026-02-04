using LegendsViewer.Backend.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendsViewer.Backend.Tests.Utilities;

[TestClass]
public class FormattingTests
{
    [TestMethod]
    public void NormalizeForSearch_RemovesDiacriticsAndCollapsesWhitespace()
    {
        var result = Formatting.NormalizeForSearch("  Ngáthsesh   the\tGreat  ");

        Assert.AreEqual("ngathsesh the great", result);
    }

    [TestMethod]
    public void NormalizeForSearch_EmptyInput_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, Formatting.NormalizeForSearch(null));
        Assert.AreEqual(string.Empty, Formatting.NormalizeForSearch("   "));
    }
}
