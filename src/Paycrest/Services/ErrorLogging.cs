using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Paycrest.Data;
using Paycrest.Domain;

namespace Paycrest.Services;

/// <summary>An error captured from the logging pipeline, waiting to be stored.</summary>
public sealed record ErrorEvent(DateTime Utc, LogLevel Level, string Category, string Message, Exception? Exception,
    string? Path, string? Method, string? UserName);

/// <summary>Hands errors from loggers (any thread) to <see cref="ErrorLogWriter"/>. Drops errors rather than block when full.</summary>
public sealed class ErrorLogQueue
{
    private readonly Channel<ErrorEvent> _channel =
        Channel.CreateBounded<ErrorEvent>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Add(ErrorEvent e) => _channel.Writer.TryWrite(e);
    public ChannelReader<ErrorEvent> Reader => _channel.Reader;
}

/// <summary>Turns off capture while the error writer itself works, so its own failures can't loop back in.</summary>
public static class ErrorCapture
{
    private static readonly AsyncLocal<bool> Suppressed = new();
    public static bool IsSuppressed => Suppressed.Value;

    public static IDisposable Suppress()
    {
        Suppressed.Value = true;
        return new Restore();
    }

    private sealed class Restore : IDisposable { public void Dispose() => Suppressed.Value = false; }
}

/// <summary>Captures every Error and Critical log entry in the app, whatever logged it.</summary>
public sealed class DatabaseErrorLoggerProvider(ErrorLogQueue queue, IHttpContextAccessor http) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new DatabaseErrorLogger(categoryName, queue, http);
    public void Dispose() { }

    private sealed class DatabaseErrorLogger(string category, ErrorLogQueue queue, IHttpContextAccessor http) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error && !ErrorCapture.IsSuppressed;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var ctx = http.HttpContext;
            // Interactive pages run over one long-lived "/_blazor" connection, which says nothing about the page.
            var path = ctx?.Request.Path.Value;
            var interactive = path?.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase) == true;
            queue.Add(new ErrorEvent(DateTime.UtcNow, logLevel, category, formatter(state, exception), exception,
                interactive ? "(interactive page)" : path, interactive ? null : ctx?.Request.Method, ctx?.User.Identity?.Name));
        }
    }
}

