using Microsoft.EntityFrameworkCore;
using Paycrest.Domain;
using Paycrest.Services.Payroll;

namespace Paycrest.Services;

/// <summary>Loads a realistic demo workforce so a new company can try every feature.</summary>
public sealed class SampleDataService(Db dbs, SensitiveData sensitive)
{
    public async Task<int> LoadAsync()
    {
        await using var db = await dbs.OpenAsync();
        if (await db.Employees.AnyAsync()) throw new InvalidOperationException("Sample data can only be loaded into an empty company.");
        var company = await db.Companies.FirstAsync();
        var state = company.State ?? "TX";
        var rnd = new Random(42);

        var depts = new[] { "Operations", "Sales", "Engineering", "Finance", "People" }
            .Select(n => new Department { Name = n, Code = n[..3].ToUpperInvariant() }).ToList();
        db.Departments.AddRange(depts);
        await db.SaveChangesAsync();
        Department D(string n) => depts.First(d => d.Name == n);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var people = new (string First, string Last, string Title, string Dept, PayType Type, decimal Rate, FilingStatus Fs, int YearsAgo, EmploymentType Emp)[]
        {
            ("Maya", "Chen", "Chief Executive Officer", "Operations", PayType.Salary, 145000, FilingStatus.MarriedFilingJointly, 6, EmploymentType.FullTime),
            ("Jordan", "Patel", "Operations Manager", "Operations", PayType.Salary, 82000, FilingStatus.Single, 4, EmploymentType.FullTime),
            ("Luis", "Ramirez", "Warehouse Associate", "Operations", PayType.Hourly, 19.50m, FilingStatus.HeadOfHousehold, 2, EmploymentType.FullTime),
            ("Aisha", "Okafor", "Warehouse Associate", "Operations", PayType.Hourly, 18.75m, FilingStatus.Single, 1, EmploymentType.FullTime),
            ("Ben", "Turner", "Driver", "Operations", PayType.Hourly, 22.00m, FilingStatus.MarriedFilingJointly, 3, EmploymentType.FullTime),
            ("Sofia", "Rossi", "Sales Lead", "Sales", PayType.Salary, 72000, FilingStatus.Single, 3, EmploymentType.FullTime),
            ("Ethan", "Kim", "Account Executive", "Sales", PayType.Salary, 58000, FilingStatus.Single, 1, EmploymentType.FullTime),
            ("Priya", "Shah", "Software Engineer", "Engineering", PayType.Salary, 118000, FilingStatus.MarriedFilingJointly, 2, EmploymentType.FullTime),
            ("Noah", "Williams", "Support Specialist", "Engineering", PayType.Hourly, 24.00m, FilingStatus.Single, 1, EmploymentType.PartTime),
            ("Grace", "Johnson", "Controller", "Finance", PayType.Salary, 95000, FilingStatus.HeadOfHousehold, 5, EmploymentType.FullTime),
            ("Omar", "Haddad", "HR Generalist", "People", PayType.Salary, 64000, FilingStatus.Single, 2, EmploymentType.FullTime),
            ("Riley", "Brooks", "Design Contractor", "Engineering", PayType.Hourly, 55.00m, FilingStatus.Single, 1, EmploymentType.Contractor1099),
        };

        var employees = new List<Employee>();
        var n = 1001;
        foreach (var p in people)
        {
            var ssn = $"9{rnd.Next(10, 99)}{rnd.Next(10, 99)}{rnd.Next(1000, 9999)}"; // 9xx = never-issued test range
            var e = new Employee
            {
                EmployeeNumber = (n++).ToString(),
                FirstName = p.First, LastName = p.Last, JobTitle = p.Title,
                Email = $"{p.First}.{p.Last}@example.com".ToLowerInvariant(),
                Phone = $"(555) 01{rnd.Next(0, 9)}-{rnd.Next(1000, 9999)}",
                DateOfBirth = today.AddYears(-rnd.Next(22, 58)).AddDays(-rnd.Next(0, 365)),
                AddressLine1 = $"{rnd.Next(100, 9999)} {(new[] { "Oak", "Maple", "Cedar", "Elm", "Pine" })[rnd.Next(5)]} St",
                City = "Springfield", State = state, PostalCode = $"7{rnd.Next(1000, 9999)}",
                HireDate = today.AddYears(-p.YearsAgo).AddDays(-rnd.Next(0, 200)),
                DepartmentId = D(p.Dept).Id, PayType = p.Type, PayRate = p.Rate,
                PayFrequency = company.DefaultPayFrequency, EmploymentType = p.Emp,
                DefaultHours = p.Emp == EmploymentType.PartTime ? 40 : 80 * 26m / (int)company.DefaultPayFrequency,
                IsExempt = p.Type == PayType.Salary, WorkState = state,
                FilingStatus = p.Fs, StateFilingStatus = p.Fs,
                W4DependentsAmount = p.Fs == FilingStatus.Single ? 0 : 2000,
                SsnProtected = sensitive.Protect(ssn), SsnLast4 = ssn[^4..],
                EmergencyContactName = "Alex " + p.Last, EmergencyContactPhone = "(555) 010-0000", EmergencyContactRelation = "Spouse",
                PaymentMethod = rnd.Next(10) == 0 ? PaymentMethod.Check : PaymentMethod.DirectDeposit
            };
            employees.Add(e);
        }
        db.Employees.AddRange(employees);
        await db.SaveChangesAsync();

        // Reporting lines
        var ceo = employees[0];
        foreach (var e in employees.Skip(1)) e.ManagerId = ceo.Id;
        employees[2].ManagerId = employees[3].ManagerId = employees[4].ManagerId = employees[1].Id;
        employees[6].ManagerId = employees[5].Id;
        D("Operations").Description = "Fulfilment and logistics";

        var types = await db.DeductionTypes.ToDictionaryAsync(d => d.Code);
        var policies = await db.TimeOffPolicies.ToListAsync();
        foreach (var e in employees)
        {
            if (e.PaymentMethod == PaymentMethod.DirectDeposit)
            {
                var acct = rnd.NextInt64(100000000, 999999999).ToString();
                db.EmployeeBankAccounts.Add(new EmployeeBankAccount
                {
                    EmployeeId = e.Id, BankName = "First Demo Bank", RoutingNumber = "011000015",
                    AccountNumberProtected = sensitive.Protect(acct), AccountLast4 = acct[^4..],
                    AccountType = BankAccountType.Checking, SplitType = DepositSplitType.Remainder
                });
            }
            if (e.IsContractor) continue;
            if (e.EmploymentType == EmploymentType.FullTime)
            {
                db.EmployeeDeductions.Add(new EmployeeDeduction { EmployeeId = e.Id, DeductionTypeId = types["MED"].Id, Amount = e.FilingStatus == FilingStatus.Single ? 95m : 240m, EmployerAmount = 310m });
                db.EmployeeDeductions.Add(new EmployeeDeduction { EmployeeId = e.Id, DeductionTypeId = types["DENT"].Id, Amount = 12.50m });
                if (rnd.Next(3) > 0)
                    db.EmployeeDeductions.Add(new EmployeeDeduction { EmployeeId = e.Id, DeductionTypeId = types["401K"].Id, IsPercentOfGross = true, Amount = rnd.Next(3, 9) });
            }
            foreach (var p in policies)
                db.TimeOffBalances.Add(new TimeOffBalance { EmployeeId = e.Id, TimeOffPolicyId = p.Id, Balance = rnd.Next(8, 60) });
        }
        db.EmployeeDeductions.Add(new EmployeeDeduction { EmployeeId = employees[7].Id, DeductionTypeId = types["ROTH"].Id, Amount = 150m });

        // Approved time for hourly staff over the last two pay periods
        var reg = await db.EarningTypes.FirstAsync(t => t.Code == EarningType.Regular);
        var (curStart, _) = PayPeriods.Containing(company.DefaultPayFrequency, today, company.WorkWeekStart);
        var from = curStart.AddDays(-28);
        foreach (var e in employees.Where(e => e.PayType == PayType.Hourly))
        {
            for (var d = from; d < today; d = d.AddDays(1))
            {
                if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                var hours = e.EmploymentType == EmploymentType.PartTime ? 4m : 8m + (rnd.Next(5) == 0 ? 2m : 0m);
                db.TimeEntries.Add(new TimeEntry
                {
                    EmployeeId = e.Id, WorkDate = d, EarningTypeId = reg.Id, Hours = hours, Source = "Sample",
                    Status = d < today.AddDays(-2) ? ApprovalStatus.Approved : ApprovalStatus.Pending,
                    ApprovedBy = "sample", ApprovedUtc = DateTime.UtcNow
                });
            }
        }

        db.TimeOffRequests.Add(new TimeOffRequest
        {
            EmployeeId = employees[3].Id, TimeOffPolicyId = policies[0].Id, StartDate = today.AddDays(14), EndDate = today.AddDays(15),
            Hours = 16, Reason = "Family visit"
        });
        db.TimeOffRequests.Add(new TimeOffRequest
        {
            EmployeeId = employees[6].Id, TimeOffPolicyId = policies[0].Id, StartDate = today.AddDays(30), EndDate = today.AddDays(34),
            Hours = 40, Reason = "Vacation"
        });

        foreach (var y in new[] { today.Year, today.Year + 1 })
        {
            if (await db.Holidays.AnyAsync(h => h.Date.Year == y)) continue;
            db.Holidays.AddRange(UsHolidays(y));
        }

        db.Announcements.Add(new Announcement
        {
            Title = "Welcome to WageHarbor",
            Body = "Pay stubs, W-4 updates, time off and your timesheet are all in one place now. Use the menu on the left to explore.",
            IsPinned = true, PostedBy = "HR"
        });

        await db.SaveChangesAsync();
        return employees.Count;
    }

