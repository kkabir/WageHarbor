using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Paycrest.Data;
using Paycrest.Domain;
using Paycrest.Services.Payroll;

namespace Paycrest.Services;

public sealed class EmployeeService(Db dbs, SensitiveData sensitive, UserManager<ApplicationUser> users, CurrentUser currentUser)
{
    public async Task<string> NextEmployeeNumberAsync()
    {
        await using var db = await dbs.OpenAsync();
        var numbers = await db.Employees.Select(e => e.EmployeeNumber).ToListAsync();
        var max = numbers.Select(n => int.TryParse(n, out var i) ? i : 0).DefaultIfEmpty(1000).Max();
        return (Math.Max(max, 1000) + 1).ToString();
    }

    public async Task<Employee> SaveAsync(Employee e, string? newSsn)
    {
        await using var db = await dbs.OpenAsync();
        if (await db.Employees.AnyAsync(x => x.EmployeeNumber == e.EmployeeNumber && x.Id != e.Id))
            throw new InvalidOperationException($"Employee number {e.EmployeeNumber} is already in use.");
        if (e.ManagerId == e.Id && e.Id != 0) throw new InvalidOperationException("An employee cannot be their own manager.");

        if (!string.IsNullOrWhiteSpace(newSsn))
        {
            var digits = SensitiveData.Digits(newSsn);
            if (digits.Length != 9) throw new InvalidOperationException("SSN must have 9 digits.");
            e.SsnProtected = sensitive.Protect(digits);
            e.SsnLast4 = SensitiveData.Last4(digits);
        }
        if (e.Status == EmployeeStatus.Terminated && e.TerminationDate == null)
            e.TerminationDate = DateOnly.FromDateTime(DateTime.Today);

        // Save scalar fields only; child collections are managed separately.
        var copy = Detach(e);
        if (copy.Id == 0) db.Employees.Add(copy);
        else db.Employees.Update(copy);
        await db.SaveChangesAsync();

        if (e.Id == 0)
        {
            e.Id = copy.Id;
            // Enroll new employees in active time-off policies.
            var policies = await db.TimeOffPolicies.Where(p => p.IsActive).Select(p => p.Id).ToListAsync();
            if (!copy.IsContractor)
            {
                db.TimeOffBalances.AddRange(policies.Select(p => new TimeOffBalance { EmployeeId = copy.Id, TimeOffPolicyId = p }));
                await db.SaveChangesAsync();
            }
        }
        return e;
    }

    private static Employee Detach(Employee e)
    {
        var copy = (Employee)typeof(Employee).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(e, null)!;
        copy.Department = null;
        copy.Manager = null;
        copy.BankAccounts = [];
        copy.Deductions = [];
        copy.TimeOffBalances = [];
        copy.Documents = [];
        return copy;
    }

    public sealed record ImportResult(int LineNumber, string Name, bool Imported, string Message);

