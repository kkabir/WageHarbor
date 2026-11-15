using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Paycrest.Services;

/// <summary>The colour scheme of an email's header: the brand for most, red for error alerts.</summary>
public enum EmailTone { Brand, Success, Alert }

/// <summary>
/// What an email says. <see cref="EmailLayout"/> turns it into the designed HTML and a matching plain-text copy.
/// Every text field is plain text (it is encoded for you); links are raw URLs.
/// </summary>
public sealed record EmailMessage
{
    /// <summary>The big title in the header.</summary>
    public required string Heading { get; init; }
    /// <summary>Small capitals above the title, e.g. "Payday" or "Your account".</summary>
    public string? Eyebrow { get; init; }
    /// <summary>One line under the title, in the header.</summary>
    public string? Lead { get; init; }
    /// <summary>The line inbox lists show after the subject. Defaults to <see cref="Lead"/> or the first paragraph.</summary>
    public string? Preheader { get; init; }
    public EmailTone Tone { get; init; } = EmailTone.Brand;

    /// <summary>e.g. "Hi Luis,"</summary>
    public string? Greeting { get; init; }
    public IReadOnlyList<string> Paragraphs { get; init; } = [];
    /// <summary>A checklist under the paragraphs.</summary>
    public IReadOnlyList<string> Bullets { get; init; } = [];
    /// <summary>Label/value rows in a soft panel (dates, ids, who sent it…).</summary>
    public IReadOnlyList<(string Label, string Value)> Details { get; init; } = [];
    /// <summary>Someone's own words, shown as a quote (e.g. a contact-form message).</summary>
    public string? Quote { get; init; }
    /// <summary>Technical text in a monospace box (e.g. a stack trace).</summary>
    public string? Code { get; init; }

    public string? ButtonText { get; init; }
    public string? ButtonUrl { get; init; }
    /// <summary>Small grey text after the button: how long a link lasts, "ignore this if…".</summary>
    public string? Note { get; init; }
    /// <summary>Why the person gets this email, shown in the footer.</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// WageHarbor's email design. Built from tables with inline styles, because Gmail and Outlook ignore most CSS;
/// the few &lt;style&gt; rules only add polish (phone layout) where mail apps support them.
/// The logo is a PNG with alt text and the name is real text, so emails still look right with images blocked.
/// </summary>
public static partial class EmailLayout
{
    /// <summary>The public site address, for the logo and footer links. Set from Site:BaseUrl at start-up.</summary>
    public static string BaseUrl { get; set; } = "https://www.wageharbor.com";

    private const string Font = "'Segoe UI',-apple-system,BlinkMacSystemFont,Roboto,'Helvetica Neue',Arial,sans-serif";
    private const string Navy = "#0b2545", Teal = "#0e7c86", Aqua = "#5eead4", Gold = "#fbbf24";
    private const string Ink = "#0f172a", Body = "#334155", Muted = "#64748b", Line = "#e2e8f0", Soft = "#f5f8fb";

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static (string From, string To, string Eyebrow, string Strip) Colors(EmailTone tone) => tone switch
    {
        EmailTone.Alert => ("#450a0a", "#b91c1c", "#fecaca", "linear-gradient(90deg,#f87171,#fbbf24)"),
        EmailTone.Success => (Navy, "#047857", "#a7f3d0", $"linear-gradient(90deg,{Aqua},{Gold})"),
        _ => (Navy, Teal, Gold, $"linear-gradient(90deg,{Gold},{Aqua})"),
    };

    /// <summary>The HTML version of <paramref name="m"/>.</summary>
    public static string Render(EmailMessage m)
    {
        var (from, to, eyebrowColor, strip) = Colors(m.Tone);
        var site = BaseUrl.TrimEnd('/');
        var preheader = m.Preheader ?? m.Lead ?? m.Paragraphs.FirstOrDefault() ?? "";
        var b = new StringBuilder();

        if (m.Greeting != null)
            b.Append($"<p style=\"margin:0 0 16px;font-size:17px;font-weight:600;color:{Ink}\">{H(m.Greeting)}</p>");
        foreach (var p in m.Paragraphs)
            b.Append($"<p style=\"margin:0 0 16px;font-size:16px;line-height:1.65;color:{Body}\">{H(p)}</p>");

        if (m.Bullets.Count > 0)
        {
            b.Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:4px 0 20px\">");
            foreach (var item in m.Bullets)
                b.Append($"""
                    <tr><td valign="top" style="padding:0 12px 10px 0"><div style="width:22px;height:22px;line-height:22px;border-radius:11px;background:#e6f6f5;color:{Teal};font-size:13px;font-weight:700;text-align:center">&#10003;</div></td>
                    <td data-li style="padding:1px 0 10px;font-size:15px;line-height:1.5;color:{Body}">{H(item)}</td></tr>
                    """);
            b.Append("</table>");
        }

