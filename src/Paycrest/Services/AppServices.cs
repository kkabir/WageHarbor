using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Paycrest.Data;

namespace Paycrest.Services;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string HR = "HR";
    public const string Payroll = "Payroll";
    public const string Manager = "Manager";
    public const string Employee = "Employee";

    public static readonly string[] All = [Admin, HR, Payroll, Manager, Employee];

    // Combined role lists for [Authorize(Roles = ...)]
    public const string PeopleAdmins = "Admin,HR,Payroll";
    public const string PayrollAdmins = "Admin,Payroll";
    public const string Approvers = "Admin,HR,Payroll,Manager";
    public const string SettingsAdmins = "Admin,HR,Payroll";
}

/// <summary>Information about the signed-in user for the current circuit/request.</summary>
public sealed class CurrentUser(AuthenticationStateProvider auth, IDbContextFactory<ApplicationDbContext> factory)
{
    private ClaimsPrincipal? _principal;
    private int? _employeeId;
    private bool _employeeLoaded;

    public async Task<ClaimsPrincipal> GetPrincipalAsync() =>
        _principal ??= (await auth.GetAuthenticationStateAsync()).User;

    public async Task<string?> GetNameAsync() => (await GetPrincipalAsync()).Identity?.Name;

    public async Task<string?> GetUserIdAsync() => (await GetPrincipalAsync()).FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<bool> IsInAnyRoleAsync(params string[] roles)
    {
        var p = await GetPrincipalAsync();
        return roles.Any(p.IsInRole);
    }

    public Task<bool> CanManagePeopleAsync() => IsInAnyRoleAsync(AppRoles.Admin, AppRoles.HR, AppRoles.Payroll);
    public Task<bool> CanApproveAllAsync() => IsInAnyRoleAsync(AppRoles.Admin, AppRoles.HR, AppRoles.Payroll);

    /// <summary>The employee record linked to this login, if any.</summary>
    public async Task<int?> GetEmployeeIdAsync()
    {
        if (_employeeLoaded) return _employeeId;
        var userId = await GetUserIdAsync();
        if (userId != null)
        {
            await using var db = await factory.CreateDbContextAsync();
            _employeeId = await db.Employees.Where(e => e.UserId == userId).Select(e => (int?)e.Id).FirstOrDefaultAsync();
        }
        _employeeLoaded = true;
        return _employeeId;
    }
}

/// <summary>Creates DbContexts stamped with the current user for the audit trail.</summary>
public sealed class Db(IDbContextFactory<ApplicationDbContext> factory, CurrentUser user)
{
    public async Task<ApplicationDbContext> OpenAsync()
    {
        var db = await factory.CreateDbContextAsync();
        db.AuditUser = await user.GetNameAsync();
        return db;
    }
}

/// <summary>Encrypts SSNs and bank account numbers at rest.</summary>
public sealed class SensitiveData(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("Paycrest.SensitiveData.v1");

    public string Protect(string plain) => _protector.Protect(plain);

    public string? Unprotect(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return null;
        try { return _protector.Unprotect(cipher); }
        catch { return null; }
    }

    public static string Digits(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());
    public static string Last4(string digits) => digits.Length <= 4 ? digits : digits[^4..];
    public static string MaskSsn(string? last4) => string.IsNullOrEmpty(last4) ? "—" : $"***-**-{last4}";
    public static string MaskAccount(string? last4) => string.IsNullOrEmpty(last4) ? "—" : $"••••{last4}";
}

/// <summary>Company-local date and time.</summary>
public sealed class Clock(IDbContextFactory<ApplicationDbContext> factory)
{
    private TimeZoneInfo? _tz;

    public async Task<TimeZoneInfo> GetZoneAsync()
    {
        if (_tz != null) return _tz;
        await using var db = await factory.CreateDbContextAsync();
        var id = await db.Companies.Select(c => c.TimeZoneId).FirstOrDefaultAsync();
        try { _tz = TimeZoneInfo.FindSystemTimeZoneById(id ?? "America/New_York"); }
        catch { _tz = TimeZoneInfo.Local; }
        return _tz;
    }

    public async Task<DateTime> NowAsync() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, await GetZoneAsync());
    public async Task<DateOnly> TodayAsync() => DateOnly.FromDateTime(await NowAsync());
    public async Task<DateTime> ToLocalAsync(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), await GetZoneAsync());
}

public sealed class ToastService
{
    public event Action<string, string>? OnShow;
    public void Success(string message) => OnShow?.Invoke(message, "success");
    public void Error(string message) => OnShow?.Invoke(message, "danger");
    public void Info(string message) => OnShow?.Invoke(message, "info");
}

public static class Fmt
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    public static string Money(decimal v) => v.ToString("C2", Us);
    public static string Money(decimal? v) => v.HasValue ? Money(v.Value) : "—";
    public static string Hours(decimal v) => v.ToString("0.##", Us);
    public static string Pct(decimal fraction) => (fraction * 100).ToString("0.###", Us) + "%";
    public static string Date(DateOnly d) => d.ToString("MMM d, yyyy", Us);
    public static string Date(DateOnly? d) => d.HasValue ? Date(d.Value) : "—";
    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static readonly string[] States =
    [
        "AL","AK","AZ","AR","CA","CO","CT","DE","DC","FL","GA","HI","ID","IL","IN","IA","KS","KY","LA","ME","MD","MA","MI","MN","MS",
        "MO","MT","NE","NV","NH","NJ","NM","NY","NC","ND","OH","OK","OR","PA","RI","SC","SD","TN","TX","UT","VT","VA","WA","WV","WI","WY"
    ];
}
