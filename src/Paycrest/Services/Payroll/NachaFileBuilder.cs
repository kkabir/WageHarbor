using System.Text;
using Paycrest.Domain;

namespace Paycrest.Services.Payroll;

public sealed record NachaEntry(string Name, string IndividualId, string RoutingNumber, string AccountNumber, BankAccountType AccountType, decimal Amount);

/// <summary>
/// Builds a NACHA (ACH) PPD credit file for direct deposit. Upload the file to your bank's ACH origination portal.
/// Every record is exactly 94 characters; the file is padded to a multiple of 10 records.
/// </summary>
public static class NachaFileBuilder
{
    public static string Build(Company company, DateOnly effectiveDate, IReadOnlyList<NachaEntry> entries, DateTime? createdAt = null)
    {
        var odfi = Digits(company.BankRoutingNumber);
        if (odfi.Length != 9) throw new InvalidOperationException("Company bank routing number (9 digits) is required for ACH. Set it in Settings › Company.");
        var companyId = Digits(company.AchCompanyId);
        if (string.IsNullOrEmpty(companyId))
            companyId = "1" + Digits(company.Ein);
        if (companyId.Length < 2) throw new InvalidOperationException("ACH Company ID or EIN is required. Set it in Settings › Company.");
        var origin = string.IsNullOrWhiteSpace(company.AchImmediateOrigin) ? companyId : Digits(company.AchImmediateOrigin);
        var now = createdAt ?? DateTime.Now;
        var name = Alpha(company.Name);
        var odfi8 = odfi[..8];

        var lines = new List<string>
        {
            // 1 - File header
            "1" + "01" + " " + odfi + Right(origin, 10) + now.ToString("yyMMdd") + now.ToString("HHmm") + "A" + "094" + "10" + "1"
                + Left(Alpha(company.BankName ?? "BANK"), 23) + Left(name, 23) + Left("PAYROLL", 8),
            // 5 - Batch header, service class 220 = credits only
            "5" + "220" + Left(name, 16) + Left("", 20) + Left(companyId, 10) + "PPD" + Left("PAYROLL", 10)
                + effectiveDate.ToString("MMMdd").ToUpperInvariant()[..5].PadRight(6) + effectiveDate.ToString("yyMMdd") + "   " + "1" + odfi8 + "0000001"
        };

        long hash = 0;
        decimal credit = 0;
        var seq = 0;
        foreach (var e in entries.Where(e => e.Amount > 0))
        {
            var rdfi = Digits(e.RoutingNumber);
            if (rdfi.Length != 9) throw new InvalidOperationException($"Invalid routing number for {e.Name}.");
            seq++;
            hash += long.Parse(rdfi[..8]);
            credit += e.Amount;
            var tran = e.AccountType == BankAccountType.Savings ? "32" : "22";
            lines.Add("6" + tran + rdfi[..8] + rdfi[8] + Left(Digits(e.AccountNumber), 17) + Cents(e.Amount, 10)
                      + Left(Alpha(e.IndividualId), 15) + Left(Alpha(e.Name), 22) + "  " + "0" + odfi8 + seq.ToString("D7"));
        }

        var hashStr = (hash % 10_000_000_000L).ToString("D10");
        // 8 - Batch control
        lines.Add("8" + "220" + seq.ToString("D6") + hashStr + Cents(0, 12) + Cents(credit, 12) + Left(companyId, 10)
                  + new string(' ', 19) + new string(' ', 6) + odfi8 + "0000001");

        var recordCount = lines.Count + 1;
        var blocks = (int)Math.Ceiling(recordCount / 10.0);
        // 9 - File control
        lines.Add("9" + "000001" + blocks.ToString("D6") + seq.ToString("D8") + hashStr + Cents(0, 12) + Cents(credit, 12) + new string(' ', 39));

        while (lines.Count % 10 != 0) lines.Add(new string('9', 94));

        foreach (var l in lines)
            if (l.Length != 94) throw new InvalidOperationException($"NACHA record has length {l.Length}: '{l}'");

        return string.Join("\r\n", lines) + "\r\n";
    }

    /// <summary>ABA routing number checksum.</summary>
    public static bool IsValidRouting(string? routing)
    {
        var d = Digits(routing);
        if (d.Length != 9) return false;
        var sum = 3 * (d[0] + d[3] + d[6] - 3 * '0') + 7 * (d[1] + d[4] + d[7] - 3 * '0') + (d[2] + d[5] + d[8] - 3 * '0');
        return sum % 10 == 0;
    }

    private static string Digits(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());
    private static string Alpha(string s) => new(s.ToUpperInvariant().Select(c => c < 128 && !char.IsControl(c) ? c : ' ').ToArray());
    private static string Left(string s, int len) => s.Length >= len ? s[..len] : s.PadRight(len);
    private static string Right(string s, int len) => s.Length >= len ? s[^len..] : s.PadLeft(len);
    private static string Cents(decimal amount, int len) => ((long)Math.Round(amount * 100, MidpointRounding.AwayFromZero)).ToString().PadLeft(len, '0');
}
