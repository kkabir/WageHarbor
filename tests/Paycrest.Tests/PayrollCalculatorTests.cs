using Paycrest.Domain;
using Paycrest.Services.Payroll;

namespace Paycrest.Tests;

public class PayrollCalculatorTests
{
    private static readonly decimal[] Rates = [0.10m, 0.12m, 0.22m, 0.24m, 0.32m, 0.35m, 0.37m];

    private static TaxYear Tax2026()
    {
        var ty = new TaxYear
        {
            Year = 2026, SocialSecurityWageBase = 184500m,
            StandardDeductionSingle = 16100m, StandardDeductionMarried = 32200m, StandardDeductionHeadOfHousehold = 24150m
        };
        void Add(FilingStatus s, params decimal[] t) =>
            ty.Brackets.AddRange(t.Select((x, i) => new FederalTaxBracket { Year = 2026, FilingStatus = s, Threshold = x, Rate = Rates[i] }));
        Add(FilingStatus.Single, 0, 12400, 50400, 105700, 201775, 256225, 640600);
        Add(FilingStatus.MarriedFilingJointly, 0, 24800, 100800, 211400, 403550, 512450, 768700);
        Add(FilingStatus.HeadOfHousehold, 0, 17700, 67450, 105700, 201750, 256200, 640600);
        return ty;
    }

    private static readonly EarningType Reg = new() { Id = 1, Code = EarningType.Regular, Name = "Regular" };
    private static readonly EarningType Ot = new() { Id = 2, Code = EarningType.Overtime, Name = "Overtime", Multiplier = 1.5m };
    private static readonly EarningType Bonus = new() { Id = 3, Code = "BONUS", Name = "Bonus", IsHoursBased = false, IsSupplemental = true };

    private static Employee Salaried(decimal annual = 52000m) => new()
    {
        Id = 1, FirstName = "Test", LastName = "Person", PayType = PayType.Salary, PayRate = annual,
        PayFrequency = PayFrequency.BiWeekly, DefaultHours = 80, WorkState = "TX", HireDate = new DateOnly(2020, 1, 1)
    };

    private static PayCalcInput Input(Employee e, YtdTotals? ytd = null, IReadOnlyList<TimeEntry>? time = null,
        IReadOnlyList<EmployeeDeduction>? deductions = null, IReadOnlyList<OneTimeEarning>? oneTime = null,
        StateTaxRule? state = null) => new()
    {
        Employee = e,
        Company = new Company { Name = "Co", SutaRate = 0.027m, SutaWageBase = 9000m },
        TaxYear = Tax2026(),
        StateRule = state ?? new StateTaxRule { StateCode = "TX", Method = StateTaxMethod.NoIncomeTax },
        PeriodStart = new DateOnly(2026, 3, 1),
        PeriodEnd = new DateOnly(2026, 3, 14),
        PayDate = new DateOnly(2026, 3, 20),
        TimeEntries = time ?? [],
        Deductions = deductions ?? [],
        OneTimeEarnings = oneTime ?? [],
        Ytd = ytd ?? YtdTotals.Empty,
        RegularEarning = Reg,
        OvertimeEarning = Ot
    };

    [Fact]
    public void Federal_withholding_single_biweekly_matches_percentage_method()
    {
        // (2,000 × 26 − 16,100) = 35,900 → 1,240 + 12% × 23,500 = 4,060 / 26 = 156.15
        var fit = PayrollCalculator.FederalWithholding(2000m, 26, Salaried(), Tax2026());
        Assert.Equal(156.15m, fit);
    }

    [Fact]
    public void Step2_checkbox_halves_standard_deduction_and_brackets()
    {
        var e = Salaried(); e.W4MultipleJobs = true;
        // 52,000 − 8,050 = 43,950 → 620 + 12% × 19,000 + 22% × 18,750 = 7,025 / 26 = 270.19
        Assert.Equal(270.19m, PayrollCalculator.FederalWithholding(2000m, 26, e, Tax2026()));
    }

    [Fact]
    public void Dependent_credit_and_extra_withholding_are_applied()
    {
        var e = Salaried(); e.FilingStatus = FilingStatus.MarriedFilingJointly; e.W4DependentsAmount = 2000; e.W4ExtraWithholding = 25;
        // 52,000 − 32,200 = 19,800 → 1,980 − 2,000 credit = 0, plus 25 extra
        Assert.Equal(25m, PayrollCalculator.FederalWithholding(2000m, 26, e, Tax2026()));
    }

    [Fact]
    public void Salary_paycheck_gross_to_net()
    {
        var check = PayrollCalculator.Calculate(Input(Salaried()), []);
        Assert.Equal(2000m, check.GrossPay);
        Assert.Equal(156.15m, check.FederalIncomeTax);
        Assert.Equal(124.00m, check.SocialSecurityTax);
        Assert.Equal(29.00m, check.MedicareTax);
        Assert.Equal(0m, check.StateIncomeTax);
        Assert.Equal(2000m - 156.15m - 124m - 29m, check.NetPay);
        Assert.Equal(12.00m, check.FutaTax);      // 0.6% of 2,000
        Assert.Equal(54.00m, check.SutaTax);      // 2.7% of 2,000
    }

