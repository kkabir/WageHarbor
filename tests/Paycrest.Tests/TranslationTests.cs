using System.Text.Json;
using System.Text.RegularExpressions;
using Paycrest.Services.Localization;

namespace Paycrest.Tests;

/// <summary>
/// Every language file must translate every screen string, and keep its {0} placeholders and HTML tags,
/// or a page would show English in the middle of another language, or break.
/// </summary>
public partial class TranslationTests
{
    private static readonly string LangFolder = Path.Combine(FindRepoRoot(), "src", "Paycrest", "Resources", "Lang");

    /// <summary>Found from this source file's location, so it works wherever the tests are built to.</summary>
    private static string FindRepoRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
    {
        foreach (var start in new[] { Path.GetDirectoryName(thisFile), AppContext.BaseDirectory })
            for (var dir = string.IsNullOrEmpty(start) ? null : new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (Directory.Exists(Path.Combine(dir.FullName, "src", "Paycrest"))) return dir.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static List<string> Keys() =>
        JsonSerializer.Deserialize<List<string>>(File.ReadAllText(Path.Combine(LangFolder, "_keys.json")))!;

    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (var l in Lang.All.Skip(1)) data.Add(l.Code);
        return data;
    }

    [Theory, MemberData(nameof(Languages))]
    public void Every_language_has_a_file_with_every_string(string code)
    {
        var file = Path.Combine(LangFolder, code + ".json");
        Assert.True(File.Exists(file), $"{code}.json is missing.");
        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))!;
        var missing = Keys().Where(k => !map.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v)).ToList();
        Assert.True(missing.Count == 0, $"{code}.json is missing {missing.Count} string(s), e.g. \"{missing.FirstOrDefault()}\"");
    }

    [Theory, MemberData(nameof(Languages))]
    public void Translations_keep_placeholders_and_tags(string code)
    {
        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(LangFolder, code + ".json")))!;
        var problems = new List<string>();
        foreach (var (english, translated) in map)
        {
            if (!Same(Placeholder(), english, translated)) problems.Add($"placeholders differ: \"{english}\"");
            if (!Same(Tag(), english, translated)) problems.Add($"HTML tags differ: \"{english}\"");
        }
        Assert.True(problems.Count == 0, $"{code}.json: {string.Join("; ", problems.Take(5))}");
    }

    [Fact]
    public void Money_stays_in_dollars_and_dates_stay_gregorian_in_every_language()
    {
        foreach (var language in Lang.All)
        {
            var culture = Lang.CultureFor(language);
            Assert.Equal("$1,234.50", 1234.5m.ToString("C2", culture));
            Assert.IsType<System.Globalization.GregorianCalendar>(culture.DateTimeFormat.Calendar);
            Assert.Equal(2026, new DateTime(2026, 11, 16).ToString("yyyy", culture) is var y ? int.Parse(y) : 0);
        }
    }

    private static bool Same(Regex pattern, string a, string b) =>
        pattern.Matches(a).Select(m => m.Value).Order().SequenceEqual(pattern.Matches(b).Select(m => m.Value).Order());

    [GeneratedRegex(@"\{\d+\}")] private static partial Regex Placeholder();
    [GeneratedRegex(@"</?[a-z]+\b[^>]*>")] private static partial Regex Tag();
}
