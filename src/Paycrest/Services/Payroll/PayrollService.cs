using Microsoft.EntityFrameworkCore;
using Paycrest.Data;
using Paycrest.Domain;

namespace Paycrest.Services.Payroll;

public sealed class PayrollService(Db dbs, CurrentUser user, SensitiveData sensitive)
{
    public async Task<(DateOnly Start, DateOnly End, DateOnly PayDate)> SuggestNextPeriodAsync(PayFrequency freq, DateOnly today)
    {
        await using var db = await dbs.OpenAsync();
        var company = await db.Companies.FirstAsync();
        var lastEnd = await db.PayrollRuns
            .Where(r => r.PayFrequency == freq && r.RunType == PayrollRunType.Regular && r.Status != PayrollRunStatus.Voided)
            .MaxAsync(r => (DateOnly?)r.PeriodEnd);

        var (start, end) = lastEnd.HasValue
            ? PayPeriods.Next(freq, lastEnd.Value)
            : PayPeriods.Containing(freq, today.AddDays(-7), company.WorkWeekStart);
        var holidays = await db.Holidays.Select(h => h.Date).ToListAsync();
        return (start, end, PayPeriods.PayDate(end, company.PayDateOffsetDays, holidays));
    }

    public async Task<PayrollRun> CreateRunAsync(PayrollRunType type, PayFrequency freq, DateOnly start, DateOnly end,
        DateOnly payDate, bool applyDeductions, string? notes)
    {
        if (end < start) throw new InvalidOperationException("Period end must be on or after period start.");
        await using var db = await dbs.OpenAsync();
        if (type == PayrollRunType.Regular &&
            await db.PayrollRuns.AnyAsync(r => r.RunType == PayrollRunType.Regular && r.PayFrequency == freq &&
                                               r.Status != PayrollRunStatus.Voided && r.PeriodStart <= end && r.PeriodEnd >= start))
            throw new InvalidOperationException("A regular payroll already exists that overlaps this period.");

        var run = new PayrollRun
        {
            RunType = type, PayFrequency = freq, PeriodStart = start, PeriodEnd = end, PayDate = payDate,
            ApplyDeductions = applyDeductions, Notes = notes, CreatedBy = await user.GetNameAsync()
        };
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync();
        if (type == PayrollRunType.Regular) await CalculateAsync(run.Id);
        return run;
    }

