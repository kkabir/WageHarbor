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

app.UseVisitorLog();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

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
