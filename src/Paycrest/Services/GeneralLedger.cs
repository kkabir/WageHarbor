using System.Globalization;
using System.Text;
using System.Text.Json;
using Paycrest.Domain;

namespace Paycrest.Services;

/// <summary>Account names (or codes, for Xero) that a payroll journal entry posts to. Saved per company.</summary>
public sealed class GlAccounts
{
    public string Wages { get; set; } = "Payroll Expenses:Wages";
    public string EmployerTaxes { get; set; } = "Payroll Expenses:Taxes";
    public string EmployerBenefits { get; set; } = "Payroll Expenses:Benefits";
    public string Cash { get; set; } = "Checking";
    public string FederalTaxesPayable { get; set; } = "Payroll Liabilities:Federal Taxes (941)";
    public string StateTaxPayable { get; set; } = "Payroll Liabilities:State Withholding";
    public string FutaPayable { get; set; } = "Payroll Liabilities:FUTA (940)";
    public string SutaPayable { get; set; } = "Payroll Liabilities:State Unemployment";
    public string DeductionsPayable { get; set; } = "Payroll Liabilities:Deductions & Benefits";

    public static GlAccounts From(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<GlAccounts>(json) ?? new(); } catch (JsonException) { return new(); }
    }

    public string ToJson() => JsonSerializer.Serialize(this);
}

public sealed record JournalLine(string Account, decimal Debit, decimal Credit, string Memo);

/// <summary>Turns a paid payroll into a balanced journal entry, and writes it for QuickBooks Online, Xero or as plain CSV.</summary>
public static class GeneralLedger
{
    public static List<JournalLine> Entry(IReadOnlyCollection<Paycheck> checks, GlAccounts a)
    {
        decimal S(Func<Paycheck, decimal> f) => Math.Round(checks.Sum(f), 2);
        var lines = new List<JournalLine>
        {
            // Debits: what payroll cost the business.
            new(a.Wages, S(c => c.GrossPay), 0, "Gross wages"),
            new(a.EmployerTaxes, S(c => c.EmployerSocialSecurity + c.EmployerMedicare + c.FutaTax + c.SutaTax), 0, "Employer payroll taxes"),
            new(a.EmployerBenefits, S(c => c.EmployerContributions), 0, "Employer-paid benefits"),
            // Credits: where the money goes.
            new(a.Cash, 0, S(c => c.NetPay), "Net pay to employees"),
            new(a.FederalTaxesPayable, 0, S(c => c.FederalIncomeTax + c.SocialSecurityTax + c.MedicareTax + c.AdditionalMedicareTax + c.EmployerSocialSecurity + c.EmployerMedicare), "Federal income tax, Social Security and Medicare"),
            new(a.StateTaxPayable, 0, S(c => c.StateIncomeTax), "State income tax withheld"),
            new(a.FutaPayable, 0, S(c => c.FutaTax), "Federal unemployment"),
            new(a.SutaPayable, 0, S(c => c.SutaTax), "State unemployment"),
            new(a.DeductionsPayable, 0, S(c => c.PreTaxDeductions + c.PostTaxDeductions + c.EmployerContributions), "Employee deductions and employer contributions"),
        };
        lines = lines.Where(l => l.Debit != 0 || l.Credit != 0).ToList();

        // Rounding each total separately can leave a cent or two; put it on cash so the entry balances exactly.
        var diff = lines.Sum(l => l.Debit) - lines.Sum(l => l.Credit);
        if (diff != 0)
        {
            var i = lines.FindIndex(l => l.Account == a.Cash);
            if (i >= 0) lines[i] = lines[i] with { Credit = lines[i].Credit + diff };
            else lines.Add(new(a.Cash, diff < 0 ? -diff : 0, diff > 0 ? diff : 0, "Rounding"));
        }
        return lines;
    }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string Q(string s) => s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    private static string N(decimal v) => v == 0 ? "" : v.ToString("0.00", Inv);

    /// <summary>QuickBooks Online "Import journal entries" layout.</summary>
    public static string QuickBooksCsv(List<JournalLine> lines, string journalNo, DateOnly date, string memo)
    {
        var sb = new StringBuilder("Journal No,Journal Date,Account Name,Debits,Credits,Description,Name,Memo\r\n");
        foreach (var l in lines)
            sb.Append($"{Q(journalNo)},{date.ToString("MM/dd/yyyy", Inv)},{Q(l.Account)},{N(l.Debit)},{N(l.Credit)},{Q(l.Memo)},,{Q(memo)}\r\n");
        return sb.ToString();
    }

    /// <summary>Xero "Import manual journal" layout: account codes, debits positive and credits negative.</summary>
    public static string XeroCsv(List<JournalLine> lines, DateOnly date, string memo)
    {
        var sb = new StringBuilder("*Narration,*Date,Description,*AccountCode,*TaxRate,*Amount,TrackingName1,TrackingOption1,TrackingName2,TrackingOption2\r\n");
        foreach (var l in lines)
            sb.Append($"{Q(memo)},{date.ToString("dd/MM/yyyy", Inv)},{Q(l.Memo)},{Q(l.Account)},Tax Exempt,{(l.Debit - l.Credit).ToString("0.00", Inv)},,,,\r\n");
        return sb.ToString();
    }

    public static string PlainCsv(List<JournalLine> lines, DateOnly date, string memo)
    {
        var sb = new StringBuilder("Date,Account,Debit,Credit,Description,Memo\r\n");
        foreach (var l in lines)
            sb.Append($"{date.ToString("yyyy-MM-dd", Inv)},{Q(l.Account)},{N(l.Debit)},{N(l.Credit)},{Q(l.Memo)},{Q(memo)}\r\n");
        return sb.ToString();
    }
}