        if (m.Details.Count > 0)
        {
            b.Append($"<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:4px 0 22px;background:{Soft};border:1px solid {Line};border-radius:12px\"><tr><td style=\"padding:8px 20px\">");
            b.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\">");
            for (var i = 0; i < m.Details.Count; i++)
            {
                var (label, value) = m.Details[i];
                var border = i < m.Details.Count - 1 ? $"border-bottom:1px solid {Line};" : "";
                b.Append($"""
                    <tr><td valign="top" style="{border}padding:10px 16px 10px 0;font-size:13px;color:{Muted};white-space:nowrap">{H(label)}</td>
                    <td valign="top" style="{border}padding:10px 0;font-size:14px;font-weight:600;color:{Ink};word-break:break-word">{H(value)}</td></tr>
                    """);
            }
            b.Append("</table></td></tr></table>");
        }

        if (m.Quote != null)
        {
            // Paragraphs and line breaks as real tags: white-space:pre-line doubles blank lines in some mail apps.
            var paras = Regex.Split(m.Quote.ReplaceLineEndings("\n").Trim(), @"\n\s*\n")
                .Select(p => $"<p style=\"margin:0 0 10px\">{string.Join("<br />", p.Split('\n').Select(l => H(l.Trim())))}</p>");
            b.Append($"<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:4px 0 22px\"><tr><td style=\"border-left:4px solid {Teal};background:{Soft};border-radius:0 10px 10px 0;padding:16px 20px 6px;font-size:15px;line-height:1.65;color:{Ink}\">{string.Concat(paras)}</td></tr></table>");
        }

        if (m.ButtonText != null && m.ButtonUrl != null)
        {
            var url = H(m.ButtonUrl);
            b.Append($"""
                <table role="presentation" cellspacing="0" cellpadding="0" border="0" class="btn" style="margin:26px 0 22px"><tr>
                <td align="center" bgcolor="{to}" style="border-radius:10px;background:{to};background-image:linear-gradient(135deg,{to},{from});box-shadow:0 6px 16px rgba(15,23,42,.22)">
                <a href="{url}" target="_blank" style="display:inline-block;padding:15px 30px;font-family:{Font};font-size:16px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:10px">{H(m.ButtonText)} &rarr;</a>
                </td></tr></table>
                <p data-html-only style="margin:0 0 16px;font-size:12px;line-height:1.6;color:{Muted}">Button not working? Paste this link into your browser:<br /><a href="{url}" style="color:{Teal};word-break:break-all">{url}</a></p>
                """);
        }

        if (m.Code != null)
            b.Append($"<pre style=\"margin:4px 0 20px;padding:16px;background:{Ink};color:#e2e8f0;border-radius:10px;font-family:Consolas,'SFMono-Regular',Menlo,monospace;font-size:12px;line-height:1.55;white-space:pre-wrap;word-break:break-word\">{H(m.Code)}</pre>");

        if (m.Note != null)
            b.Append($"<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin-top:8px\"><tr><td style=\"border-top:1px solid {Line};padding-top:18px;font-size:13px;line-height:1.6;color:{Muted}\">{H(m.Note)}</td></tr></table>");

        var eyebrow = m.Eyebrow == null ? "" :
            $"<p style=\"margin:0 0 10px;font-size:12px;font-weight:700;letter-spacing:1.6px;text-transform:uppercase;color:{eyebrowColor}\">{H(m.Eyebrow)}</p>";
        var lead = m.Lead == null ? "" :
            $"<p style=\"margin:12px 0 0;font-size:16px;line-height:1.55;color:#d6e9ee\">{H(m.Lead)}</p>";
        var reason = m.Reason == null ? "" :
            $"<p style=\"margin:10px 0 0;font-size:12px;line-height:1.6;color:#94a3b8\">{H(m.Reason)}</p>";

