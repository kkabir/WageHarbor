using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Paycrest.Domain;

namespace Paycrest.Services;

public sealed record TaxLiabilityRow(string Tax, string Paid, decimal Wages, decimal EmployeeAmount, decimal EmployerAmount)
{
    public decimal Total => EmployeeAmount + EmployerAmount;
}

public sealed record Form941Summary(int Year, int Quarter, int Employees, decimal Wages, decimal FederalIncomeTax,
    decimal SocialSecurityWages, decimal MedicareWages, decimal AdditionalMedicareWages)
{
    public decimal SocialSecurityTax => Math.Round(SocialSecurityWages * 0.124m, 2);
    public decimal MedicareTax => Math.Round(MedicareWages * 0.029m, 2);
    public decimal AdditionalMedicareTax => Math.Round(AdditionalMedicareWages * 0.009m, 2);
    public decimal TotalTaxes => FederalIncomeTax + SocialSecurityTax + MedicareTax + AdditionalMedicareTax;
}

public sealed class W2Row
{
    public int EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = "";
    public string Name { get; init; } = "";
    public string? SsnLast4 { get; init; }
    public decimal Box1Wages { get; init; }
    public decimal Box2FederalTax { get; init; }
    public decimal Box3SsWages { get; init; }
    public decimal Box4SsTax { get; init; }
    public decimal Box5MedicareWages { get; init; }
    public decimal Box6MedicareTax { get; init; }
    public string Box12 { get; init; } = "";
    public string? State { get; init; }
    public decimal Box16StateWages { get; init; }
    public decimal Box17StateTax { get; init; }
}

public sealed class ReportService(Db dbs)
{
    private async Task<List<Paycheck>> PaidChecksAsync(DateOnly from, DateOnly to, bool includeLines = false)
    {
        await using var db = await dbs.OpenAsync();
        var q = db.Paychecks.AsNoTracking().Include(p => p.Employee).ThenInclude(e => e!.Department)
            .Where(p => p.PayDate >= from && p.PayDate <= to && p.PayrollRun!.Status == PayrollRunStatus.Paid);
        if (includeLines) q = q.Include(p => p.Lines);
        return await q.OrderBy(p => p.PayDate).ThenBy(p => p.EmployeeName).ToListAsync();
    }

    public Task<List<Paycheck>> PayrollRegisterAsync(DateOnly from, DateOnly to) => PaidChecksAsync(from, to);

    public async Task<List<TaxLiabilityRow>> TaxLiabilityAsync(DateOnly from, DateOnly to)
    {
        var c = await PaidChecksAsync(from, to);
        var rows = new List<TaxLiabilityRow>
        {
            new("Federal income tax", "IRS (941)", c.Sum(x => x.FederalTaxableWages), c.Sum(x => x.FederalIncomeTax), 0),
            new("Social Security", "IRS (941)", c.Sum(x => x.SocialSecurityWages), c.Sum(x => x.SocialSecurityTax), c.Sum(x => x.EmployerSocialSecurity)),
            new("Medicare", "IRS (941)", c.Sum(x => x.MedicareWages), c.Sum(x => x.MedicareTax), c.Sum(x => x.EmployerMedicare)),
            new("Additional Medicare", "IRS (941)", 0, c.Sum(x => x.AdditionalMedicareTax), 0),
            new("Federal unemployment (FUTA)", "IRS (940)", c.Sum(x => x.FutaWages), 0, c.Sum(x => x.FutaTax)),
        };
        foreach (var g in c.Where(x => x.StateIncomeTax != 0 || x.StateTaxableWages != 0).GroupBy(x => x.StateCode ?? "??").OrderBy(g => g.Key))
            rows.Add(new($"{g.Key} state income tax", $"{g.Key} revenue dept.", g.Sum(x => x.StateTaxableWages), g.Sum(x => x.StateIncomeTax), 0));
        rows.Add(new("State unemployment (SUTA)", "State workforce agency", c.Sum(x => x.SutaWages), 0, c.Sum(x => x.SutaTax)));
        return rows.Where(r => r.Total != 0 || r.Wages != 0).ToList();
    }

    public async Task<Form941Summary> Form941Async(int year, int quarter)
    {
        var from = new DateOnly(year, (quarter - 1) * 3 + 1, 1);
        var to = from.AddMonths(3).AddDays(-1);
        var c = await PaidChecksAsync(from, to);
        // Additional Medicare wages are wages over $200k; derive from tax withheld.
        var addlWages = Math.Round(c.Sum(x => x.AdditionalMedicareTax) / 0.009m, 2);
        return new Form941Summary(year, quarter, c.Select(x => x.EmployeeId).Distinct().Count(),
            c.Sum(x => x.FederalTaxableWages), c.Sum(x => x.FederalIncomeTax), c.Sum(x => x.SocialSecurityWages),
            c.Sum(x => x.MedicareWages), addlWages);
    }

