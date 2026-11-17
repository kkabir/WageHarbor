using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Paycrest.Components;
using Paycrest.Components.Account;
using Paycrest.Data;
using Paycrest.Services;
using Paycrest.Services.Payroll;
using Paycrest.Services.Localization;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
builder.Services.AddAuthorization();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
// The factory also registers ApplicationDbContext as scoped (used by Identity).
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        options.Password.RequiredLength = 8;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager<CompanySignInManager>()
    .AddDefaultTokenProviders();
// Email links (confirm email, reset or set a password) work for 3 days, so a new employee has time to open theirs.
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromDays(3));

// Keys must persist across app-pool recycles, otherwise encrypted SSNs/bank numbers and login cookies break.
builder.Services.AddDataProtection()
    .SetApplicationName("Paycrest")
    .PersistKeysToDbContext<ApplicationDbContext>();

// Emails link to the public site (logo, buttons, footer), not to whatever address the request came in on.
EmailLayout.BaseUrl = builder.Configuration["Site:BaseUrl"] is { Length: > 0 } siteUrl ? siteUrl.TrimEnd('/') : "https://www.wageharbor.com";
var smtp = builder.Configuration.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions();
builder.Services.AddSingleton(smtp);
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, SmtpEmailSender>();

builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<Db>();
builder.Services.AddScoped<Clock>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddSingleton<SensitiveData>();
builder.Services.AddScoped<PayrollService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<TimeService>();
builder.Services.AddScoped<TimeOffService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<SampleDataService>();
builder.Services.AddScoped<OnboardingService>();
builder.Services.AddScoped<AccountEmails>();

// Permanent redirects, so search engines move their index to the https:// address.
// Screen languages (see Services/Localization/Lang.cs): chosen with the language picker (a cookie), ?lang=xx links,
// or the browser's language on a first visit.
builder.Services.AddSingleton<Lang>();
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    var cultures = Lang.All.Select(Lang.CultureFor).ToList();
    o.DefaultRequestCulture = new RequestCulture(cultures[0]);
    o.SupportedCultures = cultures;
    o.SupportedUICultures = cultures;
    o.FallBackToParentCultures = o.FallBackToParentUICultures = true;
    o.RequestCultureProviders =
    [
        new QueryStringRequestCultureProvider { QueryStringKey = "lang", UIQueryStringKey = "lang" },
        new CookieRequestCultureProvider { CookieName = Lang.CookieName },
        new AcceptLanguageHeaderRequestCultureProvider { MaximumAcceptLanguageHeaderValuesToTry = 5 },
    ];
});

builder.Services.AddHttpsRedirection(o => o.RedirectStatusCode = StatusCodes.Status308PermanentRedirect);

// Every Error/Critical log entry is stored in the database and emailed (see ErrorLogging.cs).
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ErrorLogQueue>();
builder.Services.AddSingleton<ILoggerProvider, DatabaseErrorLoggerProvider>();
builder.Services.AddHostedService<ErrorLogWriter>();
builder.Services.AddSingleton<PaydayEmailQueue>();
builder.Services.AddHostedService<PaydayEmailSender>();
// Page views for the platform visitor log (see VisitorLog.cs): no IP addresses or emails are stored.
builder.Services.AddSingleton<VisitorLogQueue>();
builder.Services.AddHostedService<VisitorLogWriter>();
// Search engines: sitemap dated to each build, and an IndexNow ping once per new build (see SearchEngines.cs).
builder.Services.AddHttpClient();
builder.Services.AddHostedService<IndexNowPinger>();

var app = builder.Build();

await SeedData.InitializeAsync(app.Services);

// Https:Enabled = false serves the site over plain HTTP (no redirect, no HSTS) until the host has an SSL certificate.
var httpsEnabled = app.Configuration.GetValue("Https:Enabled", true);

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    if (httpsEnabled) app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Site:CanonicalHost (e.g. www.wageharbor.com): the bare domain redirects there so search engines see one site.
if (app.Configuration["Site:CanonicalHost"] is { Length: > 0 } canonicalHost && canonicalHost.StartsWith("www."))
{
    var bareHost = canonicalHost[4..];
    app.Use(async (ctx, next) =>
    {
        if (!string.Equals(ctx.Request.Host.Host, bareHost, StringComparison.OrdinalIgnoreCase)) { await next(); return; }
        ctx.Response.StatusCode = StatusCodes.Status301MovedPermanently;
        ctx.Response.Headers.Location = $"https://{canonicalHost}{ctx.Request.PathBase}{ctx.Request.Path}{ctx.Request.QueryString}";
    });
}
if (httpsEnabled) app.UseHttpsRedirection();

app.UseRequestLocalization();
// A ?lang=xx link also remembers the choice, so the live part of the page (its circuit) uses the same language.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Query["lang"].ToString() is { Length: > 0 } q && Lang.Find(q) is { } chosen)
        ctx.Response.Cookies.Append(Lang.CookieName, CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(Lang.CultureFor(chosen).Name)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax, HttpOnly = true, Secure = ctx.Request.IsHttps });
    await next();
});
app.UseVisitorLog();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapSitemap();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

// The language picker posts here: remember the choice for a year, then go back to the page.
app.MapGet("/set-language", (string? lang, string? returnUrl, HttpContext http) =>
{
    var language = Lang.Find(lang) ?? Lang.All[0];
    var culture = Lang.CultureFor(language).Name;
    http.Response.Cookies.Append(Lang.CookieName, CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax, HttpOnly = true, Secure = http.Request.IsHttps });
    // Only same-site paths, so the picker can't be used to send people elsewhere.
    var back = returnUrl is { Length: > 0 } r && r.StartsWith('/') && !r.StartsWith("//") && !r.StartsWith("/\\") ? r : "/";
    return Results.LocalRedirect(back);
});

// The signed-in user's company logo. ?v= changes when the logo does, so browsers may cache it.
app.MapGet("/company-logo", async (HttpContext http, IDbContextFactory<ApplicationDbContext> factory) =>
{
    var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    await using var db = await factory.CreateDbContextAsync();
    db.CurrentCompanyId = await db.Users.Where(u => u.Id == userId).Select(u => u.CompanyId).FirstOrDefaultAsync();
    var logo = await db.CompanyLogos.AsNoTracking().FirstOrDefaultAsync();
    if (logo == null) return Results.NotFound();
    http.Response.Headers.CacheControl = "private, max-age=604800";
    http.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(logo.Data, logo.ContentType);
}).RequireAuthorization();

app.Run();