    [Fact]
    public void Social_security_stops_at_wage_base()
    {
        var warnings = new List<string>();
        var check = PayrollCalculator.Calculate(Input(Salaried(), new YtdTotals { SocialSecurityWages = 184000m, FutaWages = 7000m, SutaWages = 9000m }), warnings);
        Assert.Equal(500m, check.SocialSecurityWages);
        Assert.Equal(31.00m, check.SocialSecurityTax);
        Assert.Equal(0m, check.FutaTax);
        Assert.Equal(0m, check.SutaTax);
        Assert.Contains(warnings, w => w.Contains("wage base"));
    }

    [Fact]
    public void Additional_medicare_applies_only_above_threshold()
    {
        var check = PayrollCalculator.Calculate(Input(Salaried(), new YtdTotals { MedicareWages = 199000m }), []);
        Assert.Equal(9.00m, check.AdditionalMedicareTax); // 0.9% × 1,000 over 200k
    }

    [Fact]
    public void Hourly_overtime_is_calculated_per_workweek()
    {
        var e = Salaried(); e.PayType = PayType.Hourly; e.PayRate = 20m;
        // Week 1 (Sun Mar 1): 5 × 9h = 45h → 5h OT. Week 2: 4 × 8h = 32h.
        var time = Enumerable.Range(2, 5).Select(d => Entry(new DateOnly(2026, 3, d), 9))
            .Concat(Enumerable.Range(9, 4).Select(d => Entry(new DateOnly(2026, 3, d), 8))).ToList();
        var check = PayrollCalculator.Calculate(Input(e, time: time), []);
        var reg = check.Lines.Single(l => l.Code == EarningType.Regular);
        var ot = check.Lines.Single(l => l.Code == EarningType.Overtime);
        Assert.Equal(72m, reg.Hours);
        Assert.Equal(5m, ot.Hours);
        Assert.Equal(30m, ot.Rate);
        Assert.Equal(72 * 20m + 5 * 30m, check.GrossPay);
    }

    [Fact]
    public void Exempt_employees_do_not_get_overtime()
    {
        var e = Salaried(); e.PayType = PayType.Hourly; e.PayRate = 20m; e.IsExempt = true;
        var time = Enumerable.Range(2, 5).Select(d => Entry(new DateOnly(2026, 3, d), 10)).ToList();
        var check = PayrollCalculator.Calculate(Input(e, time: time), []);
        Assert.DoesNotContain(check.Lines, l => l.Code == EarningType.Overtime);
        Assert.Equal(1000m, check.GrossPay);
    }

    [Fact]
    public void Pretax_401k_reduces_federal_but_not_fica_and_gets_match()
    {
        var k401 = new DeductionType { Id = 10, Code = "401K", Name = "401(k)", ReducesFederalTaxable = true, ReducesStateTaxable = true, EmployerMatchRate = 0.5m, EmployerMatchCapPercent = 0.06m };
        var ded = new EmployeeDeduction { DeductionTypeId = 10, DeductionType = k401, IsPercentOfGross = true, Amount = 10 };
        var check = PayrollCalculator.Calculate(Input(Salaried(), deductions: [ded]), []);
        Assert.Equal(200m, check.PreTaxDeductions);
        Assert.Equal(1800m, check.FederalTaxableWages);
        Assert.Equal(2000m, check.SocialSecurityWages);
        Assert.Equal(100m, check.EmployerContributions); // 50% of 200, under 6% cap (120)
    }

    [Fact]
    public void Section125_reduces_fica()
    {
        var med = new DeductionType { Id = 11, Code = "MED", Name = "Medical", ReducesFederalTaxable = true, ReducesFicaTaxable = true, ReducesStateTaxable = true };
        var check = PayrollCalculator.Calculate(Input(Salaried(), deductions: [new EmployeeDeduction { DeductionTypeId = 11, DeductionType = med, Amount = 100 }]), []);
        Assert.Equal(1900m, check.SocialSecurityWages);
        Assert.Equal(117.80m, check.SocialSecurityTax);
    }

    [Fact]
    public void Annual_limit_caps_deduction()
    {
        var k401 = new DeductionType { Id = 10, Code = "401K", Name = "401(k)", ReducesFederalTaxable = true, AnnualLimit = 24500m };
        var ytd = new YtdTotals { DeductionsByType = new Dictionary<int, decimal> { [10] = 24400m } };
        var warnings = new List<string>();
        var check = PayrollCalculator.Calculate(Input(Salaried(), ytd, deductions: [new EmployeeDeduction { DeductionTypeId = 10, DeductionType = k401, Amount = 500 }]), warnings);
        Assert.Equal(100m, check.PreTaxDeductions);
        Assert.Contains(warnings, w => w.Contains("annual limit"));
    }