    public async Task<List<W2Row>> W2Async(int year)
    {
        var c = await PaidChecksAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), includeLines: true);
        await using var db = await dbs.OpenAsync();
        var box12Codes = await db.DeductionTypes.AsNoTracking().Where(d => d.W2Box12Code != null)
            .ToDictionaryAsync(d => d.Id, d => d.W2Box12Code!);

        return c.Where(x => x.Employee is { IsContractor: false }).GroupBy(x => x.EmployeeId).Select(g =>
        {
            var e = g.First().Employee!;
            var box12 = g.SelectMany(x => x.Lines)
                .Where(l => l.DeductionTypeId.HasValue && box12Codes.ContainsKey(l.DeductionTypeId.Value) &&
                            l.LineType is PaycheckLineType.PreTaxDeduction or PaycheckLineType.PostTaxDeduction)
                .GroupBy(l => box12Codes[l.DeductionTypeId!.Value])
                .Select(b => $"{b.Key} {b.Sum(l => l.Amount):N2}");
            return new W2Row
            {
                EmployeeId = e.Id, EmployeeNumber = e.EmployeeNumber, Name = e.LegalName, SsnLast4 = e.SsnLast4,
                Box1Wages = g.Sum(x => x.FederalTaxableWages), Box2FederalTax = g.Sum(x => x.FederalIncomeTax),
                Box3SsWages = g.Sum(x => x.SocialSecurityWages), Box4SsTax = g.Sum(x => x.SocialSecurityTax),
                Box5MedicareWages = g.Sum(x => x.MedicareWages), Box6MedicareTax = g.Sum(x => x.MedicareTax + x.AdditionalMedicareTax),
                Box12 = string.Join("; ", box12), State = e.WorkState,
                Box16StateWages = g.Sum(x => x.StateTaxableWages), Box17StateTax = g.Sum(x => x.StateIncomeTax)
            };
        }).OrderBy(r => r.Name).ToList();
    }

    public async Task<List<(string Name, decimal Gross, int Count)>> Contractors1099Async(int year)
    {
        var c = await PaidChecksAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        return c.Where(x => x.Employee is { IsContractor: true }).GroupBy(x => x.EmployeeName)
            .Select(g => (g.Key, g.Sum(x => x.GrossPay), g.Count())).OrderBy(x => x.Key).ToList();
    }

    public async Task<List<(string Label, decimal Gross, decimal EmployerCost)>> MonthlyCostAsync(int months)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var c = await PaidChecksAsync(from, today.AddYears(1));
        return Enumerable.Range(0, months).Select(i =>
        {
            var m = from.AddMonths(i);
            var inMonth = c.Where(x => x.PayDate.Year == m.Year && x.PayDate.Month == m.Month).ToList();
            return (m.ToString("MMM", CultureInfo.InvariantCulture), inMonth.Sum(x => x.GrossPay),
                inMonth.Sum(x => x.GrossPay + x.EmployerTaxes + x.EmployerContributions));
        }).ToList();
    }

    public async Task<List<(string Department, int Active, decimal AnnualPayroll)>> HeadcountAsync()
    {
        await using var db = await dbs.OpenAsync();
        var emps = await db.Employees.AsNoTracking().Include(e => e.Department).Where(e => e.Status != EmployeeStatus.Terminated).ToListAsync();
        return emps.GroupBy(e => e.Department?.Name ?? "Unassigned")
            .Select(g => (g.Key, g.Count(), g.Sum(e => e.AnnualizedPay))).OrderByDescending(x => x.Item2).ToList();
    }

    public static string ToCsv<T>(IEnumerable<T> rows, params (string Header, Func<T, object?> Value)[] columns)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(c => Escape(c.Header))));
        foreach (var r in rows)
            sb.AppendLine(string.Join(",", columns.Select(c => Escape(Convert.ToString(c.Value(r), CultureInfo.InvariantCulture)))));
        return sb.ToString();
    }

    private static string Escape(string? s)
    {
        s ??= "";
        // Neutralize spreadsheet formula injection.
        if (s.Length > 0 && "=+-@".Contains(s[0]) && !decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) s = "'" + s;
        return s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }
}
