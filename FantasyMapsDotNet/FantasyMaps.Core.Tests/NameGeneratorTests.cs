using FantasyMaps.Core.Language;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class NameGeneratorTests
{
    [Fact]
    public void MakeName_ReturnsStringWithinLengthBounds()
    {
        var lang = LanguageFactory.MakeRandomLanguage();
        for (int i = 0; i < 20; i++)
        {
            string name = NameGenerator.MakeName(lang, $"key{i}");
            Assert.True(name.Length >= 3 && name.Length <= 20,
                $"Name '{name}' is outside expected length range");
        }
    }

    [Fact]
    public void MakeName_SameKeyReturnsSameName()
    {
        var lang = LanguageFactory.MakeRandomLanguage();
        string name1 = NameGenerator.MakeName(lang, "city0");
        string name2 = NameGenerator.MakeName(lang, "city0");
        Assert.Equal(name1, name2);
    }
}
