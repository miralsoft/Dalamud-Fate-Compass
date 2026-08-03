using FateHelper.Core.Localization;

namespace FateHelper.Core.Tests;

public sealed class LocalizationTests
{
    private static Localizer WithCatalogs()
    {
        var localizer = new Localizer();
        foreach (var catalog in EmbeddedCatalogs.LoadAll())
        {
            localizer.Register(catalog);
        }

        return localizer;
    }

    [Fact]
    public void BothShippedLanguagesLoad()
    {
        var codes = EmbeddedCatalogs.LoadAll().Select(catalog => catalog.Code).ToList();

        Assert.Contains("en", codes);
        Assert.Contains("de", codes);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    public void EveryDeclaredKeyExistsInEveryLanguage(string code)
    {
        var catalog = EmbeddedCatalogs.LoadAll().Single(entry => entry.Code == code);

        var missing = StringKeys.All.Where(key => !catalog.TryGet(key, out _)).ToList();

        Assert.True(
            missing.Count == 0,
            $"Language '{code}' is missing {missing.Count} key(s): {string.Join(", ", missing)}");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    public void NoLanguageDefinesKeysThatAreNotDeclared(string code)
    {
        // Catches a key that was renamed in StringKeys but left behind in a catalogue, which
        // would otherwise sit there as dead weight (C-04).
        var catalog = EmbeddedCatalogs.LoadAll().Single(entry => entry.Code == code);
        var declared = StringKeys.All.ToHashSet(StringComparer.Ordinal);

        var stray = catalog.Keys.Where(key => !declared.Contains(key)).ToList();

        Assert.True(stray.Count == 0, $"Language '{code}' defines unknown key(s): {string.Join(", ", stray)}");
    }

    [Fact]
    public void GermanIsNotJustACopyOfEnglish()
    {
        var localizer = WithCatalogs();

        localizer.Use("en");
        var english = localizer.Get(StringKeys.ListRemaining);

        localizer.Use("de");
        var german = localizer.Get(StringKeys.ListRemaining);

        Assert.NotEqual(english, german);
        Assert.Equal("Restzeit", german);
    }

    [Fact]
    public void MissingKeyFallsBackToEnglish()
    {
        var localizer = new Localizer();
        localizer.Register(new DictionaryCatalog("en", new Dictionary<string, string>
        {
            ["only.in.english"] = "English text",
        }));
        localizer.Register(new DictionaryCatalog("de", new Dictionary<string, string>()));

        localizer.Use("de");

        Assert.Equal("English text", localizer.Get("only.in.english"));
    }

    [Fact]
    public void UnknownKeyReturnsTheKeyItselfRatherThanBlank()
    {
        var localizer = WithCatalogs();

        // Showing the raw key makes the gap obvious in the window instead of leaving an empty
        // row that looks like a bug elsewhere.
        Assert.Equal("no.such.key", localizer.Get("no.such.key"));
    }

    [Fact]
    public void UnknownLanguageFallsBackInsteadOfEmptyingTheUi()
    {
        var localizer = WithCatalogs();

        localizer.Use("klingon");

        Assert.Equal("en", localizer.ActiveCode);
        Assert.Equal("Time left", localizer.Get(StringKeys.ListRemaining));
    }

    [Fact]
    public void FormatSubstitutesArguments()
    {
        var localizer = new Localizer();
        localizer.Register(new DictionaryCatalog("en", new Dictionary<string, string>
        {
            ["greet"] = "Teleport to {0}.",
        }));

        Assert.Equal("Teleport to Limsa.", localizer.Format("greet", "Limsa"));
    }

    [Fact]
    public void ABrokenFormatStringDoesNotThrow()
    {
        var localizer = new Localizer();
        localizer.Register(new DictionaryCatalog("en", new Dictionary<string, string>
        {
            ["broken"] = "Value: {0} and {7}",
        }));

        // A bad translation must not take the window down with it.
        Assert.Equal("Value: {0} and {7}", localizer.Format("broken", "x"));
    }

    [Fact]
    public void MissingKeysReportIsEmptyForTheShippedLanguages()
    {
        var localizer = WithCatalogs();

        Assert.Empty(localizer.MissingKeys("de"));
    }

    [Theory]
    [InlineData("auto", "de", "de")]
    [InlineData("auto", "de-DE", "de")]
    [InlineData("auto", "DE", "de")]
    [InlineData("auto", "en", "en")]
    [InlineData("auto", "fr", "en")]
    [InlineData("auto", null, "en")]
    [InlineData("auto", "", "en")]
    [InlineData("de", "en", "de")]
    [InlineData("en", "de", "en")]
    [InlineData("klingon", "de", "en")]
    [InlineData(null, "de", "de")]
    public void ResolverPicksTheRightLanguage(string? configured, string? clientLanguage, string expected)
    {
        string[] available = ["en", "de"];

        Assert.Equal(expected, LanguageResolver.Resolve(configured, clientLanguage, available));
    }

    [Fact]
    public void ResolverFallsBackWhenNothingIsLoaded()
    {
        Assert.Equal("en", LanguageResolver.Resolve("de", "de", []));
    }

    [Fact]
    public void ResolverUsesWhateverExistsWhenEnglishIsAbsent()
    {
        Assert.Equal("fr", LanguageResolver.Resolve("klingon", "klingon", ["fr"]));
    }
}