    /// <summary>
    /// Saves checked import rows one by one. Skips rows with errors and people who already exist (same email,
    /// or same last name and SSN last four). Creates departments by name and numbers employees when the file doesn't.
    /// </summary>
    public async Task<List<ImportResult>> ImportAsync(IEnumerable<EmployeeImportRow> rows)
    {
        var results = new List<ImportResult>();
        await using var db = await dbs.OpenAsync();
        var existing = await db.Employees.Select(e => new { e.Email, e.LastName, e.SsnLast4, e.EmployeeNumber }).ToListAsync();
        var emails = existing.Where(e => e.Email != null).Select(e => e.Email!.ToLowerInvariant()).ToHashSet();
        var nameSsn = existing.Where(e => e.SsnLast4 != null).Select(e => $"{e.LastName.ToLowerInvariant()}|{e.SsnLast4}").ToHashSet();
        var numbers = existing.Select(e => e.EmployeeNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var departments = await db.Departments.ToDictionaryAsync(d => d.Name.ToLowerInvariant(), d => d.Id);
        var nextNumber = numbers.Select(n => int.TryParse(n, out var i) ? i : 0).DefaultIfEmpty(1000).Max();

        foreach (var row in rows)
        {
            var e = row.Employee;
            var name = $"{e.FirstName} {e.LastName}".Trim();
            if (!row.IsValid) { results.Add(new(row.LineNumber, name, false, string.Join(" ", row.Errors))); continue; }
            if (e.Email != null && emails.Contains(e.Email.ToLowerInvariant()))
            { results.Add(new(row.LineNumber, name, false, $"Skipped: an employee with {e.Email} already exists.")); continue; }
            if (row.Ssn != null && nameSsn.Contains($"{e.LastName.ToLowerInvariant()}|{row.Ssn[^4..]}"))
            { results.Add(new(row.LineNumber, name, false, "Skipped: this person already exists (same last name and SSN).")); continue; }

            try
            {
                if (string.IsNullOrWhiteSpace(e.EmployeeNumber) || numbers.Contains(e.EmployeeNumber))
                    e.EmployeeNumber = (nextNumber = Math.Max(nextNumber, 1000) + 1).ToString();
                if (row.DepartmentName is { } dept)
                {
                    if (!departments.TryGetValue(dept.ToLowerInvariant(), out var deptId))
                    {
                        var d = new Department { Name = dept.Length > 80 ? dept[..80] : dept };
                        db.Departments.Add(d);
                        await db.SaveChangesAsync();
                        departments[dept.ToLowerInvariant()] = deptId = d.Id;
                    }
                    e.DepartmentId = deptId;
                }

                await SaveAsync(e, row.Ssn);
                if (row.HasBank)
                    await SaveBankAccountAsync(new EmployeeBankAccount { EmployeeId = e.Id, RoutingNumber = row.RoutingNumber!, AccountType = row.AccountType }, row.AccountNumber);

                numbers.Add(e.EmployeeNumber);
                if (e.Email != null) emails.Add(e.Email.ToLowerInvariant());
                if (row.Ssn != null) nameSsn.Add($"{e.LastName.ToLowerInvariant()}|{row.Ssn[^4..]}");
                results.Add(new(row.LineNumber, name, true, $"Added as #{e.EmployeeNumber}" + (row.Warnings.Count > 0 ? ". " + string.Join(" ", row.Warnings) : ".")));
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
            {
                results.Add(new(row.LineNumber, name, false, ex.InnerException?.Message ?? ex.Message));
            }
        }
        return results;
    }

    public async Task<string?> RevealSsnAsync(int employeeId)
    {
        await using var db = await dbs.OpenAsync();
        var e = await db.Employees.FirstAsync(x => x.Id == employeeId);
        db.AuditLogs.Add(new AuditLog { CompanyId = db.CurrentCompanyId, UserName = await currentUser.GetNameAsync(), Action = "ViewedSSN", EntityType = nameof(Employee), EntityId = employeeId.ToString() });
        await db.SaveChangesAsync();
        var ssn = sensitive.Unprotect(e.SsnProtected);
        return ssn is { Length: 9 } ? $"{ssn[..3]}-{ssn[3..5]}-{ssn[5..]}" : null;
    }

    public async Task SaveBankAccountAsync(EmployeeBankAccount a, string? newAccountNumber)
    {
        if (!NachaFileBuilder.IsValidRouting(a.RoutingNumber)) throw new InvalidOperationException("Routing number fails the ABA checksum. Please double-check it.");
        await using var db = await dbs.OpenAsync();
        if (!string.IsNullOrWhiteSpace(newAccountNumber))
        {
            var digits = SensitiveData.Digits(newAccountNumber);
            if (digits.Length is < 4 or > 17) throw new InvalidOperationException("Account number must be 4–17 digits.");
            a.AccountNumberProtected = sensitive.Protect(digits);
            a.AccountLast4 = SensitiveData.Last4(digits);
        }
        else if (a.Id == 0) throw new InvalidOperationException("Account number is required.");
        a.Employee = null;
        if (a.Id == 0) db.EmployeeBankAccounts.Add(a); else db.EmployeeBankAccounts.Update(a);
        await db.SaveChangesAsync();
    }

    public async Task SaveDeductionAsync(EmployeeDeduction d)
    {
        await using var db = await dbs.OpenAsync();
        d.Employee = null; d.DeductionType = null;
        if (d.Id == 0) db.EmployeeDeductions.Add(d); else db.EmployeeDeductions.Update(d);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync<T>(int id) where T : class
    {
        await using var db = await dbs.OpenAsync();
        var entity = await db.Set<T>().FindAsync(id);
        if (entity != null) { db.Set<T>().Remove(entity); await db.SaveChangesAsync(); }
    }

    /// <summary>Creates a self-service login for the employee and links it.</summary>
    public async Task<string> CreateLoginAsync(int employeeId, string email, IEnumerable<string> roles)
    {
        await using var db = await dbs.OpenAsync();
        var emp = await db.Employees.FirstAsync(e => e.Id == employeeId);
        if (emp.UserId != null) throw new InvalidOperationException("Employee already has a login.");

        var user = await users.FindByEmailAsync(email);
        string? tempPassword = null;
        if (user == null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, CompanyId = emp.CompanyId };
            tempPassword = GeneratePassword();
            var result = await users.CreateAsync(user, tempPassword);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }
        else if (user.CompanyId != emp.CompanyId)
            throw new InvalidOperationException("That email is already used by a login at another company.");
        else if (await db.Employees.AnyAsync(e => e.UserId == user.Id))
            throw new InvalidOperationException("That login is already linked to another employee.");

        foreach (var role in roles.Append(AppRoles.Employee).Distinct())
            if (!await users.IsInRoleAsync(user, role)) await users.AddToRoleAsync(user, role);

        emp.UserId = user.Id;
        emp.Email ??= email;
        await db.SaveChangesAsync();
        return tempPassword ?? "(existing account linked — password unchanged)";
    }

    public static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnopqrstuvwxyz", digits = "23456789", sym = "!@#$%*?";
        var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        char Pick(string s) { var b = new byte[4]; rng.GetBytes(b); return s[(int)(BitConverter.ToUInt32(b) % (uint)s.Length)]; }
        var chars = new List<char> { Pick(upper), Pick(lower), Pick(digits), Pick(sym) };
        for (var i = 0; i < 8; i++) chars.Add(Pick(upper + lower + digits));
        return new string(chars.OrderBy(_ => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000)).ToArray());
    }
}