    /// <summary>(Re)builds every paycheck in a draft run from current employee, time, deduction and tax data.</summary>
    public async Task CalculateAsync(int runId)
    {
        await using var db = await dbs.OpenAsync();
        var run = await db.PayrollRuns.Include(r => r.Adjustments).ThenInclude(a => a.EarningType)
            .FirstAsync(r => r.Id == runId);
        if (run.Status != PayrollRunStatus.Draft) throw new InvalidOperationException("Only draft payrolls can be recalculated.");

        var company = await db.Companies.AsNoTracking().FirstAsync();
        var year = run.PayDate.Year;
        var taxYear = await db.TaxYears.AsNoTracking().Include(t => t.Brackets).FirstOrDefaultAsync(t => t.Year == year)
                      ?? throw new InvalidOperationException($"Federal tax tables for {year} are not configured. Add them under Settings › Tax tables (use 'Copy from prior year').");
        var stateRules = await db.StateTaxRules.AsNoTracking().Include(s => s.Brackets).Where(s => s.Year == year).ToListAsync();
        var earningTypes = await db.EarningTypes.AsNoTracking().ToListAsync();
        var reg = earningTypes.First(e => e.Code == EarningType.Regular);
        var ot = earningTypes.First(e => e.Code == EarningType.Overtime);

        IQueryable<Employee> q = db.Employees.AsNoTracking()
            .Include(e => e.BankAccounts)
            .Include(e => e.Deductions).ThenInclude(d => d.DeductionType);
        var adjEmployeeIds = run.Adjustments.Select(a => a.EmployeeId).Distinct().ToList();
        q = run.RunType == PayrollRunType.Regular
            ? q.Where(e => (e.PayFrequency == run.PayFrequency && e.HireDate <= run.PeriodEnd &&
                            (e.Status != EmployeeStatus.Terminated || (e.TerminationDate != null && e.TerminationDate >= run.PeriodStart)))
                           || adjEmployeeIds.Contains(e.Id))
            : q.Where(e => adjEmployeeIds.Contains(e.Id));
        var employees = await q.OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ToListAsync();
        var ids = employees.Select(e => e.Id).ToList();

        var time = await db.TimeEntries.AsNoTracking().Include(t => t.EarningType)
            .Where(t => ids.Contains(t.EmployeeId) && t.WorkDate >= run.PeriodStart && t.WorkDate <= run.PeriodEnd &&
                        (t.PayrollRunId == null || t.PayrollRunId == run.Id))
            .ToListAsync();
        var pendingTime = time.Count(t => t.Status == ApprovalStatus.Pending);

        var ytd = await LoadYtdAsync(db, ids, year, run.Id);

        // Remove previous calculation
        db.Paychecks.RemoveRange(db.Paychecks.Where(p => p.PayrollRunId == run.Id));
        await db.SaveChangesAsync();

        var warnings = new List<string>();
        if (pendingTime > 0) warnings.Add($"{pendingTime} time entr{(pendingTime == 1 ? "y is" : "ies are")} still pending approval and will not be paid.");

        foreach (var emp in employees)
        {
            var input = new PayCalcInput
            {
                Employee = emp,
                Company = company,
                TaxYear = taxYear,
                StateRule = stateRules.FirstOrDefault(s => s.StateCode == emp.WorkState),
                RunType = run.RunType,
                PeriodStart = run.PeriodStart,
                PeriodEnd = run.PeriodEnd,
                PayDate = run.PayDate,
                ApplyDeductions = run.ApplyDeductions,
                TimeEntries = run.RunType == PayrollRunType.Regular ? time.Where(t => t.EmployeeId == emp.Id).ToList() : [],
                OneTimeEarnings = run.Adjustments.Where(a => a.EmployeeId == emp.Id)
                    .Select(a => new OneTimeEarning { EarningType = a.EarningType!, Hours = a.Hours, Amount = a.Amount, Note = a.Note }).ToList(),
                Deductions = emp.Deductions,
                Ytd = ytd.GetValueOrDefault(emp.Id, YtdTotals.Empty),
                RegularEarning = reg,
                OvertimeEarning = ot
            };
            var empWarnings = new List<string>();
            var check = PayrollCalculator.Calculate(input, empWarnings);
            if (check.GrossPay == 0 && check.Lines.Count == 0) { warnings.AddRange(empWarnings); continue; }

            check.PayrollRunId = run.Id;
            if (emp.PaymentMethod == PaymentMethod.DirectDeposit)
            {
                check.Deposits = PayrollCalculator.AllocateDeposits(check.NetPay, emp.BankAccounts);
                if (check.Deposits.Count == 0 && check.NetPay > 0)
                {
                    check.PaymentMethod = PaymentMethod.Check;
                    empWarnings.Add($"{emp.FullName}: no active bank account — will be paid by check.");
                }
            }
            check.Warnings = empWarnings.Count > 0 ? string.Join("\n", empWarnings) : null;
            warnings.AddRange(empWarnings);
            db.Paychecks.Add(check);
        }
        await db.SaveChangesAsync();

        var checks = await db.Paychecks.Where(p => p.PayrollRunId == run.Id).ToListAsync();
        run.EmployeeCount = checks.Count;
        run.TotalGross = checks.Sum(c => c.GrossPay);
        run.TotalEmployeeTaxes = checks.Sum(c => c.EmployeeTaxes);
        run.TotalDeductions = checks.Sum(c => c.PreTaxDeductions + c.PostTaxDeductions);
        run.TotalNet = checks.Sum(c => c.NetPay);
        run.TotalEmployerTaxes = checks.Sum(c => c.EmployerSocialSecurity + c.EmployerMedicare + c.FutaTax + c.SutaTax);
        run.TotalEmployerContributions = checks.Sum(c => c.EmployerContributions);
        run.Warnings = warnings.Count > 0 ? string.Join("\n", warnings.Distinct()) : null;
        run.CalculatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static async Task<Dictionary<int, YtdTotals>> LoadYtdAsync(ApplicationDbContext db, List<int> employeeIds, int year, int excludeRunId)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);
        var paid = db.Paychecks.Where(p => employeeIds.Contains(p.EmployeeId) && p.PayDate >= start && p.PayDate <= end &&
                                           p.PayrollRunId != excludeRunId && p.PayrollRun!.Status == PayrollRunStatus.Paid);
        var sums = await paid.GroupBy(p => p.EmployeeId).Select(g => new
        {
            EmployeeId = g.Key,
            Gross = g.Sum(p => p.GrossPay),
            Ss = g.Sum(p => p.SocialSecurityWages),
            Med = g.Sum(p => p.MedicareWages),
            Futa = g.Sum(p => p.FutaWages),
            Suta = g.Sum(p => p.SutaWages)
        }).ToListAsync();
        var deductions = await db.PaycheckLines
            .Where(l => l.DeductionTypeId != null &&
                        (l.LineType == PaycheckLineType.PreTaxDeduction || l.LineType == PaycheckLineType.PostTaxDeduction) &&
                        paid.Select(p => p.Id).Contains(l.PaycheckId))
            .Join(db.Paychecks, l => l.PaycheckId, p => p.Id, (l, p) => new { p.EmployeeId, l.DeductionTypeId, l.Amount })
            .GroupBy(x => new { x.EmployeeId, x.DeductionTypeId })
            .Select(g => new { g.Key.EmployeeId, g.Key.DeductionTypeId, Amount = g.Sum(x => x.Amount) })
            .ToListAsync();

