using Paycrest.Domain;

namespace Paycrest.Services.Payroll;

/// <summary>Year-to-date figures from paid paychecks before the current one.</summary>
public sealed class YtdTotals
{
    public decimal Gross { get; init; }
    public decimal SocialSecurityWages { get; init; }
    public decimal MedicareWages { get; init; }
    public decimal FutaWages { get; init; }
    public decimal SutaWages { get; init; }
    public IReadOnlyDictionary<int, decimal> DeductionsByType { get; init; } = new Dictionary<int, decimal>();
    public static readonly YtdTotals Empty = new();
}

public sealed class OneTimeEarning
{
    public required EarningType EarningType { get; init; }
    public decimal? Hours { get; init; }
    public decimal Amount { get; init; }
    public string? Note { get; init; }
}

public sealed class PayCalcInput
{
    public required Employee Employee { get; init; }
    public required Company Company { get; init; }
    public required TaxYear TaxYear { get; init; }
    public StateTaxRule? StateRule { get; init; }
    public PayrollRunType RunType { get; init; } = PayrollRunType.Regular;
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public DateOnly PayDate { get; init; }
    public bool ApplyDeductions { get; init; } = true;
    /// <summary>Approved time entries in the period, with EarningType loaded.</summary>
    public IReadOnlyList<TimeEntry> TimeEntries { get; init; } = [];
    public IReadOnlyList<OneTimeEarning> OneTimeEarnings { get; init; } = [];
    /// <summary>Employee deductions with DeductionType loaded.</summary>
    public IReadOnlyList<EmployeeDeduction> Deductions { get; init; } = [];
    public YtdTotals Ytd { get; init; } = YtdTotals.Empty;
    public required EarningType RegularEarning { get; init; }
    public required EarningType OvertimeEarning { get; init; }
}

