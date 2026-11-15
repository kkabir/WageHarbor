using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Paycrest.Data;
using Paycrest.Domain;
using Paycrest.Services.Payroll;

namespace Paycrest.Services;

/// <summary>What a new hire fills in on the onboarding page.</summary>
public sealed class OnboardingInput
{
    [StringLength(60)] public string? PreferredName { get; set; }
    [Required(ErrorMessage = "Please enter your phone number."), StringLength(30)] public string Phone { get; set; } = "";
    [Required(ErrorMessage = "Please enter your date of birth.")] public DateOnly? DateOfBirth { get; set; }
    [Required(ErrorMessage = "Please enter your Social Security number.")] public string Ssn { get; set; } = "";

    [Required(ErrorMessage = "Please enter your street address."), StringLength(120)] public string AddressLine1 { get; set; } = "";
    [StringLength(120)] public string? AddressLine2 { get; set; }
    [Required(ErrorMessage = "Please enter your city."), StringLength(80)] public string City { get; set; } = "";
    [Required(ErrorMessage = "Please choose your state.")] public string? State { get; set; }
    [Required(ErrorMessage = "Please enter your ZIP code."), RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "ZIP code should be 5 digits.")] public string PostalCode { get; set; } = "";

    [StringLength(120)] public string? EmergencyContactName { get; set; }
    [StringLength(30)] public string? EmergencyContactPhone { get; set; }
    [StringLength(40)] public string? EmergencyContactRelation { get; set; }

    // Federal W-4
    public FilingStatus FilingStatus { get; set; } = FilingStatus.Single;
    public bool W4MultipleJobs { get; set; }
    [Range(0, 100000)] public decimal W4DependentsAmount { get; set; }
    [Range(0, 10_000_000)] public decimal W4OtherIncome { get; set; }
    [Range(0, 10_000_000)] public decimal W4Deductions { get; set; }
    [Range(0, 100000)] public decimal W4ExtraWithholding { get; set; }
    // State withholding
    public FilingStatus StateFilingStatus { get; set; } = FilingStatus.Single;
    [Range(0, 20)] public int StateAllowances { get; set; }

    // Pay
    public bool DirectDeposit { get; set; } = true;
    public string? RoutingNumber { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountNumberConfirm { get; set; }
    public BankAccountType AccountType { get; set; } = BankAccountType.Checking;

    // Login
    [Required(ErrorMessage = "Please choose a password."), StringLength(100, MinimumLength = 8, ErrorMessage = "Your password needs at least 8 characters.")]
    public string Password { get; set; } = "";
    [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")] public string PasswordConfirm { get; set; } = "";

    // Signature
    [Required(ErrorMessage = "Please type your full legal name to sign.")] public string SignedName { get; set; } = "";
    public bool Certify { get; set; }
}

/// <summary>An open onboarding link, with what the page needs to greet the new hire.</summary>
public sealed record OnboardingView(OnboardingInvite Invite, Employee Employee, string CompanyName, bool HasLogin);

/// <summary>New-hire self-onboarding: invites (company side) and completion (the new hire, without signing in).</summary>
public sealed class OnboardingService(
    Db dbs,
    IDbContextFactory<ApplicationDbContext> factory,
    SensitiveData sensitive,
    UserManager<ApplicationUser> users,
    IEmailSender<ApplicationUser> emailSender,
    ILogger<OnboardingService> logger)
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(14);

    public sealed record InviteResult(string Link, string? EmailError);

    /// <summary>Creates a new link for the employee (cancelling any earlier open one) and emails it.</summary>
    public async Task<InviteResult> SendInviteAsync(int employeeId, string email, string baseUri)
    {
        if (!System.Net.Mail.MailAddress.TryCreate(email.Trim(), out _)) throw new InvalidOperationException("Please enter a valid email address.");
        await using var db = await dbs.OpenAsync();
        var emp = await db.Employees.FirstAsync(e => e.Id == employeeId);
        if (emp.UserId != null) throw new InvalidOperationException($"{emp.FirstName} already has a login. They can update their details under My profile.");
        var company = await db.Companies.AsNoTracking().FirstAsync();
        var existingUser = await users.FindByEmailAsync(email.Trim());
        if (existingUser != null) throw new InvalidOperationException("That email already has a WageHarbor login. Use a different email, or link the existing login on this tab.");

        var now = DateTime.UtcNow;
        foreach (var old in await db.OnboardingInvites.Where(i => i.EmployeeId == employeeId && i.CompletedUtc == null && i.RevokedUtc == null).ToListAsync())
            old.RevokedUtc = now;

        var token = WebEncoders(RandomNumberGenerator.GetBytes(32));
        db.OnboardingInvites.Add(new OnboardingInvite
        {
            EmployeeId = employeeId, Email = email.Trim(), TokenHash = Hash(token),
            CreatedBy = db.AuditUser, ExpiresUtc = now.Add(LinkLifetime),
        });
        emp.Email ??= email.Trim();
        await db.SaveChangesAsync();

        var link = $"{baseUri.TrimEnd('/')}/onboard/{token}";
        string? emailError = "Email isn't set up, so send the link yourself.";
        if (emailSender is SmtpEmailSender smtp)
        {
            var h = (string s) => WebUtility.HtmlEncode(s);
            emailError = await smtp.TrySendAsync(email.Trim(), $"Welcome to {company.Name}: set up your pay",
                $"""
                <div style="font-family:Segoe UI,Arial,sans-serif;font-size:15px;color:#111827;max-width:560px">
                  <h2 style="color:#0b2545;margin:0 0 12px">Welcome to {h(company.Name)}, {h(emp.PreferredName ?? emp.FirstName)}!</h2>
                  <p>{h(company.Name)} uses WageHarbor for payroll. Please take about 10 minutes to enter your details so you can be paid:</p>
                  <ul><li>Your address and emergency contact</li><li>Your tax withholding (Form W-4)</li><li>The bank account for direct deposit</li><li>A password, so you can see your pay stubs any time</li></ul>
                  <p style="margin:24px 0"><a href="{link}" style="background:#0e7c86;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:600">Set up my pay</a></p>
                  <p style="color:#6b7280;font-size:13px">This private link works once and expires on {now.Add(LinkLifetime):MMMM d, yyyy}. Don't forward it. If you weren't expecting this, you can ignore it.</p>
                </div>
                """);
        }
        return new(link, emailError);
    }

    /// <summary>Finds an open invite by its token, across companies (the new hire isn't signed in).</summary>
    public async Task<OnboardingView?> FindAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return null;
        await using var db = await factory.CreateDbContextAsync();
        var hash = Hash(token);
        var invite = await db.OnboardingInvites.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(i => i.TokenHash == hash);
        if (invite == null || !invite.IsOpen(DateTime.UtcNow)) return null;
        var emp = await db.Employees.IgnoreQueryFilters().AsNoTracking().FirstAsync(e => e.Id == invite.EmployeeId && e.CompanyId == invite.CompanyId);
        var company = await db.Companies.IgnoreQueryFilters().AsNoTracking().FirstAsync(c => c.Id == invite.CompanyId);
        return new(invite, emp, company.Name, emp.UserId != null);
    }

    /// <summary>Saves the new hire's details, bank account and login. Returns problems to show, or none when done.</summary>
    public async Task<List<string>> CompleteAsync(string token, OnboardingInput input, string? ip)
    {
        var problems = new List<string>();
        var view = await FindAsync(token);
        if (view == null) return ["This link has expired or was already used. Ask your employer for a new one."];
        var emp0 = view.Employee;

        var ssn = SensitiveData.Digits(input.Ssn);
        if (ssn.Length != 9) problems.Add("Social Security number must have 9 digits.");
        if (input.DateOfBirth is { } dob && (dob > DateOnly.FromDateTime(DateTime.Today.AddYears(-14)) || dob.Year < 1900)) problems.Add("Please check your date of birth.");
        if (!Fmt.States.Contains(input.State ?? "")) problems.Add("Please choose your state.");
        string? routing = null, account = null;
        if (input.DirectDeposit)
        {
            routing = SensitiveData.Digits(input.RoutingNumber);
            account = SensitiveData.Digits(input.AccountNumber);
            if (!NachaFileBuilder.IsValidRouting(routing)) problems.Add("That routing number isn't valid. It's the 9-digit number at the bottom left of a check.");
            if (account.Length is < 4 or > 17) problems.Add("Account number should be 4 to 17 digits.");
            else if (account != SensitiveData.Digits(input.AccountNumberConfirm)) problems.Add("The two account numbers don't match.");
        }
        if (!input.Certify) problems.Add("Please tick the box to confirm your information.");
        if (!SameName(input.SignedName, emp0)) problems.Add($"To sign, type your full legal name as your employer has it: {emp0.FirstName} {emp0.LastName}.");
        if (problems.Count > 0) return problems;

        // Create the login first: if the password is rejected nothing else should change.
        ApplicationUser? user = null;
        if (!view.HasLogin)
        {
            if (await users.FindByEmailAsync(view.Invite.Email) != null)
                return ["That email already has a WageHarbor login. Ask your employer to link it to your employee record."];
            user = new ApplicationUser { UserName = view.Invite.Email, Email = view.Invite.Email, EmailConfirmed = true, CompanyId = view.Invite.CompanyId };
            var created = await users.CreateAsync(user, input.Password);
            if (!created.Succeeded) return created.Errors.Select(e => e.Description).ToList();
            await users.AddToRoleAsync(user, AppRoles.Employee);
        }

        try
        {
            await SaveDetailsAsync(view, input, ssn, routing, account, user, ip);
        }
        catch
        {
            // Don't leave a login behind for details that weren't saved.
            if (user != null) await users.DeleteAsync(user);
            throw;
        }
        return [];
    }

    private async Task SaveDetailsAsync(OnboardingView view, OnboardingInput input, string ssn, string? routing, string? account, ApplicationUser? user, string? ip)
    {
        var emp0 = view.Employee;
        await using var db = await factory.CreateDbContextAsync();
        db.CurrentCompanyId = view.Invite.CompanyId;
        db.AuditUser = view.Invite.Email;
        var emp = await db.Employees.FirstAsync(e => e.Id == emp0.Id);
        emp.PreferredName = Clean(input.PreferredName) ?? emp.PreferredName;
        emp.Phone = input.Phone.Trim();
        emp.DateOfBirth = input.DateOfBirth;
        emp.SsnProtected = sensitive.Protect(ssn);
        emp.SsnLast4 = SensitiveData.Last4(ssn);
        emp.AddressLine1 = input.AddressLine1.Trim();
        emp.AddressLine2 = Clean(input.AddressLine2);
        emp.City = input.City.Trim();
        emp.State = input.State;
        emp.PostalCode = input.PostalCode.Trim();
        emp.EmergencyContactName = Clean(input.EmergencyContactName);
        emp.EmergencyContactPhone = Clean(input.EmergencyContactPhone);
        emp.EmergencyContactRelation = Clean(input.EmergencyContactRelation);
        emp.FilingStatus = input.FilingStatus;
        emp.W4MultipleJobs = input.W4MultipleJobs;
        emp.W4DependentsAmount = input.W4DependentsAmount;
        emp.W4OtherIncome = input.W4OtherIncome;
        emp.W4Deductions = input.W4Deductions;
        emp.W4ExtraWithholding = input.W4ExtraWithholding;
        emp.StateFilingStatus = input.StateFilingStatus;
        emp.StateAllowances = input.StateAllowances;
        emp.PaymentMethod = input.DirectDeposit ? PaymentMethod.DirectDeposit : PaymentMethod.Check;
        emp.Email ??= view.Invite.Email;
        if (user != null) emp.UserId = user.Id;

        if (input.DirectDeposit)
        {
            foreach (var old in await db.EmployeeBankAccounts.Where(b => b.EmployeeId == emp.Id && b.IsActive).ToListAsync()) old.IsActive = false;
            db.EmployeeBankAccounts.Add(new EmployeeBankAccount
            {
                EmployeeId = emp.Id, RoutingNumber = routing!, AccountNumberProtected = sensitive.Protect(account!),
                AccountLast4 = SensitiveData.Last4(account!), AccountType = input.AccountType, SplitType = DepositSplitType.Remainder, Priority = 1,
            });
        }

        var invite = await db.OnboardingInvites.FirstAsync(i => i.Id == view.Invite.Id);
        invite.CompletedUtc = DateTime.UtcNow;
        invite.SignedName = input.SignedName.Trim();
        invite.SignedIp = ip;
        await db.SaveChangesAsync();
        logger.LogInformation("Onboarding completed for employee {EmployeeId} in company {CompanyId}.", emp.Id, invite.CompanyId);
    }

    /// <summary>The latest invite for each of these employees, for the employee page.</summary>
    public async Task<OnboardingInvite?> LatestAsync(int employeeId)
    {
        await using var db = await dbs.OpenAsync();
        return await db.OnboardingInvites.AsNoTracking().Where(i => i.EmployeeId == employeeId).OrderByDescending(i => i.CreatedUtc).FirstOrDefaultAsync();
    }

    public async Task RevokeAsync(int inviteId)
    {
        await using var db = await dbs.OpenAsync();
        var invite = await db.OnboardingInvites.FirstAsync(i => i.Id == inviteId);
        if (invite.CompletedUtc == null) invite.RevokedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static bool SameName(string typed, Employee e)
    {
        static string N(string? s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetter).ToArray());
        var t = N(typed);
        return t.Length > 0 && (t == N(e.FirstName + e.LastName) || t == N(e.FirstName + e.MiddleName + e.LastName) || t == N(e.LegalName));
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string WebEncoders(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
