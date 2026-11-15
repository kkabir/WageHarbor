using Paycrest.Domain;
using Paycrest.Services;

namespace Paycrest.Tests;

public class GeneralLedgerTests
{
    // Two paychecks whose pieces add up the way PayrollCalculator produces them: gross = net + employee taxes + deductions.
    private static readonly Paycheck[] Checks =
    [
        new() { GrossPay = 4538.46m, NetPay = 3538.38m, PreTaxDeductions = 252.50m, PostTaxDeductions = 150m, EmployeeTaxes = 597.58m,
                FederalIncomeTax = 269.70m, SocialSecurityTax = 265.73m, MedicareTax = 62.15m, StateIncomeTax = 0m,
                EmployerSocialSecurity = 265.73m, EmployerMedicare = 62.15m, FutaTax = 0m, SutaTax = 12.25m, EmployerContributions = 310m },
        new() { GrossPay = 1612.50m, NetPay = 1179.60m, PreTaxDeductions = 236.50m, PostTaxDeductions = 0m, EmployeeTaxes = 196.40m,
                FederalIncomeTax = 81.27m, SocialSecurityTax = 85.32m, MedicareTax = 19.95m, StateIncomeTax = 9.86m,
                EmployerSocialSecurity = 85.32m, EmployerMedicare = 19.95m, FutaTax = 9.68m, SutaTax = 43.54m, EmployerContributions = 0m },
    ];

    [Fact]
    public void Entry_balances_and_costs_add_up()
    {
        var lines = GeneralLedger.Entry(Checks, new GlAccounts());
        Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
        var expectedCost = Checks.Sum(c => c.GrossPay + c.EmployerTaxes + c.EmployerContributions);
        Assert.Equal(expectedCost, lines.Sum(l => l.Debit));
        Assert.Equal(Checks.Sum(c => c.NetPay), lines.Single(l => l.Account == "Checking").Credit);
    }

    [Fact]
    public void Leaves_out_zero_lines_and_uses_custom_accounts()
    {
        var accounts = new GlAccounts { Cash = "1000 Operating", FutaPayable = "2210" };
        var lines = GeneralLedger.Entry([Checks[0]], accounts);
        Assert.DoesNotContain(lines, l => l.Account == "2210"); // no FUTA on the first check
        Assert.Contains(lines, l => l.Account == "1000 Operating" && l.Credit == 3538.38m);
    }

    [Fact]
    public void Writes_quickbooks_and_xero_layouts()
    {
        var lines = GeneralLedger.Entry(Checks, new GlAccounts());
        var qb = GeneralLedger.QuickBooksCsv(lines, "PR-7", new DateOnly(2026, 10, 29), "Payroll Oct 29, 2026").Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Journal No,Journal Date,Account Name,Debits,Credits", qb[0]);
        Assert.Contains("PR-7,10/29/2026,Payroll Expenses:Wages,6150.96,", qb[1]);

        // Read it back as real CSV: one description contains a comma and is quoted.
        var xero = EmployeeCsv.ReadRecords(GeneralLedger.XeroCsv(lines, new DateOnly(2026, 10, 29), "Payroll")).Skip(1).Select(r => r.Item2).ToList();
        Assert.Equal(lines.Count, xero.Count);
        Assert.All(xero, cells => Assert.Equal(10, cells.Count));
        Assert.Equal(0m, xero.Sum(cells => decimal.Parse(cells[5], System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Bad_saved_settings_fall_back_to_defaults() =>
        Assert.Equal("Checking", GlAccounts.From("{not json").Cash);
}