/// <summary>
/// Gross-to-net payroll engine. Pure and deterministic: no database access, so it can be unit tested.
/// Federal withholding follows the IRS Pub. 15-T annualized percentage method for 2020+ Forms W-4.
/// </summary>
public static class PayrollCalculator
{
    public static Paycheck Calculate(PayCalcInput input, List<string> warnings)
    {
        var emp = input.Employee;
        var periods = (int)emp.PayFrequency;
        var check = new Paycheck
        {
            EmployeeId = emp.Id,
            EmployeeName = emp.FullName,
            PeriodStart = input.PeriodStart,
            PeriodEnd = input.PeriodEnd,
            PayDate = input.PayDate,
            PaymentMethod = emp.PaymentMethod,
            StateCode = emp.WorkState
        };

        // ---------------- 1. Earnings ----------------
        var earnings = new List<(PaycheckLine Line, EarningType Type)>();
        void AddEarning(EarningType type, decimal? hours, decimal? rate, decimal amount, string? description = null)
        {
            amount = Fmt.Round(amount);
            if (amount == 0 && (hours ?? 0) == 0) return;
            earnings.Add((new PaycheckLine
            {
                LineType = PaycheckLineType.Earning,
                Code = type.Code,
                Description = description ?? type.Name,
                Hours = hours,
                Rate = rate,
                Amount = amount,
                EarningTypeId = type.Id
            }, type));
        }

        if (input.RunType == PayrollRunType.Regular)
        {
            if (emp.PayType == PayType.Salary)
            {
                var factor = ProrationFactor(emp, input.PeriodStart, input.PeriodEnd);
                var amount = emp.PayRate / periods * factor;
                if (factor < 1m) warnings.Add($"{emp.FullName}: salary prorated to {factor:P0} for partial period.");
                AddEarning(input.RegularEarning, emp.DefaultHours > 0 ? Fmt.Round(emp.DefaultHours * factor) : null, null, amount, "Salary");
            }
            else
            {
                AddHourlyEarnings(input, AddEarning, warnings);
            }
        }

        foreach (var ot in input.OneTimeEarnings)
        {
            var rate = emp.PayType == PayType.Hourly ? emp.PayRate : HourlyEquivalent(emp);
            var amount = ot.Amount != 0 ? ot.Amount : (ot.Hours ?? 0) * rate * ot.EarningType.Multiplier;
            AddEarning(ot.EarningType, ot.Hours, ot.Amount != 0 ? null : rate * ot.EarningType.Multiplier, amount,
                string.IsNullOrWhiteSpace(ot.Note) ? null : $"{ot.EarningType.Name} – {ot.Note}");
        }

        check.Lines.AddRange(earnings.Select(e => e.Line));
        check.TotalHours = earnings.Where(e => e.Type.IsHoursBased).Sum(e => e.Line.Hours ?? 0);
        check.GrossPay = earnings.Sum(e => e.Line.Amount);
        var taxableGross = earnings.Where(e => e.Type.IsTaxable).Sum(e => e.Line.Amount);
        var supplementalGross = earnings.Where(e => e.Type.IsTaxable && e.Type.IsSupplemental).Sum(e => e.Line.Amount);

        if (check.GrossPay < 0) warnings.Add($"{emp.FullName}: gross pay is negative.");

        // 1099 contractors: no withholding or employer taxes.
        if (emp.IsContractor)
        {
            check.NetPay = check.GrossPay;
            return check;
        }

        // ---------------- 2. Pre-tax deductions ----------------
        var active = input.ApplyDeductions
            ? input.Deductions.Where(d => d.IsActive && d.DeductionType is { IsActive: true }
                                          && (d.StartDate == null || d.StartDate <= input.PeriodEnd)
                                          && (d.EndDate == null || d.EndDate >= input.PeriodStart)).ToList()
            : [];

        decimal reduceFed = 0, reduceFica = 0, reduceState = 0, preTotal = 0, employerContrib = 0;
        foreach (var d in active.Where(d => d.DeductionType!.IsPreTax))
        {
            var amt = DeductionAmount(d, check.GrossPay, input.Ytd, warnings, emp);
            amt = Math.Min(amt, Math.Max(0, taxableGross - preTotal));
            if (amt <= 0) continue;
            preTotal += amt;
            if (d.DeductionType!.ReducesFederalTaxable) reduceFed += amt;
            if (d.DeductionType.ReducesFicaTaxable) reduceFica += amt;
            if (d.DeductionType.ReducesStateTaxable) reduceState += amt;
            check.Lines.Add(DeductionLine(PaycheckLineType.PreTaxDeduction, d, amt));
            employerContrib += AddEmployerContribution(check, d, amt);
        }

        // ---------------- 3. Taxable wages ----------------
        var fedTaxable = Math.Max(0, taxableGross - reduceFed);
        var ficaTaxable = Math.Max(0, taxableGross - reduceFica);
        var stateTaxable = Math.Max(0, taxableGross - reduceState);
        var ty = input.TaxYear;
        check.FederalTaxableWages = fedTaxable;
        check.StateTaxableWages = stateTaxable;

        // ---------------- 4. Federal income tax ----------------
        if (!emp.FederalExempt)
        {
            var supplementalTaxable = Math.Min(supplementalGross, fedTaxable);
            var regularTaxable = fedTaxable - supplementalTaxable;
            var fit = FederalWithholding(regularTaxable, periods, emp, ty, input.RunType == PayrollRunType.Regular);
            fit += Fmt.Round(supplementalTaxable * ty.SupplementalRate);
            check.FederalIncomeTax = Math.Max(0, fit);
        }

        // ---------------- 5. FICA ----------------
        var ssRoom = Math.Max(0, ty.SocialSecurityWageBase - input.Ytd.SocialSecurityWages);
        check.SocialSecurityWages = Math.Min(ficaTaxable, ssRoom);
        check.SocialSecurityTax = Fmt.Round(check.SocialSecurityWages * ty.SocialSecurityRate);
        check.EmployerSocialSecurity = check.SocialSecurityTax;
        if (ficaTaxable > ssRoom) warnings.Add($"{emp.FullName}: reached the Social Security wage base.");

        check.MedicareWages = ficaTaxable;
        check.MedicareTax = Fmt.Round(ficaTaxable * ty.MedicareRate);
        check.EmployerMedicare = check.MedicareTax;
        var overBefore = Math.Max(0, input.Ytd.MedicareWages - ty.AdditionalMedicareThreshold);
        var overAfter = Math.Max(0, input.Ytd.MedicareWages + ficaTaxable - ty.AdditionalMedicareThreshold);
        check.AdditionalMedicareTax = Fmt.Round((overAfter - overBefore) * ty.AdditionalMedicareRate);

        // ---------------- 6. State income tax ----------------
        check.StateIncomeTax = StateWithholding(stateTaxable, periods, emp, input.StateRule, warnings);

        // ---------------- 7. Employer unemployment taxes ----------------
        check.FutaWages = Math.Min(ficaTaxable, Math.Max(0, ty.FutaWageBase - input.Ytd.FutaWages));
        check.FutaTax = Fmt.Round(check.FutaWages * ty.FutaRate);
        check.SutaWages = Math.Min(ficaTaxable, Math.Max(0, input.Company.SutaWageBase - input.Ytd.SutaWages));
        check.SutaTax = Fmt.Round(check.SutaWages * input.Company.SutaRate);

        AddTaxLine(check, PaycheckLineType.EmployeeTax, "FIT", "Federal income tax", check.FederalIncomeTax);
        AddTaxLine(check, PaycheckLineType.EmployeeTax, "SS", "Social Security", check.SocialSecurityTax);
        AddTaxLine(check, PaycheckLineType.EmployeeTax, "MED", "Medicare", check.MedicareTax);
        AddTaxLine(check, PaycheckLineType.EmployeeTax, "MEDADD", "Additional Medicare", check.AdditionalMedicareTax);
        AddTaxLine(check, PaycheckLineType.EmployeeTax, "SIT", $"{emp.WorkState} state income tax", check.StateIncomeTax);
        AddTaxLine(check, PaycheckLineType.EmployerTax, "ER_SS", "Employer Social Security", check.EmployerSocialSecurity);
        AddTaxLine(check, PaycheckLineType.EmployerTax, "ER_MED", "Employer Medicare", check.EmployerMedicare);
        AddTaxLine(check, PaycheckLineType.EmployerTax, "FUTA", "Federal unemployment (FUTA)", check.FutaTax);
        AddTaxLine(check, PaycheckLineType.EmployerTax, "SUTA", $"{input.Company.State ?? "State"} unemployment (SUTA)", check.SutaTax);

        check.EmployeeTaxes = check.FederalIncomeTax + check.SocialSecurityTax + check.MedicareTax +
                              check.AdditionalMedicareTax + check.StateIncomeTax;
        check.PreTaxDeductions = preTotal;

        // ---------------- 8. Post-tax deductions ----------------
        var available = check.GrossPay - preTotal - check.EmployeeTaxes;
        decimal postTotal = 0;
        foreach (var d in active.Where(d => !d.DeductionType!.IsPreTax))
        {
            var amt = DeductionAmount(d, check.GrossPay, input.Ytd, warnings, emp);
            if (amt > available - postTotal)
            {
                warnings.Add($"{emp.FullName}: {d.DeductionType!.Name} reduced to avoid negative net pay.");
                amt = Math.Max(0, available - postTotal);
            }
            if (amt <= 0) continue;
            postTotal += amt;
            check.Lines.Add(DeductionLine(PaycheckLineType.PostTaxDeduction, d, amt));
            employerContrib += AddEmployerContribution(check, d, amt);
        }

        check.PostTaxDeductions = postTotal;
        check.EmployerContributions = employerContrib;
        check.NetPay = check.GrossPay - preTotal - check.EmployeeTaxes - postTotal;
        if (check.NetPay < 0) warnings.Add($"{emp.FullName}: net pay is negative ({Fmt.Money(check.NetPay)}).");
        return check;
    }

