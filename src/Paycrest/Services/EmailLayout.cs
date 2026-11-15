using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Paycrest.Services;

/// <summary>
/// The look shared by WageHarbor's account emails: wordmark, heading, short text, one clear button and a note.
/// Inline styles and tables only, because many mail apps ignore style sheets. <see cref="ToPlainText"/> makes the
/// text copy sent alongside, which mail apps without HTML show and spam filters expect.
/// </summary>
public static partial class EmailLayout
{
    private static string H(string s) => WebUtility.HtmlEncode(s);

    /// <param name="heading">Plain text.</param>
    /// <param name="paragraphs">Plain text; each becomes a paragraph.</param>
    /// <param name="buttonText">Plain text, or null for no button.</param>
    /// <param name="buttonUrl">The raw (not HTML-encoded) link.</param>
    /// <param name="note">Small grey text under the button, plain text.</param>
    public static string Render(string heading, IEnumerable<string> paragraphs, string? buttonText = null, string? buttonUrl = null, string? note = null)
    {
        var body = new StringBuilder();
        foreach (var p in paragraphs)
            body.Append($"<p style=\"margin:0 0 14px;line-height:1.55\">{H(p)}</p>");
        if (buttonText != null && buttonUrl != null)
        {
            var url = H(buttonUrl);
            body.Append($"""
                <table role="presentation" cellspacing="0" cellpadding="0" style="margin:22px 0"><tr><td style="border-radius:8px;background:#0e7c86">
                <a href="{url}" style="display:inline-block;padding:12px 24px;color:#ffffff;font-weight:600;text-decoration:none;border-radius:8px">{H(buttonText)}</a>
                </td></tr></table>
                <p data-html-only style="margin:0 0 14px;font-size:13px;color:#6b7280;line-height:1.5">If the button doesn't work, copy this link into your browser:<br /><a href="{url}" style="color:#0e7c86;word-break:break-all">{url}</a></p>
                """);
        }
        if (note != null)
            body.Append($"<p style=\"margin:14px 0 0;font-size:13px;color:#6b7280;line-height:1.5\">{H(note)}</p>");

        return $"""
            <!doctype html>
            <html><body style="margin:0;padding:0;background:#f3f5f9">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f5f9;padding:24px 12px"><tr><td align="center">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;font-family:'Segoe UI',Arial,sans-serif;font-size:15px;color:#111827">
                <tr><td style="padding:0 4px 14px;font-size:22px;font-weight:700;letter-spacing:-.3px"><span style="color:#0b2545">Wage</span><span style="color:#0e7c86">Harbor</span></td></tr>
                <tr><td style="background:#ffffff;border:1px solid #e5e8ef;border-radius:12px;padding:28px">
                  <h1 style="margin:0 0 16px;font-size:20px;color:#0b2545">{H(heading)}</h1>
                  {body}
                </td></tr>
                <tr><td style="padding:14px 4px;font-size:12px;color:#9ca3af">WageHarbor · Payroll &amp; HR for small business · wageharbor.com</td></tr>
              </table>
            </td></tr></table>
            </body></html>
            """;
    }

    /// <summary>A readable text version of an HTML email: links become "text (url)", tags are dropped.</summary>
    public static string ToPlainText(string html)
    {
        var s = Regex.Replace(html, @"<(style|head)[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        // The "copy this link" fallback only helps in HTML; the text copy already shows the link.
        s = Regex.Replace(s, @"<p data-html-only[^>]*>.*?</p>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        s = LinkRegex().Replace(s, m =>
        {
            var url = WebUtility.HtmlDecode(m.Groups[1].Value);
            var text = WebUtility.HtmlDecode(Regex.Replace(m.Groups[2].Value, "<[^>]+>", "")).Trim();
            return text == url || text.Length == 0 ? url : $"{text}: {url}";
        });
        s = Regex.Replace(s, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</(p|h\d|tr|li|div|table)>", "\n\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<li[^>]*>", "- ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "<[^>]+>", "");
        s = WebUtility.HtmlDecode(s);
        s = Regex.Replace(s, @"[ \t]+", " ");
        s = Regex.Replace(s, @" *\n *", "\n");
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        return s.Trim();
    }

    [GeneratedRegex("<a [^>]*href=\"([^\"]+)\"[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();
}