        return employeeIds.ToDictionary(id => id, id =>
        {
            var s = sums.FirstOrDefault(x => x.EmployeeId == id);
            return new YtdTotals
            {
                Gross = s?.Gross ?? 0,
                SocialSecurityWages = s?.Ss ?? 0,
                MedicareWages = s?.Med ?? 0,
                FutaWages = s?.Futa ?? 0,
                SutaWages = s?.Suta ?? 0,
                DeductionsByType = deductions.Where(d => d.EmployeeId == id).ToDictionary(d => d.DeductionTypeId!.Value, d => d.Amount)
            };
        });
    }

    public async Task AddAdjustmentAsync(int runId, int employeeId, int earningTypeId, decimal? hours, decimal amount, string? note)
    {
        await using var db = await dbs.OpenAsync();
        var run = await db.PayrollRuns.FirstAsync(r => r.Id == runId);
        if (!run.IsEditable) throw new InvalidOperationException("Payroll is not in draft.");
        db.PayrollAdjustments.Add(new PayrollAdjustment
        {
            PayrollRunId = runId, EmployeeId = employeeId, EarningTypeId = earningTypeId, Hours = hours, Amount = amount, Note = note
        });
        await db.SaveChangesAsync();
        await CalculateAsync(runId);
    }

    public async Task RemoveAdjustmentAsync(int adjustmentId)
    {
        await using var db = await dbs.OpenAsync();
        var adj = await db.PayrollAdjustments.Include(a => a.PayrollRun).FirstAsync(a => a.Id == adjustmentId);
        if (!adj.PayrollRun!.IsEditable) throw new InvalidOperationException("Payroll is not in draft.");
        db.PayrollAdjustments.Remove(adj);
        await db.SaveChangesAsync();
        await CalculateAsync(adj.PayrollRunId);
    }

    public async Task ApproveAsync(int runId)
    {
        await using var db = await dbs.OpenAsync();
        var run = await db.PayrollRuns.FirstAsync(r => r.Id == runId);
        if (run.Status != PayrollRunStatus.Draft) throw new InvalidOperationException("Only draft payrolls can be approved.");
        if (run.EmployeeCount == 0) throw new InvalidOperationException("Payroll has no paychecks.");
        if (await db.Paychecks.AnyAsync(p => p.PayrollRunId == runId && p.NetPay < 0))
            throw new InvalidOperationException("One or more paychecks have negative net pay. Fix them before approving.");
        run.Status = PayrollRunStatus.Approved;
        run.ApprovedBy = await user.GetNameAsync();
        run.ApprovedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ReopenAsync(int runId)
    {
        await using var db = await dbs.OpenAsync();
        var run = await db.PayrollRuns.FirstAsync(r => r.Id == runId);
        if (run.Status != PayrollRunStatus.Approved) throw new InvalidOperationException("Only approved payrolls can be reopened.");
        run.Status = PayrollRunStatus.Draft;
        run.ApprovedBy = null;
        run.ApprovedUtc = null;
        await db.SaveChangesAsync();
    }

    /// <summary>Finalizes the payroll: assigns check numbers, locks time, accrues time off.</summary>
    public async Task MarkPaidAsync(int runId)
    {
        await using var db = await dbs.OpenAsync();
        await db.InTransactionAsync(async () =>
        {
        var run = await db.PayrollRuns.Include(r => r.Paychecks).ThenInclude(p => p.Lines).FirstAsync(r => r.Id == runId);
        if (run.Status != PayrollRunStatus.Approved) throw new InvalidOperationException("Payroll must be approved before it is marked paid.");

        var company = await db.Companies.FirstAsync();
        foreach (var check in run.Paychecks.Where(p => p.PaymentMethod == PaymentMethod.Check).OrderBy(p => p.EmployeeName))
            check.CheckNumber = company.NextCheckNumber++;

        var employeeIds = run.Paychecks.Select(p => p.EmployeeId).ToList();
        if (run.RunType == PayrollRunType.Regular)
        {
            await db.TimeEntries
                .Where(t => employeeIds.Contains(t.EmployeeId) && t.WorkDate >= run.PeriodStart && t.WorkDate <= run.PeriodEnd &&
                            t.Status == ApprovalStatus.Approved && t.PayrollRunId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.PayrollRunId, run.Id));
            await AccrueTimeOffAsync(db, run);
        }

        run.Status = PayrollRunStatus.Paid;
        run.PaidUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        });
    }

    private async Task AccrueTimeOffAsync(ApplicationDbContext db, PayrollRun run)
    {
        var policies = await db.TimeOffPolicies.Where(p => p.IsActive &&
            (p.AccrualMethod == AccrualMethod.PerPayPeriod || p.AccrualMethod == AccrualMethod.PerHourWorked)).ToListAsync();
        if (policies.Count == 0) return;
        var employeeIds = run.Paychecks.Select(p => p.EmployeeId).ToList();
        var balances = await db.TimeOffBalances.Where(b => employeeIds.Contains(b.EmployeeId)).ToListAsync();
        var who = await user.GetNameAsync();

        foreach (var check in run.Paychecks)
        {
            var worked = check.Lines.Where(l => l.LineType == PaycheckLineType.Earning && (l.Code == EarningType.Regular || l.Code == EarningType.Overtime))
                .Sum(l => l.Hours ?? 0);
            foreach (var policy in policies)
            {
                var bal = balances.FirstOrDefault(b => b.EmployeeId == check.EmployeeId && b.TimeOffPolicyId == policy.Id);
                if (bal == null) continue; // employee not enrolled in this policy
                var hours = policy.AccrualMethod == AccrualMethod.PerPayPeriod ? policy.AccrualRate : Math.Round(worked * policy.AccrualRate, 4);
                if (policy.MaxBalance.HasValue) hours = Math.Min(hours, Math.Max(0, policy.MaxBalance.Value - bal.Balance));
                if (hours <= 0) continue;
                bal.Balance += hours;
                bal.AccruedYtd += hours;
                db.TimeOffTransactions.Add(new TimeOffTransaction
                {
                    EmployeeId = check.EmployeeId, TimeOffPolicyId = policy.Id, Date = run.PayDate, Hours = hours,
                    Kind = "Accrual", PayrollRunId = run.Id, Note = $"Payroll {run.PeriodLabel}", CreatedBy = who
                });
            }
        }
    }

    public async Task VoidAsync(int runId)
    {
        await using var db = await dbs.OpenAsync();
        await db.InTransactionAsync(async () =>
        {
        var run = await db.PayrollRuns.FirstAsync(r => r.Id == runId);
        if (run.Status is PayrollRunStatus.Draft or PayrollRunStatus.Voided) throw new InvalidOperationException("Delete draft payrolls instead of voiding them.");

        if (run.Status == PayrollRunStatus.Paid)
        {
            await db.TimeEntries.Where(t => t.PayrollRunId == runId).ExecuteUpdateAsync(s => s.SetProperty(t => t.PayrollRunId, (int?)null));
            var accruals = await db.TimeOffTransactions.Where(t => t.PayrollRunId == runId && t.Kind == "Accrual").ToListAsync();
            foreach (var a in accruals)
            {
                var bal = await db.TimeOffBalances.FirstOrDefaultAsync(b => b.EmployeeId == a.EmployeeId && b.TimeOffPolicyId == a.TimeOffPolicyId);
                if (bal != null) { bal.Balance -= a.Hours; bal.AccruedYtd -= a.Hours; }
                db.TimeOffTransactions.Add(new TimeOffTransaction
                {
                    EmployeeId = a.EmployeeId, TimeOffPolicyId = a.TimeOffPolicyId, Date = DateOnly.FromDateTime(DateTime.Today),
                    Hours = -a.Hours, Kind = "Reversal", PayrollRunId = runId, Note = "Payroll voided", CreatedBy = await user.GetNameAsync()
                });
            }
        }
        run.Status = PayrollRunStatus.Voided;
        await db.SaveChangesAsync();
        });
    }

    public async Task DeleteDraftAsync(int runId)
    {
        await using var db = await dbs.OpenAsync();
        var run = await db.PayrollRuns.FirstAsync(r => r.Id == runId);
        if (run.Status != PayrollRunStatus.Draft) throw new InvalidOperationException("Only drafts can be deleted.");
        db.PayrollRuns.Remove(run);
        await db.SaveChangesAsync();
    }

    public async Task<(string FileName, string Content)> BuildNachaAsync(int runId)
    {
        await using var db = await dbs.OpenAsync();
        var run = await db.PayrollRuns.AsNoTracking().FirstAsync(r => r.Id == runId);
        if (run.Status is not (PayrollRunStatus.Approved or PayrollRunStatus.Paid))
            throw new InvalidOperationException("Approve the payroll before generating the ACH file.");
        var company = await db.Companies.AsNoTracking().FirstAsync();
        var checks = await db.Paychecks.AsNoTracking().Include(p => p.Deposits).Include(p => p.Employee)
            .Where(p => p.PayrollRunId == runId && p.PaymentMethod == PaymentMethod.DirectDeposit).ToListAsync();

        var entries = checks.SelectMany(c => c.Deposits.Select(d => new NachaEntry(
            c.EmployeeName, c.Employee!.EmployeeNumber, d.RoutingNumber,
            sensitive.Unprotect(d.AccountNumberProtected) ?? throw new InvalidOperationException($"Cannot decrypt bank account for {c.EmployeeName}."),
            d.AccountType, d.Amount))).ToList();
        if (entries.Count == 0) throw new InvalidOperationException("No direct deposits in this payroll.");

        var content = NachaFileBuilder.Build(company, run.PayDate, entries);
        return ($"ACH_{run.PayDate:yyyyMMdd}_run{run.Id}.txt", content);
    }
}