    /// <summary>IRS Pub 15-T Worksheet 1A (annual percentage method) for 2020+ Forms W-4.</summary>
    public static decimal FederalWithholding(decimal taxablePerPeriod, int periods, Employee w4, TaxYear ty, bool includeExtra = true)
    {
        // Step 2 checkbox: the Pub 15-T "Step 2" table equals the standard table with every threshold halved.
        var factor = w4.W4MultipleJobs ? 0.5m : 1m;
        var annual = taxablePerPeriod * periods + w4.W4OtherIncome - w4.W4Deductions
                     - ty.StandardDeduction(w4.FilingStatus) * factor;
        var brackets = ty.Brackets.Where(b => b.FilingStatus == w4.FilingStatus)
            .Select(b => (b.Threshold * factor, b.Rate)).ToList();
        var annualTax = Math.Max(0, Progressive(annual, brackets) - w4.W4DependentsAmount);
        var perPeriod = Fmt.Round(annualTax / periods);
        if (includeExtra) perPeriod += w4.W4ExtraWithholding;
        return perPeriod;
    }

    public static decimal Progressive(decimal income, IEnumerable<(decimal Threshold, decimal Rate)> brackets)
    {
        if (income <= 0) return 0;
        var ordered = brackets.OrderBy(b => b.Threshold).ToList();
        decimal tax = 0;
        for (var i = 0; i < ordered.Count; i++)
        {
            var (floor, rate) = ordered[i];
            if (income <= floor) break;
            var ceiling = i + 1 < ordered.Count ? ordered[i + 1].Threshold : decimal.MaxValue;
            tax += (Math.Min(income, ceiling) - floor) * rate;
        }
        return tax;
    }

