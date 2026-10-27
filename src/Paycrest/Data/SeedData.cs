using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Paycrest.Domain;
using Paycrest.Services;

namespace Paycrest.Data;

/// <summary>Reference data required for the app to work. Runs at startup and only inserts what is missing.</summary>
public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        if (!await db.Companies.AnyAsync())
            db.Companies.Add(new Company { Name = "My Company", State = "TX" });

        if (!await db.EarningTypes.AnyAsync())
        {
            db.EarningTypes.AddRange(
                new EarningType { Code = EarningType.Regular, Name = "Regular", IsSystem = true },
                new EarningType { Code = EarningType.Overtime, Name = "Overtime", Multiplier = 1.5m, IsSystem = true },
                new EarningType { Code = "DT", Name = "Double time", Multiplier = 2m },
                new EarningType { Code = "HOL", Name = "Holiday" },
                new EarningType { Code = "PTO", Name = "Paid time off", IsSystem = true },
                new EarningType { Code = "SICK", Name = "Sick pay", IsSystem = true },
                new EarningType { Code = "BONUS", Name = "Bonus", IsHoursBased = false, IsSupplemental = true },
                new EarningType { Code = "COMM", Name = "Commission", IsHoursBased = false, IsSupplemental = true },
                new EarningType { Code = "REIMB", Name = "Expense reimbursement", IsHoursBased = false, IsTaxable = false });
            await db.SaveChangesAsync();
        }

        if (!await db.DeductionTypes.AnyAsync())
        {
            db.DeductionTypes.AddRange(
                new DeductionType { Code = "401K", Name = "401(k)", Category = DeductionCategory.Retirement401k, ReducesFederalTaxable = true, ReducesStateTaxable = true, W2Box12Code = "D", AnnualLimit = 24500m, EmployerMatchRate = 0.5m, EmployerMatchCapPercent = 0.06m },
                new DeductionType { Code = "ROTH", Name = "Roth 401(k)", Category = DeductionCategory.Roth401k, W2Box12Code = "AA", AnnualLimit = 24500m },
                new DeductionType { Code = "MED", Name = "Medical insurance", Category = DeductionCategory.HealthInsurance, ReducesFederalTaxable = true, ReducesFicaTaxable = true, ReducesStateTaxable = true },
                new DeductionType { Code = "DENT", Name = "Dental & vision", Category = DeductionCategory.DentalVision, ReducesFederalTaxable = true, ReducesFicaTaxable = true, ReducesStateTaxable = true },
                new DeductionType { Code = "HSA", Name = "Health savings account", Category = DeductionCategory.Hsa, ReducesFederalTaxable = true, ReducesFicaTaxable = true, ReducesStateTaxable = true, W2Box12Code = "W" },
                new DeductionType { Code = "FSA", Name = "Flexible spending account", Category = DeductionCategory.Fsa, ReducesFederalTaxable = true, ReducesFicaTaxable = true, ReducesStateTaxable = true, AnnualLimit = 3400m },
                new DeductionType { Code = "LIFE", Name = "Voluntary life insurance", Category = DeductionCategory.LifeInsurance },
                new DeductionType { Code = "GARN", Name = "Wage garnishment", Category = DeductionCategory.Garnishment },
                new DeductionType { Code = "LOAN", Name = "Employee loan repayment", Category = DeductionCategory.Loan });
            await db.SaveChangesAsync();
        }

        if (!await db.TimeOffPolicies.AnyAsync())
        {
            var pto = await db.EarningTypes.FirstAsync(e => e.Code == "PTO");
            var sick = await db.EarningTypes.FirstAsync(e => e.Code == "SICK");
            db.TimeOffPolicies.AddRange(
                new TimeOffPolicy { Name = "Vacation", EarningTypeId = pto.Id, AccrualMethod = AccrualMethod.PerPayPeriod, AccrualRate = 3.0769m, MaxBalance = 160m },
                new TimeOffPolicy { Name = "Sick leave", EarningTypeId = sick.Id, AccrualMethod = AccrualMethod.PerHourWorked, AccrualRate = 0.0333m, MaxBalance = 48m });
        }

        await SeedTaxYearAsync(db, Tax2025());
        await SeedTaxYearAsync(db, Tax2026());
        await SeedStateRulesAsync(db, 2025);
        await SeedStateRulesAsync(db, 2026);
        await db.SaveChangesAsync();
    }

    private static async Task SeedTaxYearAsync(ApplicationDbContext db, TaxYear ty)
    {
        if (await db.TaxYears.AnyAsync(t => t.Year == ty.Year)) return;
        db.TaxYears.Add(ty);
    }

    private static List<FederalTaxBracket> Brackets(int year, FilingStatus status, params decimal[] thresholds)
    {
        decimal[] rates = [0.10m, 0.12m, 0.22m, 0.24m, 0.32m, 0.35m, 0.37m];
        return thresholds.Select((t, i) => new FederalTaxBracket { Year = year, FilingStatus = status, Threshold = t, Rate = rates[i] }).ToList();
    }

    private static TaxYear Tax2025() => new()
    {
        Year = 2025,
        SocialSecurityWageBase = 176100m,
        StandardDeductionSingle = 15750m,
        StandardDeductionMarried = 31500m,
        StandardDeductionHeadOfHousehold = 23625m,
        Notes = "2025 values (incl. July 2025 standard deduction increase). Verify against IRS Pub. 15-T.",
        Brackets =
        [
            .. Brackets(2025, FilingStatus.Single, 0, 11925, 48475, 103350, 197300, 250525, 626350),
            .. Brackets(2025, FilingStatus.MarriedFilingJointly, 0, 23850, 96950, 206700, 394600, 501050, 751600),
            .. Brackets(2025, FilingStatus.HeadOfHousehold, 0, 17000, 64850, 103350, 197300, 250500, 626350)
        ]
    };

    private static TaxYear Tax2026() => new()
    {
        Year = 2026,
        SocialSecurityWageBase = 184500m,
        StandardDeductionSingle = 16100m,
        StandardDeductionMarried = 32200m,
        StandardDeductionHeadOfHousehold = 24150m,
        Notes = "2026 values per IRS Rev. Proc. 2025-32 and SSA. Verify against IRS Pub. 15-T (2026).",
        Brackets =
        [
            .. Brackets(2026, FilingStatus.Single, 0, 12400, 50400, 105700, 201775, 256225, 640600),
            .. Brackets(2026, FilingStatus.MarriedFilingJointly, 0, 24800, 100800, 211400, 403550, 512450, 768700),
            .. Brackets(2026, FilingStatus.HeadOfHousehold, 0, 17700, 67450, 105700, 201750, 256200, 640600)
        ]
    };

    private static readonly string[] NoIncomeTaxStates = ["AK", "FL", "NV", "NH", "SD", "TN", "TX", "WA", "WY"];

    /// <summary>Simplified flat-rate withholding for flat-tax states. Rates are approximate starting points; verify with each state.</summary>
    private static Dictionary<string, decimal> FlatRates(int year) => new()
    {
        ["AZ"] = 0.025m, ["CO"] = 0.044m, ["GA"] = 0.0519m, ["ID"] = 0.053m, ["IL"] = 0.0495m,
        ["IN"] = year >= 2026 ? 0.0295m : 0.03m, ["IA"] = 0.038m, ["KY"] = year >= 2026 ? 0.035m : 0.04m,
        ["LA"] = 0.03m, ["MI"] = 0.0425m, ["MS"] = 0.044m, ["NC"] = year >= 2026 ? 0.0399m : 0.0425m,
        ["PA"] = 0.0307m, ["UT"] = 0.045m
    };

    private static async Task SeedStateRulesAsync(ApplicationDbContext db, int year)
    {
        if (await db.StateTaxRules.AnyAsync(s => s.Year == year)) return;
        foreach (var st in NoIncomeTaxStates)
            db.StateTaxRules.Add(new StateTaxRule { StateCode = st, Year = year, Method = StateTaxMethod.NoIncomeTax, Notes = "No state income tax on wages." });
        foreach (var (st, rate) in FlatRates(year))
            db.StateTaxRules.Add(new StateTaxRule
            {
                StateCode = st, Year = year, Method = StateTaxMethod.FlatRate, FlatRate = rate,
                Notes = "Simplified flat-rate withholding. Verify rate, exemptions and local taxes with the state."
            });
    }
}