    /// <summary>Common small-business paid holidays, observed on the nearest weekday.</summary>
    public static IEnumerable<Holiday> UsHolidays(int year)
    {
        static DateOnly Observed(DateOnly d) => d.DayOfWeek switch
        {
            DayOfWeek.Saturday => d.AddDays(-1),
            DayOfWeek.Sunday => d.AddDays(1),
            _ => d
        };
        static DateOnly Nth(int y, int m, DayOfWeek dow, int n)
        {
            var d = new DateOnly(y, m, 1);
            while (d.DayOfWeek != dow) d = d.AddDays(1);
            return d.AddDays(7 * (n - 1));
        }
        static DateOnly Last(int y, int m, DayOfWeek dow)
        {
            var d = new DateOnly(y, m, DateTime.DaysInMonth(y, m));
            while (d.DayOfWeek != dow) d = d.AddDays(-1);
            return d;
        }
        yield return new Holiday { Date = Observed(new DateOnly(year, 1, 1)), Name = "New Year's Day" };
        yield return new Holiday { Date = Nth(year, 1, DayOfWeek.Monday, 3), Name = "Martin Luther King Jr. Day" };
        yield return new Holiday { Date = Last(year, 5, DayOfWeek.Monday), Name = "Memorial Day" };
        yield return new Holiday { Date = Observed(new DateOnly(year, 7, 4)), Name = "Independence Day" };
        yield return new Holiday { Date = Nth(year, 9, DayOfWeek.Monday, 1), Name = "Labor Day" };
        yield return new Holiday { Date = Nth(year, 11, DayOfWeek.Thursday, 4), Name = "Thanksgiving" };
        yield return new Holiday { Date = Nth(year, 11, DayOfWeek.Thursday, 4).AddDays(1), Name = "Day after Thanksgiving" };
        yield return new Holiday { Date = Observed(new DateOnly(year, 12, 25)), Name = "Christmas Day" };
    }
}