    public static decimal StateWithholding(decimal taxablePerPeriod, int periods, Employee emp, StateTaxRule? rule, List<string> warnings)
    {
        if (emp.StateExempt || taxablePerPeriod <= 0) return 0;
        if (rule == null || rule.Method == StateTaxMethod.NotConfigured)
        {
            warnings.Add($"{emp.FullName}: no state tax rule for {emp.WorkState} — nothing withheld. Configure it under Settings › Tax tables.");
            return 0;
        }
        if (rule.Method == StateTaxMethod.NoIncomeTax) return 0;

        var annual = taxablePerPeriod * periods - rule.StandardDeduction - rule.AllowanceAmount * emp.StateAllowances;
        decimal annualTax = rule.Method == StateTaxMethod.FlatRate
            ? Math.Max(0, annual) * rule.FlatRate
            : Progressive(annual, rule.Brackets
                .Where(b => b.FilingStatus == emp.StateFilingStatus)
                .Select(b => (b.Threshold, b.Rate)));
        return Fmt.Round(annualTax / periods) + emp.StateExtraWithholding;
    }

    private static void AddHourlyEarnings(PayCalcInput input, Action<EarningType, decimal?, decimal?, decimal, string?> add, List<string> warnings)
    {
        var emp = input.Employee;
        var entries = input.TimeEntries.Where(t => t.Status == ApprovalStatus.Approved && t.EarningType != null).ToList();
        var regularEntries = entries.Where(t => t.EarningType!.Code == EarningType.Regular).ToList();

        if (entries.Count == 0)
        {
            if (emp.DefaultHours > 0)
            {
                add(input.RegularEarning, emp.DefaultHours, emp.PayRate, emp.DefaultHours * emp.PayRate, null);
                warnings.Add($"{emp.FullName}: no approved time — paid default {Fmt.Hours(emp.DefaultHours)} hours.");
            }
            else
            {
                warnings.Add($"{emp.FullName}: no approved time entries for this period.");
            }
            return;
        }

        decimal regular = 0, overtime = 0;
        if (emp.IsExempt)
        {
            regular = regularEntries.Sum(t => t.Hours);
        }
        else
        {
            // FLSA: overtime for hours worked over the threshold in each workweek.
            foreach (var week in regularEntries.GroupBy(t => WeekStart(t.WorkDate, input.Company.WorkWeekStart)))
            {
                var hours = week.Sum(t => t.Hours);
                var ot = Math.Max(0, hours - input.Company.OvertimeThresholdHours);
                regular += hours - ot;
                overtime += ot;
            }
        }

        add(input.RegularEarning, regular, emp.PayRate, regular * emp.PayRate, null);
        if (overtime > 0)
        {
            var rate = emp.PayRate * input.OvertimeEarning.Multiplier;
            add(input.OvertimeEarning, overtime, rate, overtime * rate, null);
        }

        foreach (var g in entries.Where(t => t.EarningType!.Code != EarningType.Regular).GroupBy(t => t.EarningType!.Id))
        {
            var type = g.First().EarningType!;
            var hours = g.Sum(t => t.Hours);
            var rate = emp.PayRate * type.Multiplier;
            add(type, hours, rate, hours * rate, null);
        }
    }

    public static DateOnly WeekStart(DateOnly date, DayOfWeek start)
    {
        var diff = ((int)date.DayOfWeek - (int)start + 7) % 7;
        return date.AddDays(-diff);
    }

    public static decimal HourlyEquivalent(Employee e) =>
        e.PayType == PayType.Hourly ? e.PayRate : Fmt.Round(e.PayRate / 2080m);