        return $$"""
            <!doctype html>
            <html lang="en" xmlns="http://www.w3.org/1999/xhtml">
            <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width,initial-scale=1" />
            <meta name="color-scheme" content="light only" />
            <meta name="supported-color-schemes" content="light" />
            <title>{{H(m.Heading)}}</title>
            <style>
              body { margin:0 !important; padding:0 !important; }
              a { color:{{Teal}}; }
              @media (max-width:620px) {
                .px { padding-left:22px !important; padding-right:22px !important; }
                .h1 { font-size:25px !important; }
                .btn, .btn td, .btn a { display:block !important; width:100% !important; box-sizing:border-box; text-align:center !important; }
              }
            </style>
            </head>
            <body style="margin:0;padding:0;background:#eef2f6" bgcolor="#eef2f6">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;mso-hide:all">{{H(preheader)}}&#8199;&#65279;&#847;&#8199;&#65279;&#847;&#8199;&#65279;&#847;&#8199;&#65279;&#847;&#8199;&#65279;&#847;&#8199;&#65279;&#847;</div>
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="#eef2f6" style="background:#eef2f6">
            <tr><td align="center" style="padding:32px 12px 40px">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:600px;font-family:{{Font}}">

                <tr><td class="px" bgcolor="{{from}}" style="background:{{from}};background-image:linear-gradient(135deg,{{from}} 0%,{{to}} 100%);border-radius:18px 18px 0 0;padding:28px 40px 34px">
                  <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="margin-bottom:30px"><tr>
                    <td style="padding-right:12px"><img src="{{site}}/brand/wageharbor-mark-email.png" width="40" height="40" alt="" style="display:block;border:0;border-radius:10px" /></td>
                    <td style="font-size:21px;font-weight:700;letter-spacing:-.3px;color:#ffffff">Wage<span style="color:{{Aqua}}">Harbor</span></td>
                  </tr></table>
                  {{eyebrow}}
                  <h1 class="h1" style="margin:0;font-size:28px;line-height:1.25;font-weight:700;letter-spacing:-.4px;color:#ffffff">{{H(m.Heading)}}</h1>
                  {{lead}}
                </td></tr>
                <tr><td height="5" style="height:5px;line-height:5px;font-size:0;background:{{Gold}};background-image:{{strip}}">&nbsp;</td></tr>

                <tr><td class="px" bgcolor="#ffffff" style="background:#ffffff;border:1px solid {{Line}};border-top:0;border-radius:0 0 18px 18px;padding:36px 40px 34px">
                  {{b}}
                </td></tr>

                <tr><td align="center" style="padding:26px 20px 0;font-size:13px;line-height:1.6;color:{{Muted}}">
                  <p style="margin:0 0 6px"><strong style="color:{{Ink}}">WageHarbor</strong> &middot; Payroll &amp; HR for small business</p>
                  <p style="margin:0"><a href="{{site}}/help" style="color:{{Muted}};text-decoration:underline">Help</a> &nbsp;&middot;&nbsp; <a href="{{site}}/privacy" style="color:{{Muted}};text-decoration:underline">Privacy</a> &nbsp;&middot;&nbsp; <a href="{{site}}/contact" style="color:{{Muted}};text-decoration:underline">Contact us</a></p>
                  {{reason}}
                </td></tr>

              </table>
            </td></tr>
            </table>
            </body>
            </html>
            """;
    }

    /// <summary>The plain-text version of <paramref name="m"/>, sent alongside the HTML.</summary>
    public static string RenderText(EmailMessage m)
    {
        var t = new StringBuilder();
        if (m.Eyebrow != null) t.AppendLine(m.Eyebrow.ToUpperInvariant());
        t.AppendLine(m.Heading);
        if (m.Lead != null) t.AppendLine(m.Lead);
        t.AppendLine();
        if (m.Greeting != null) t.AppendLine(m.Greeting).AppendLine();
        foreach (var p in m.Paragraphs) t.AppendLine(p).AppendLine();
        if (m.Bullets.Count > 0) { foreach (var x in m.Bullets) t.AppendLine("  * " + x); t.AppendLine(); }
        if (m.Details.Count > 0)
        {
            var w = m.Details.Max(d => d.Label.Length);
            foreach (var (label, value) in m.Details) t.AppendLine($"  {(label + ":").PadRight(w + 2)}{value}");
            t.AppendLine();
        }
        if (m.Quote != null) { foreach (var line in m.Quote.ReplaceLineEndings("\n").Split('\n')) t.AppendLine("> " + line); t.AppendLine(); }
        if (m.ButtonText != null && m.ButtonUrl != null) t.AppendLine($"{m.ButtonText}:").AppendLine(m.ButtonUrl).AppendLine();
        if (m.Code != null) t.AppendLine(m.Code).AppendLine();
        if (m.Note != null) t.AppendLine(m.Note).AppendLine();
        t.AppendLine("--");
        t.AppendLine("WageHarbor · Payroll & HR for small business");
        t.AppendLine(BaseUrl.TrimEnd('/'));
        if (m.Reason != null) t.AppendLine(m.Reason);
        return t.ToString().TrimEnd();
    }

    /// <summary>Kept for callers that only have a heading, paragraphs and a button.</summary>
    public static string Render(string heading, IEnumerable<string> paragraphs, string? buttonText = null, string? buttonUrl = null, string? note = null) =>
        Render(new EmailMessage { Heading = heading, Paragraphs = paragraphs.ToList(), ButtonText = buttonText, ButtonUrl = buttonUrl, Note = note });

    /// <summary>A readable text version of any HTML email, for mail not built from an <see cref="EmailMessage"/>.</summary>
    public static string ToPlainText(string html)
    {
        var s = Regex.Replace(html, @"<(style|head|title)[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<div style=""display:none[^>]*>.*?</div>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        // The "paste this link" fallback only helps in HTML; the text copy already shows the link.
        s = Regex.Replace(s, @"<p data-html-only[^>]*>.*?</p>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        s = LinkRegex().Replace(s, m =>
        {
            var url = WebUtility.HtmlDecode(m.Groups[1].Value);
            var text = WebUtility.HtmlDecode(Regex.Replace(m.Groups[2].Value, "<[^>]+>", "")).Trim();
            return text == url || text.Length == 0 ? url : $"{text}: {url}";
        });
        s = Regex.Replace(s, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</(p|h\d|tr|li|div|table|pre)>", "\n\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<(li|td data-li)[^>]*>", "- ", RegexOptions.IgnoreCase);
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