    [Fact]
    public void Post_tax_deduction_never_makes_net_negative()
    {
        var garn = new DeductionType { Id = 12, Code = "GARN", Name = "Garnishment" };
        var check = PayrollCalculator.Calculate(Input(Salaried(), deductions: [new EmployeeDeduction { DeductionTypeId = 12, DeductionType = garn, Amount = 5000 }]), []);
        Assert.Equal(0m, check.NetPay);
        Assert.True(check.PostTaxDeductions < 2000m);
    }

    [Fact]
    public void Bonus_is_withheld_at_supplemental_rate()
    {
        var input = Input(Salaried(), oneTime: [new OneTimeEarning { EarningType = Bonus, Amount = 1000m }]);
        var check = PayrollCalculator.Calculate(input, []);
        Assert.Equal(3000m, check.GrossPay);
        Assert.Equal(156.15m + 220m, check.FederalIncomeTax);
    }

    [Fact]
    public void Contractor_has_no_withholding()
    {
        var e = Salaried(); e.EmploymentType = EmploymentType.Contractor1099;
        var check = PayrollCalculator.Calculate(Input(e), []);
        Assert.Equal(check.GrossPay, check.NetPay);
        Assert.Equal(0m, check.EmployeeTaxes);
        Assert.Equal(0m, check.FutaTax);
    }

    [Fact]
    public void Flat_state_tax_is_annualized()
    {
        var e = Salaried(); e.WorkState = "IL"; e.StateAllowances = 1;
        var il = new StateTaxRule { StateCode = "IL", Method = StateTaxMethod.FlatRate, FlatRate = 0.0495m, AllowanceAmount = 2600m };
        var check = PayrollCalculator.Calculate(Input(e, state: il), []);
        // (52,000 − 2,600) × 4.95% / 26 = 94.05
        Assert.Equal(94.05m, check.StateIncomeTax);
    }

    [Fact]
    public void Missing_state_rule_warns()
    {
        var e = Salaried(); e.WorkState = "CA";
        var warnings = new List<string>();
        var input = Input(e) is var i ? new PayCalcInput
        {
            Employee = i.Employee, Company = i.Company, TaxYear = i.TaxYear, StateRule = null, PeriodStart = i.PeriodStart,
            PeriodEnd = i.PeriodEnd, PayDate = i.PayDate, RegularEarning = Reg, OvertimeEarning = Ot
        } : null!;
        var check = PayrollCalculator.Calculate(input, warnings);
        Assert.Equal(0m, check.StateIncomeTax);
        Assert.Contains(warnings, w => w.Contains("no state tax rule"));
    }

    [Fact]
    public void New_hire_salary_is_prorated()
    {
        var e = Salaried(); e.HireDate = new DateOnly(2026, 3, 9); // second week of a 10-weekday period
        Assert.Equal(0.5m, PayrollCalculator.ProrationFactor(e, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 14)));
        Assert.Equal(1000m, PayrollCalculator.Calculate(Input(e), []).GrossPay);
    }

    [Fact]
    public void Deposits_split_fixed_then_remainder()
    {
        var accounts = new[]
        {
            new EmployeeBankAccount { RoutingNumber = "011000015", AccountLast4 = "1111", SplitType = DepositSplitType.Remainder },
            new EmployeeBankAccount { RoutingNumber = "011000015", AccountLast4 = "2222", SplitType = DepositSplitType.FixedAmount, SplitValue = 100m, AccountType = BankAccountType.Savings }
        };
        var d = PayrollCalculator.AllocateDeposits(1500m, accounts);
        Assert.Equal(2, d.Count);
        Assert.Equal(("2222", 100m), (d[0].AccountLast4, d[0].Amount));
        Assert.Equal(("1111", 1400m), (d[1].AccountLast4, d[1].Amount));
    }

    [Theory]
    [InlineData(PayFrequency.SemiMonthly, "2026-03-20", "2026-03-16", "2026-03-31")]
    [InlineData(PayFrequency.Monthly, "2026-02-10", "2026-02-01", "2026-02-28")]
    [InlineData(PayFrequency.Weekly, "2026-03-04", "2026-03-01", "2026-03-07")]
    public void Pay_periods_contain_date(PayFrequency f, string date, string start, string end)
    {
        var (s, e) = PayPeriods.Containing(f, DateOnly.Parse(date));
        Assert.Equal(DateOnly.Parse(start), s);
        Assert.Equal(DateOnly.Parse(end), e);
    }

    [Fact]
    public void Pay_date_moves_off_weekend()
    {
        // Mar 14 2026 + 7 = Sat Mar 21 → Fri Mar 20
        Assert.Equal(new DateOnly(2026, 3, 20), PayPeriods.PayDate(new DateOnly(2026, 3, 14), 7));
    }

    private static TimeEntry Entry(DateOnly d, decimal h) =>
        new() { WorkDate = d, Hours = h, EarningType = Reg, EarningTypeId = Reg.Id, Status = ApprovalStatus.Approved };
}