    /// <summary>Fraction of weekdays in the period that the employee was employed (for new hires and terminations).</summary>
    public static decimal ProrationFactor(Employee e, DateOnly start, DateOnly end)
    {
        var from = e.HireDate > start ? e.HireDate : start;
        var to = e.TerminationDate.HasValue && e.TerminationDate < end ? e.TerminationDate.Value : end;
        if (from == start && to == end) return 1m;
        var total = Weekdays(start, end);
        if (total == 0 || to < from) return 0m;
        return Math.Round((decimal)Weekdays(from, to) / total, 4);
    }

    public static int Weekdays(DateOnly start, DateOnly end)
    {
        var n = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) n++;
        return n;
    }

    private static decimal DeductionAmount(EmployeeDeduction d, decimal gross, YtdTotals ytd, List<string> warnings, Employee emp)
    {
        var amt = Fmt.Round(d.IsPercentOfGross ? gross * d.Amount / 100m : d.Amount);
        var limit = d.AnnualLimit ?? d.DeductionType!.AnnualLimit;
        if (limit.HasValue)
        {
            var used = ytd.DeductionsByType.GetValueOrDefault(d.DeductionTypeId);
            var room = Math.Max(0, limit.Value - used);
            if (amt > room)
            {
                warnings.Add($"{emp.FullName}: {d.DeductionType!.Name} capped at annual limit {Fmt.Money(limit.Value)}.");
                amt = room;
            }
        }
        return amt;
    }

    private static decimal AddEmployerContribution(Paycheck check, EmployeeDeduction d, decimal employeeAmount)
    {
        var type = d.DeductionType!;
        var match = type.EmployerMatchRate > 0
            ? Math.Min(employeeAmount * type.EmployerMatchRate, check.GrossPay * type.EmployerMatchCapPercent)
            : 0;
        var total = Fmt.Round(match + d.EmployerAmount);
        if (total > 0)
        {
            check.Lines.Add(new PaycheckLine
            {
                LineType = PaycheckLineType.EmployerContribution,
                Code = "ER_" + type.Code,
                Description = $"Employer {type.Name}",
                Amount = total,
                DeductionTypeId = type.Id
            });
        }
        return total;
    }

    private static PaycheckLine DeductionLine(PaycheckLineType kind, EmployeeDeduction d, decimal amount) => new()
    {
        LineType = kind,
        Code = d.DeductionType!.Code,
        Description = d.DeductionType.Name + (d.IsPercentOfGross ? $" ({d.Amount:0.##}%)" : ""),
        Amount = amount,
        DeductionTypeId = d.DeductionTypeId
    };

    private static void AddTaxLine(Paycheck check, PaycheckLineType kind, string code, string description, decimal amount)
    {
        if (amount == 0) return;
        check.Lines.Add(new PaycheckLine { LineType = kind, Code = code, Description = description, Amount = amount });
    }

    /// <summary>Splits net pay across the employee's deposit accounts: fixed and percent first by priority, remainder last.</summary>
    public static List<PaycheckDeposit> AllocateDeposits(decimal netPay, IEnumerable<EmployeeBankAccount> accounts)
    {
        var result = new List<PaycheckDeposit>();
        var active = accounts.Where(a => a.IsActive).OrderBy(a => a.SplitType == DepositSplitType.Remainder).ThenBy(a => a.Priority).ToList();
        if (active.Count == 0 || netPay <= 0) return result;

        var remaining = netPay;
        foreach (var a in active)
        {
            var amt = a.SplitType switch
            {
                DepositSplitType.FixedAmount => Math.Min(a.SplitValue, remaining),
                DepositSplitType.Percent => Math.Min(Fmt.Round(netPay * a.SplitValue / 100m), remaining),
                _ => remaining
            };
            if (amt <= 0) continue;
            remaining -= amt;
            result.Add(new PaycheckDeposit
            {
                BankName = a.BankName,
                RoutingNumber = a.RoutingNumber,
                AccountNumberProtected = a.AccountNumberProtected,
                AccountLast4 = a.AccountLast4,
                AccountType = a.AccountType,
                Amount = amt
            });
        }
        // No remainder account: put what is left into the last account.
        if (remaining > 0 && result.Count > 0) result[^1].Amount += remaining;
        return result;
    }
}
