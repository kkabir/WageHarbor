using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Paycrest.Data;
using Paycrest.Domain;

namespace Paycrest.Services;

/// <summary>Hands page views from requests to <see cref="VisitorLogWriter"/>. Drops views rather than slow a request when full.</summary>
public sealed class VisitorLogQueue
{
    private readonly Channel<PageVisit> _channel =
        Channel.CreateBounded<PageVisit>(new BoundedChannelOptions(5000) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Add(PageVisit v) => _channel.Writer.TryWrite(v);
    public ChannelReader<PageVisit> Reader => _channel.Reader;
}

/// <summary>Records page views (full pages and enhanced navigations) for the platform visitor log.</summary>
public static class VisitorLogMiddleware
{
    private static readonly string[] SkipPrefixes = ["/_blazor", "/_framework", "/_content", "/company-logo", "/Account/Manage/DownloadPersonalData", "/favicon", "/.well-known"];

    // The salt lives only in memory and changes daily, so a stored hash can't be traced back to an IP address.
    private static readonly object SaltLock = new();
    private static DateOnly _saltDay;
    private static byte[] _salt = [];

    public static IApplicationBuilder UseVisitorLog(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        await next();
        try { Record(ctx); } catch { /* never let the visitor log break a page */ }
    });

    private static void Record(HttpContext ctx)
    {
        var req = ctx.Request;
        if (!HttpMethods.IsGet(req.Method)) return;
        // For a 404 the not-found page is re-executed; log the address the visitor actually asked for.
        var path = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>()?.OriginalPath ?? req.Path.Value ?? "/";
        if (SkipPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return;
        if (Path.HasExtension(path)) return; // css, js, images, robots.txt, sitemap.xml
        if (ctx.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) != true) return;

        var ua = req.Headers.UserAgent.ToString();
        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        ctx.RequestServices.GetRequiredService<VisitorLogQueue>().Add(new PageVisit
        {
            TimeUtc = DateTime.UtcNow,
            Path = Trim(path, 400)!,
            Referrer = Trim(CleanReferrer(req.Headers.Referer.ToString(), req.Host.Host), 400),
            VisitorHash = Hash(ctx.Connection.RemoteIpAddress?.ToString() + "|" + ua),
            Device = DeviceOf(ua),
            Browser = BrowserOf(ua),
            Country = req.Headers["CF-IPCountry"].ToString() is { Length: 2 } cc && cc != "XX" ? cc.ToUpperInvariant() : null,
            Language = Trim(req.Headers.AcceptLanguage.ToString().Split(',', ';')[0].Trim(), 10) is { Length: > 0 } l ? l : null,
            StatusCode = ctx.Response.StatusCode,
            SignedIn = userId != null,
        });
    }

    /// <summary>Keeps where people came from, without query strings (which can hold tokens or emails). Same-site referrers are dropped.</summary>
    private static string? CleanReferrer(string referer, string host)
    {
        if (!Uri.TryCreate(referer, UriKind.Absolute, out var uri)) return null;
        if (string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase)) return null;
        return uri.Host + (uri.AbsolutePath == "/" ? "" : uri.AbsolutePath);
    }

    private static string Hash(string value)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        byte[] salt;
        lock (SaltLock)
        {
            if (_saltDay != today) { _saltDay = today; _salt = RandomNumberGenerator.GetBytes(32); }
            salt = _salt;
        }
        return Convert.ToHexString(HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(value)))[..16];
    }

    internal static string DeviceOf(string ua)
    {
        if (ua.Length == 0 || ua.Contains("bot", StringComparison.OrdinalIgnoreCase) || ua.Contains("crawl", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("spider", StringComparison.OrdinalIgnoreCase) || ua.Contains("slurp", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("curl/", StringComparison.OrdinalIgnoreCase) || ua.Contains("python", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("headless", StringComparison.OrdinalIgnoreCase) || ua.Contains("monitor", StringComparison.OrdinalIgnoreCase))
            return "Bot";
        if (ua.Contains("iPad", StringComparison.Ordinal) || (ua.Contains("Android", StringComparison.Ordinal) && !ua.Contains("Mobile", StringComparison.Ordinal)))
            return "Tablet";
        if (ua.Contains("Mobi", StringComparison.Ordinal) || ua.Contains("iPhone", StringComparison.Ordinal)) return "Mobile";
        return "Desktop";
    }

    internal static string? BrowserOf(string ua) =>
        ua.Contains("Edg/") ? "Edge"
        : ua.Contains("OPR/") ? "Opera"
        : ua.Contains("SamsungBrowser") ? "Samsung"
        : ua.Contains("Firefox/") ? "Firefox"
        : ua.Contains("Chrome/") || ua.Contains("CriOS/") ? "Chrome"
        : ua.Contains("Safari/") ? "Safari"
        : ua.Length == 0 ? null : "Other";

    private static string? Trim(string? s, int max) => string.IsNullOrEmpty(s) ? null : s.Length <= max ? s : s[..max];
}

/// <summary>Saves queued page views in batches and deletes views older than 180 days once a day.</summary>
public sealed class VisitorLogWriter(VisitorLogQueue queue, IDbContextFactory<ApplicationDbContext> dbFactory, ILogger<VisitorLogWriter> logger) : BackgroundService
{
    public const int KeepDays = 180;
    private DateOnly _lastCleanup;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<PageVisit>();
        while (await queue.Reader.WaitToReadAsync(stoppingToken))
        {
            // Gather what arrives over a few seconds so busy periods are one insert, not hundreds.
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            while (batch.Count < 500 && queue.Reader.TryRead(out var v)) batch.Add(v);
            using var _ = ErrorCapture.Suppress();
            try
            {
                await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);
                db.PageVisits.AddRange(batch);
                await db.SaveChangesAsync(stoppingToken);

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (_lastCleanup != today)
                {
                    _lastCleanup = today;
                    var cutoff = DateTime.UtcNow.AddDays(-KeepDays);
                    await db.PageVisits.Where(v => v.TimeUtc < cutoff).ExecuteDeleteAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not store {Count} page view(s).", batch.Count);
            }
            batch.Clear();
        }
    }
}
