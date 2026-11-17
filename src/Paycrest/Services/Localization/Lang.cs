using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace Paycrest.Services.Localization;

/// <summary>A language WageHarbor's screens can be shown in.</summary>
/// <param name="Code">Culture name, e.g. "es" or "zh-Hans".</param>
/// <param name="NativeName">The name in that language, for the language picker.</param>
/// <param name="EnglishName">The name in English, for tooltips and admins.</param>
public sealed record Language(string Code, string NativeName, string EnglishName)
{
    public bool IsRightToLeft => CultureInfo.GetCultureInfo(Code).TextInfo.IsRightToLeft;
}

/// <summary>
/// Translates screen text. The English text itself is the key: <c>L["Run payroll"]</c> shows the translation for the
/// current language, or the English when there isn't one yet, so a page never shows a blank or a key name.
/// Translations live in Resources/Lang/{code}.json as { "English text": "translation" }.
/// </summary>
public sealed class Lang
{
    public const string CookieName = ".WageHarbor.Lang";

    /// <summary>English first, then the most-spoken languages in the US after it.</summary>
    public static readonly IReadOnlyList<Language> All =
    [
        new("en", "English", "English"),
        new("es", "Español", "Spanish"),
        new("zh-Hans", "简体中文", "Chinese (Simplified)"),
        new("vi", "Tiếng Việt", "Vietnamese"),
        new("fil", "Filipino (Tagalog)", "Tagalog"),
        new("ar", "العربية", "Arabic"),
        new("ko", "한국어", "Korean"),
        new("fr", "Français", "French"),
        new("pt", "Português", "Portuguese"),
        new("ru", "Русский", "Russian"),
        new("ht", "Kreyòl ayisyen", "Haitian Creole"),
        new("hi", "हिन्दी", "Hindi"),
        new("bn", "বাংলা", "Bangla"),
        new("ur", "اردو", "Urdu"),
    ];

    private readonly FrozenDictionary<string, FrozenDictionary<string, string>> _byLanguage;

    public Lang(IWebHostEnvironment env, ILogger<Lang> logger)
    {
        var folder = Path.Combine(env.ContentRootPath, "Resources", "Lang");
        var map = new Dictionary<string, FrozenDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in All.Skip(1))
        {
            var file = Path.Combine(folder, language.Code + ".json");
            if (!File.Exists(file)) continue;
            try
            {
                var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) ?? [];
                map[language.Code] = entries.Where(e => !string.IsNullOrWhiteSpace(e.Value)).ToFrozenDictionary(e => e.Key, e => e.Value);
            }
            catch (JsonException ex)
            {
                // A broken file must not take the site down: that language just shows English.
                logger.LogError(ex, "Translation file {File} is not valid JSON; {Language} falls back to English.", file, language.EnglishName);
            }
        }
        _byLanguage = map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The language of the current request or circuit.</summary>
    public static Language Current => Find(CultureInfo.CurrentUICulture.Name) ?? All[0];

    public static Language? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 20) return null;
        if (All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) is { } exact) return exact;
        try
        {
            // e.g. "es-MX" → "es", "zh-CN" → "zh-Hans", "fil-PH" → "fil".
            for (var c = CultureInfo.GetCultureInfo(code); !string.IsNullOrEmpty(c.Name); c = c.Parent)
                if (All.FirstOrDefault(l => string.Equals(l.Code, c.Name, StringComparison.OrdinalIgnoreCase)) is { } match) return match;
        }
        catch (CultureNotFoundException) { } // a made-up ?lang= value just means English
        return null;
    }

    /// <summary>The translation of <paramref name="english"/> in the current language.</summary>
    public string this[string english] =>
        _byLanguage.TryGetValue(Current.Code, out var t) && t.TryGetValue(english, out var s) ? s : english;

    /// <summary>Translates <paramref name="english"/>, then fills in {0}, {1}… like string.Format.</summary>
    public string this[string english, params object?[] args] => string.Format(CultureInfo.CurrentCulture, this[english], args);

    /// <summary>For text containing simple markup such as &lt;strong&gt; or a link. Only use with text from the code, never user input.</summary>
    public MarkupString Html(string english, params object?[] args) =>
        new(args.Length == 0 ? this[english] : string.Format(CultureInfo.CurrentCulture, this[english], args));

    /// <summary>How many strings each language has, for tests and the platform owner.</summary>
    public IReadOnlyDictionary<string, int> Counts => _byLanguage.ToDictionary(k => k.Key, v => v.Value.Count);

    /// <summary>
    /// The culture used for a language: its words, month and day names, but always US dollars, US number
    /// format (so typed amounts read the same in every language) and the Gregorian calendar, because pay is in the US.
    /// </summary>
    public static CultureInfo CultureFor(Language language)
    {
        if (language.Code == "en") return CultureInfo.GetCultureInfo("en-US");
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(language.Code).Clone();
        culture.NumberFormat = (NumberFormatInfo)CultureInfo.GetCultureInfo("en-US").NumberFormat.Clone();
        if (culture.DateTimeFormat.Calendar is not GregorianCalendar)
        {
            var gregorian = culture.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault();
            if (gregorian != null) culture.DateTimeFormat.Calendar = gregorian;
            else culture.DateTimeFormat = (DateTimeFormatInfo)CultureInfo.GetCultureInfo("en-US").DateTimeFormat.Clone();
        }
        return culture;
    }
}