/// <summary>
/// Stores captured errors, grouping repeats by fingerprint, and emails the contact address when an error is new
/// or comes back after being resolved.
/// </summary>
public sealed partial class ErrorLogWriter(
    ErrorLogQueue queue,
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IEmailSender<ApplicationUser> emailSender,
    IConfiguration config,
    ILogger<ErrorLogWriter> logger) : BackgroundService
{
    private const int MaxEmailsPerHour = 20;
    private readonly Queue<DateTime> _recentEmails = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var e in queue.Reader.ReadAllAsync(stoppingToken))
        {
            using var _ = ErrorCapture.Suppress();
            try { await StoreAsync(e, stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The database itself may be the problem; keep going and leave a trace in the server log.
                logger.LogWarning(ex, "Could not store error: {Message}", e.Message);
            }
        }
    }

    private async Task StoreAsync(ErrorEvent e, CancellationToken ct)
    {
        var ex = e.Exception;
        var message = Trim(ex?.Message ?? e.Message, 2000);
        var fingerprint = Fingerprint(e);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.ErrorLogs.FirstOrDefaultAsync(x => x.Fingerprint == fingerprint, ct);
        var notify = false;
        if (row == null)
        {
            row = new ErrorLog { Fingerprint = fingerprint, FirstSeenUtc = e.Utc, Count = 0 };
            db.ErrorLogs.Add(row);
            notify = true;
        }
        else if (row.Status == ErrorStatus.Resolved)
        {
            row.Status = ErrorStatus.New;
            row.ResolvedUtc = null;
            row.ResolvedBy = null;
            row.Notes.Add(new CaseNote { CreatedBy = "system", Text = "Happened again after being resolved; reopened." });
            notify = true;
        }

        row.Count++;
        row.LastSeenUtc = e.Utc;
        row.Level = e.Level.ToString();
        row.Source = Trim(e.Category, 200);
        row.ExceptionType = Trim(ex?.GetType().FullName, 200);
        row.Message = message;
        row.Details = Trim(ex == null ? e.Message : e.Message + "\n\n" + ex, 20000);
        row.Path = Trim(e.Path, 400);
        row.Method = Trim(e.Method, 10);
        row.UserName = Trim(e.UserName, 256);
        await db.SaveChangesAsync(ct);

        if (notify && CanEmail()) await EmailAsync(row);
    }

    private bool CanEmail()
    {
        var now = DateTime.UtcNow;
        while (_recentEmails.Count > 0 && now - _recentEmails.Peek() > TimeSpan.FromHours(1)) _recentEmails.Dequeue();
        if (_recentEmails.Count >= MaxEmailsPerHour) return false;
        _recentEmails.Enqueue(now);
        return true;
    }

    private async Task EmailAsync(ErrorLog row)
    {
        if (emailSender is not SmtpEmailSender smtp) return;
        var to = config["Alerts:ErrorEmail"] is { Length: > 0 } a ? a : config["Site:ContactEmail"];
        if (string.IsNullOrWhiteSpace(to)) return;

        var baseUrl = (config["Site:BaseUrl"] is { Length: > 0 } b ? b : "https://www.wageharbor.com").TrimEnd('/');
        var kind = row.ExceptionType?.Split('.').Last() ?? row.Level;
        var subject = $"[WageHarbor ERROR] {kind}: {Trim(row.Message.ReplaceLineEndings(" "), 90)}";
        var h = (string? s) => WebUtility.HtmlEncode(s ?? "—");
        var html = $"""
            <h2 style="font-family:Segoe UI,Arial;color:#b91c1c;margin:0 0 8px">WageHarbor error #{row.Id}</h2>
            <p style="font-family:Segoe UI,Arial">{(row.Count > 1 ? "This error happened again after being marked resolved." : "A new error was recorded.")}</p>
            <table style="font-family:Segoe UI,Arial;font-size:14px;border-collapse:collapse">
              <tr><td style="padding:3px 12px 3px 0;color:#6b7280">When (UTC)</td><td>{row.LastSeenUtc:yyyy-MM-dd HH:mm:ss}</td></tr>
              <tr><td style="padding:3px 12px 3px 0;color:#6b7280">Type</td><td>{h(row.ExceptionType)}</td></tr>
              <tr><td style="padding:3px 12px 3px 0;color:#6b7280">Message</td><td>{h(row.Message)}</td></tr>
              <tr><td style="padding:3px 12px 3px 0;color:#6b7280">Page</td><td>{h(row.Method)} {h(row.Path)}</td></tr>
              <tr><td style="padding:3px 12px 3px 0;color:#6b7280">User</td><td>{h(row.UserName)}</td></tr>
              <tr><td style="padding:3px 12px 3px 0;color:#6b7280">Source</td><td>{h(row.Source)}</td></tr>
              <tr><td style="padding:3px 12px 3px 0;color:#6b7280">Times seen</td><td>{row.Count}</td></tr>
            </table>
            <p style="font-family:Segoe UI,Arial"><a href="{baseUrl}/platform/errors?id={row.Id}">Open in the WageHarbor error log</a></p>
            <pre style="font-size:12px;background:#f3f4f6;padding:10px;white-space:pre-wrap">{h(Trim(row.Details, 4000))}</pre>
            """;
        await smtp.SendAsync(to, subject, html);
    }

    /// <summary>Same exception type, same message shape (numbers and ids ignored) and same app code location → same error.</summary>
    private static string Fingerprint(ErrorEvent e)
    {
        var ex = e.Exception;
        var shape = Variable().Replace(ex?.Message ?? e.Message, "#");
        var frame = ex?.StackTrace?.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Contains("Paycrest."))
                    ?? ex?.StackTrace?.Split('\n').FirstOrDefault()?.Trim();
        var key = $"{ex?.GetType().FullName ?? e.Category}|{shape}|{frame}";
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key)));
    }

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F-]{27}|\d+")]
    private static partial Regex Variable();

    private static string? Trim(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];
}
