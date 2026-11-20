using System.Text.RegularExpressions;
using Paycrest.Services;

namespace Paycrest.Tests;

/// <summary>The sitemap, IndexNow and each page's search tags must agree on which pages exist and which are translated.</summary>
public partial class SeoTests
{
    private static string Components([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "src", "Paycrest", "Components");

    private static List<(string File, string Path, bool Translated)> PagesWithSeoTags() =>
        Directory.EnumerateFiles(Components(), "*.razor", SearchOption.AllDirectories)
            .Select(f => (File: f, Match: SeoTag().Match(File.ReadAllText(f))))
            .Where(x => x.Match.Success)
            .Select(x => (Path.GetFileName(x.File), x.Match.Groups["path"].Value, x.Match.Value.Contains("Translated=\"true\"")))
            .ToList();

    [Fact]
    public void Every_page_with_search_tags_is_in_the_sitemap_with_the_right_languages()
    {
        var pages = PagesWithSeoTags();
        Assert.NotEmpty(pages);
        foreach (var (file, path, translated) in pages)
        {
            Assert.True(SearchEngines.PublicPaths.Contains(path), $"{file} ({path}) has SeoTags but isn't in SearchEngines' page lists.");
            Assert.True(translated == SearchEngines.TranslatedPaths.Contains(path),
                $"{file} ({path}): Translated on SeoTags and SearchEngines.TranslatedPaths disagree.");
        }
        foreach (var path in SearchEngines.PublicPaths)
            Assert.True(pages.Any(p => p.Path == path), $"{path} is in the sitemap but no page gives it SeoTags.");
    }

    [Fact]
    public void Indexnow_lists_every_language_of_translated_pages()
    {
        var urls = SearchEngines.AllUrls("https://x.test").ToList();
        Assert.Contains("https://x.test/pricing", urls);
        Assert.Contains("https://x.test/pricing?lang=es", urls);
        Assert.Contains("https://x.test/help/articles/first-payroll", urls);
        Assert.DoesNotContain("https://x.test/help/articles/first-payroll?lang=es", urls);
        Assert.Equal(urls.Count, urls.Distinct().Count());
    }

    [GeneratedRegex("<SeoTags Path=\"(?<path>[^\"]+)\"[^>]*>")]
    private static partial Regex SeoTag();
}
