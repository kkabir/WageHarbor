using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace Paycrest.Services;

/// <summary>
/// Helps search engines notice new versions of the site:
/// a sitemap whose dates follow each publish, and an IndexNow ping (Bing, Yandex, Seznam, Naver…) once per new build.
/// Google doesn't take IndexNow; it re-reads the sitemap, which can also be resubmitted in Search Console.
/// </summary>
public static partial class SearchEngines
{
    /// <summary>Public pages worth indexing. Add new public pages here.</summary>
    public static readonly string[] PublicPaths =
    [
        "/", "/pricing", "/about", "/security", "/privacy", "/terms", "/contact",
        "/help", "/help/company", "/help/employee", "/help/articles",
        "/help/articles/first-payroll", "/help/articles/hourly-vs-salary", "/help/articles/read-your-pay-stub",
        "/Account/Register",
    ];

    public static string SiteRoot(IConfiguration config) =>
        config["Site:CanonicalHost"] is { Length: > 0 } host ? $"https://{host}" :
        (config["Site:BaseUrl"] is { Length: > 0 } b ? b : "https://www.wageharbor.com").TrimEnd('/');

    /// <summary>/sitemap.xml, with every page dated to the current build so each publish reads as an update.</summary>
    public static IEndpointRouteBuilder MapSitemap(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap.xml", (HttpContext http, IConfiguration config) =>
        {
            var root = SiteRoot(config);
            var lastmod = BuildInfo.BuiltUtc.ToString("yyyy-MM-dd");
            var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
            foreach (var path in PublicPaths)
                xml.Append($"  <url><loc>{root}{path}</loc><lastmod>{lastmod}</lastmod></url>\n");
            xml.Append("</urlset>\n");
            http.Response.Headers.CacheControl = "public, max-age=3600";
            return Results.Text(xml.ToString(), "application/xml", Encoding.UTF8);
        });
        return app;
    }

    /// <summary>The IndexNow key: Site:IndexNowKey, or the key file (32 hex characters + .txt) in wwwroot.</summary>
    public static string? IndexNowKey(IConfiguration config, IWebHostEnvironment env)
    {
        if (config["Site:IndexNowKey"] is { Length: > 0 } k) return k;
        var root = env.WebRootPath;
        if (root == null || !Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, "*.txt")
            .Select(Path.GetFileNameWithoutExtension)
            .FirstOrDefault(n => n != null && KeyName().IsMatch(n));
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex KeyName();
}

/// <summary>After a new build starts on the live site, tells IndexNow search engines that every public page changed.</summary>
public sealed class IndexNowPinger(IConfiguration config, IWebHostEnvironment env, IHttpClientFactory httpFactory, ILogger<IndexNowPinger> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Only the live site, and only when it knows its public address.
        if (env.IsDevelopment() || string.IsNullOrWhiteSpace(config["Site:CanonicalHost"]) || !config.GetValue("Site:IndexNow", true)) return;
        var key = SearchEngines.IndexNowKey(config, env);
        if (key == null) { logger.LogWarning("IndexNow skipped: no key file (32 hex characters + .txt) in wwwroot."); return; }

        // Wait until the site is answering, and ping once per build even if the app restarts many times.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        var stamp = $"{BuildInfo.Commit ?? "build"}-{BuildInfo.BuiltUtc:yyyyMMddHHmmss}";
        var marker = MarkerPath(stamp);
        if (marker != null && File.Exists(marker)) return;

        var root = SearchEngines.SiteRoot(config);
        var body = new
        {
            host = new Uri(root).Host,
            key,
            keyLocation = $"{root}/{key}.txt",
            urlList = SearchEngines.PublicPaths.Select(p => root + p).ToArray(),
        };
        try
        {
            using var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            using var response = await client.PostAsJsonAsync("https://api.indexnow.org/indexnow", body, stoppingToken);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("IndexNow: told search engines about {Count} pages for {Build} ({Status}).", body.urlList.Length, stamp, (int)response.StatusCode);
                if (marker != null) try { await File.WriteAllTextAsync(marker, DateTime.UtcNow.ToString("o"), stoppingToken); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            else
                logger.LogWarning("IndexNow ping for {Build} was refused: {Status} {Reason}.", stamp, (int)response.StatusCode, await response.Content.ReadAsStringAsync(stoppingToken));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "IndexNow ping for {Build} failed.", stamp);
        }
    }

    /// <summary>A writable place to remember that this build was announced, or null if there is none.</summary>
    private string? MarkerPath(string stamp)
    {
        foreach (var dir in new[] { Path.Combine(env.ContentRootPath, "App_Data"), Path.GetTempPath() })
        {
            try { Directory.CreateDirectory(dir); return Path.Combine(dir, $"indexnow-{stamp}.sent"); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return null;
    }
}
