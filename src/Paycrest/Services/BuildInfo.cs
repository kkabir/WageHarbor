using System.Globalization;
using System.Reflection;

namespace Paycrest.Services;

/// <summary>
/// Build number, commit and build time, stamped into the assembly by the StampBuildInfo target in Paycrest.csproj.
/// Shown in the page footers so you can tell which version is live.
/// </summary>
public static class BuildInfo
{
    private static readonly Dictionary<string, string?> Meta = typeof(BuildInfo).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .GroupBy(a => a.Key)
        .ToDictionary(g => g.Key, g => g.Last().Value);

    private static string? Get(string key) => Meta.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    /// <summary>Number of commits on the branch when it was built, e.g. "24".</summary>
    public static string? Number => Get("BuildNumber");

    /// <summary>Short commit id, e.g. "183d95a".</summary>
    public static string? Commit => Get("BuildCommit");

    /// <summary>When the build was made (UTC), or the DLL's file time if it wasn't stamped.</summary>
    public static DateTime BuiltUtc { get; } =
        DateTime.TryParse(Get("BuildDateUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)
            ? d
            : File.GetLastWriteTimeUtc(typeof(BuildInfo).Assembly.Location);

    /// <summary>Short footer text, e.g. "Build 24 (183d95a) · Published Nov 15, 2026".</summary>
    public static string Label { get; } =
        (Number != null ? $"Build {Number}" : "Build") +
        (Commit != null ? $" ({Commit})" : "") +
        $" · Published {BuiltUtc.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}";

    /// <summary>Hover text with the exact time.</summary>
    public static string Tooltip { get; } = $"Built {BuiltUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC";
}
