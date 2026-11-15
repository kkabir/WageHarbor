using System.Globalization;
using System.Text;
using Paycrest.Domain;

namespace Paycrest.Services;

/// <summary>One row of an employee import, read and checked but not yet saved.</summary>
public sealed class EmployeeImportRow
{
    public int LineNumber { get; init; }
    public Employee Employee { get; } = new();
    public string? Ssn { get; set; }
    public string? DepartmentName { get; set; }
    public string? RoutingNumber { get; set; }
    public string? AccountNumber { get; set; }
    public BankAccountType AccountType { get; set; } = BankAccountType.Checking;
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public bool IsValid => Errors.Count == 0;
    public bool HasBank => !string.IsNullOrEmpty(RoutingNumber) && !string.IsNullOrEmpty(AccountNumber);
}

public sealed record EmployeeImportPreview(List<EmployeeImportRow> Rows, List<string> RecognizedColumns, List<string> IgnoredColumns);

/// <summary>
/// Reads employee spreadsheets (CSV) exported from ADP, Gusto, QuickBooks, Paychex or made by hand.
/// Columns are matched by name, so their order doesn't matter.
/// </summary>
public static class EmployeeCsv
{
    // Field → header names it accepts (compared lowercase, ignoring spaces, punctuation and "employee"/"legal" prefixes).
    private static readonly Dictionary<string, string[]> Aliases = new()
    {
        ["number"] = ["number", "id", "no", "filenumber", "associateid", "payrollid", "employeenumber", "employeeid", "empid", "empno", "empnumber"],
        ["first"] = ["firstname", "first", "givenname"],
        ["middle"] = ["middlename", "middleinitial", "middle", "mi"],
        ["last"] = ["lastname", "last", "surname", "familyname"],
        ["preferred"] = ["preferredname", "nickname", "preferredfirstname"],
        ["email"] = ["email", "emailaddress", "workemail", "personalemail", "homeemail"],
        ["phone"] = ["phone", "phonenumber", "mobile", "mobilephone", "cellphone", "homephone"],
        ["ssn"] = ["ssn", "socialsecuritynumber", "socialsecurity", "taxid", "tin"],
        ["dob"] = ["dateofbirth", "birthdate", "dob", "birthday"],
        ["address1"] = ["address", "address1", "addressline1", "street", "streetaddress", "homeaddress"],
        ["address2"] = ["address2", "addressline2", "apt", "suite", "unit"],
        ["city"] = ["city", "town"],
        ["state"] = ["state", "homestate", "residentstate", "stateprovince"],
        ["zip"] = ["zip", "zipcode", "postalcode", "postcode"],
        ["hire"] = ["hiredate", "startdate", "dateofhire", "hired", "originalhiredate"],
        ["title"] = ["jobtitle", "title", "position", "role"],
        ["department"] = ["department", "dept", "homedepartment", "departmentname"],
        ["type"] = ["employmenttype", "workertype", "type", "employeetype", "worker"],
        ["paytype"] = ["paytype", "compensationtype", "paybasis", "salaryorhourly", "flsapaytype"],
        ["rate"] = ["payrate", "rate", "compensation", "wage", "regularpayrate", "baserate"],
        // ADP and others often export both of these; whichever has a value wins.
        ["salary"] = ["annualsalary", "salary", "annualpay", "yearlysalary", "annualrate"],
        ["hourly"] = ["hourlyrate", "hourly", "hourlypay", "hourlywage"],
        ["frequency"] = ["payfrequency", "payschedule", "frequency", "payperiod"],
        ["workstate"] = ["workstate", "worklocationstate", "sitstate", "taxstate", "statewithholding"],
        ["exempt"] = ["exempt", "flsaexempt", "flsastatus", "overtimeexempt"],
        ["filing"] = ["filingstatus", "federalfilingstatus", "w4filingstatus", "maritalstatus"],
        ["routing"] = ["routingnumber", "routing", "aba", "bankrouting", "transitnumber"],
        ["account"] = ["accountnumber", "account", "bankaccount", "bankaccountnumber"],
        ["accounttype"] = ["accounttype", "bankaccounttype", "checkingsavings"],
    };

    public const string Template =
        "Employee Number,First Name,Last Name,Email,Phone,SSN,Date of Birth,Address,City,State,Zip,Hire Date,Job Title,Department,Employment Type,Pay Type,Pay Rate,Pay Frequency,Work State,Filing Status,Routing Number,Account Number,Account Type\r\n" +
        "1001,Jane,Doe,jane@example.com,(555) 010-1234,123-45-6789,1990-04-12,12 Main St,Austin,TX,78701,2024-03-01,Barista,Operations,Full-time,Hourly,18.50,Bi-weekly,TX,Single,011000015,123456789,Checking\r\n" +
        "1002,Sam,Lee,sam@example.com,,,,,,,,2025-01-06,Store Manager,Operations,Full-time,Salary,52000,Bi-weekly,TX,Married filing jointly,,,\r\n";

    public static EmployeeImportPreview Parse(string csv, string defaultState, PayFrequency defaultFrequency)
    {
        var lines = ReadRecords(csv);
        var rows = new List<EmployeeImportRow>();
        if (lines.Count == 0) return new(rows, [], []);

        var header = lines[0].Item2;
        var map = new Dictionary<string, int>();
        var ignored = new List<string>();
        for (var i = 0; i < header.Count; i++)
        {
            var key = Normalize(header[i]);
            var field = Aliases.FirstOrDefault(a => a.Value.Contains(key)).Key;
            if (field != null && !map.ContainsKey(field)) map[field] = i;
            else if (!string.IsNullOrWhiteSpace(header[i])) ignored.Add(header[i].Trim());
        }
        var recognized = map.OrderBy(m => m.Value).Select(m => header[m.Value].Trim()).ToList();

        foreach (var (line, cells) in lines.Skip(1))
        {
            if (cells.All(string.IsNullOrWhiteSpace)) continue;
            string? Get(string field) => map.TryGetValue(field, out var i) && i < cells.Count && !string.IsNullOrWhiteSpace(cells[i]) ? cells[i].Trim() : null;
            rows.Add(ReadRow(line, Get, defaultState, defaultFrequency));
        }

        // The same email or SSN twice in one file is almost always a mistake.
        foreach (var dup in rows.Where(r => r.Employee.Email != null).GroupBy(r => r.Employee.Email!.ToLowerInvariant()).Where(g => g.Count() > 1).SelectMany(g => g.Skip(1)))
            dup.Errors.Add($"Email {dup.Employee.Email} appears more than once in the file.");
        foreach (var dup in rows.Where(r => r.Ssn != null).GroupBy(r => r.Ssn).Where(g => g.Count() > 1).SelectMany(g => g.Skip(1)))
            dup.Errors.Add("This SSN appears more than once in the file.");

        // Without name columns every row fails the same way; say what's actually wrong instead.
        if (!map.ContainsKey("first") || !map.ContainsKey("last"))
            foreach (var r in rows) { r.Errors.Clear(); r.Errors.Add("The file needs First Name and Last Name columns."); }

        return new(rows, recognized, ignored);
    }

    private static EmployeeImportRow ReadRow(int line, Func<string, string?> get, string defaultState, PayFrequency defaultFrequency)
    {
        var row = new EmployeeImportRow { LineNumber = line };
        var e = row.Employee;

        e.EmployeeNumber = get("number") ?? "";
        e.FirstName = get("first") ?? "";
        e.MiddleName = get("middle");
        e.LastName = get("last") ?? "";
        e.PreferredName = get("preferred");
        if (e.FirstName.Length == 0) row.Errors.Add("First name is missing.");
        if (e.LastName.Length == 0) row.Errors.Add("Last name is missing.");

        if (get("email") is { } email)
        {
            if (System.Net.Mail.MailAddress.TryCreate(email, out _)) e.Email = email;
            else row.Warnings.Add($"Email \"{email}\" doesn't look valid and was left blank.");
        }
        e.Phone = get("phone");

        if (get("ssn") is { } ssn)
        {
            var digits = new string(ssn.Where(char.IsDigit).ToArray());
            if (digits.Length == 9) row.Ssn = digits;
            else row.Errors.Add("SSN must have 9 digits.");
        }

        e.DateOfBirth = Date(get("dob"), "Date of birth", row);
        e.AddressLine1 = get("address1");
        e.AddressLine2 = get("address2");
        e.City = get("city");
        e.State = State(get("state"), "State", row);
        e.PostalCode = get("zip");

        if (Date(get("hire"), "Hire date", row) is { } hire) e.HireDate = hire;
        e.JobTitle = get("title");
        row.DepartmentName = get("department");
        e.EmploymentType = ParseEmploymentType(get("type"), row);
        e.WorkState = State(get("workstate"), "Work state", row) ?? e.State ?? defaultState;

        var salary = Money(get("salary"), "Annual salary", row);
        var hourly = Money(get("hourly"), "Hourly rate", row);
        var rate = salary ?? hourly ?? Money(get("rate"), "Pay rate", row);
        var payType = get("paytype");
        e.PayType = payType != null
            ? (payType.Contains("sal", StringComparison.OrdinalIgnoreCase) || payType.Contains("annual", StringComparison.OrdinalIgnoreCase) ? PayType.Salary : PayType.Hourly)
            : salary != null ? PayType.Salary
            : hourly != null ? PayType.Hourly
            : rate >= 1000 ? PayType.Salary : PayType.Hourly; // only a generic rate column: a large number is an annual salary
        if (payType != null && salary != null && hourly != null)
            rate = e.PayType == PayType.Salary ? salary : hourly;
        if (rate is { } pr) e.PayRate = pr;
        else row.Warnings.Add("No pay rate; add it before running payroll.");
        if (e.PayType == PayType.Hourly && e.PayRate > 500) row.Warnings.Add($"Hourly rate {e.PayRate:C} looks high. Is this an annual salary?");

        e.PayFrequency = ParseFrequency(get("frequency"), defaultFrequency, row);
        e.DefaultHours = e.EmploymentType == EmploymentType.PartTime ? 40 : 2080m / (int)e.PayFrequency;
        if (get("exempt") is { } ex) e.IsExempt = ex.StartsWith('y') || ex.StartsWith('Y') || ex.Equals("true", StringComparison.OrdinalIgnoreCase) || ex.Equals("exempt", StringComparison.OrdinalIgnoreCase);
        else e.IsExempt = false;
        e.FilingStatus = e.StateFilingStatus = ParseFiling(get("filing"));

        row.RoutingNumber = get("routing") is { } rt ? new string(rt.Where(char.IsDigit).ToArray()) : null;
        row.AccountNumber = get("account") is { } ac ? new string(ac.Where(char.IsDigit).ToArray()) : null;
        if (get("accounttype") is { } at && at.Contains("sav", StringComparison.OrdinalIgnoreCase)) row.AccountType = BankAccountType.Savings;
        if (row.RoutingNumber != null || row.AccountNumber != null)
        {
            if (row.RoutingNumber is not { Length: 9 } || !ValidRouting(row.RoutingNumber)) { row.Warnings.Add("Routing number isn't valid; bank details were skipped."); row.RoutingNumber = row.AccountNumber = null; }
            else if (row.AccountNumber is not { Length: >= 4 and <= 17 }) { row.Warnings.Add("Account number isn't valid; bank details were skipped."); row.RoutingNumber = row.AccountNumber = null; }
        }
        e.PaymentMethod = row.HasBank ? PaymentMethod.DirectDeposit : PaymentMethod.Check;
        if (!row.HasBank) row.Warnings.Add("No bank account; paid by check until one is added.");
        return row;
    }

    private static decimal? Money(string? text, string label, EmployeeImportRow row)
    {
        if (text == null) return null;
        if (decimal.TryParse(text.Replace("$", "").Replace(",", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v >= 0) return v;
        row.Errors.Add($"{label} \"{text}\" isn't a number.");
        return null;
    }

    public static bool ValidRouting(string r) => Payroll.NachaFileBuilder.IsValidRouting(r);

    private static DateOnly? Date(string? text, string label, EmployeeImportRow row)
    {
        if (text == null) return null;
        string[] formats = ["yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy", "M/d/yy", "MM/dd/yy", "M-d-yyyy", "yyyy/M/d", "MMM d, yyyy", "d-MMM-yyyy", "d-MMM-yy"];
        var t = text.Trim();
        if (t.Contains(' ') && t.Length > 10 && DateTime.TryParse(t, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out var dt)) return DateOnly.FromDateTime(dt);
        if (DateOnly.TryParseExact(t, formats, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out var d)) return d;
        row.Errors.Add($"{label} \"{text}\" isn't a date I can read (use 2024-03-01 or 3/1/2024).");
        return null;
    }

    private static string? State(string? text, string label, EmployeeImportRow row)
    {
        if (text == null) return null;
        var t = text.Trim().ToUpperInvariant();
        if (Fmt.States.Contains(t)) return t;
        var byName = StateNames.FirstOrDefault(s => s.Value.Equals(text.Trim(), StringComparison.OrdinalIgnoreCase)).Key;
        if (byName != null) return byName;
        row.Warnings.Add($"{label} \"{text}\" isn't a US state code and was ignored.");
        return null;
    }

    private static EmploymentType ParseEmploymentType(string? text, EmployeeImportRow row)
    {
        if (text == null) return EmploymentType.FullTime;
        var t = Normalize(text);
        if (t.Contains("1099") || t.Contains("contract") || t.Contains("vendor")) return EmploymentType.Contractor1099;
        if (t.Contains("part") || t == "pt") return EmploymentType.PartTime;
        if (t.Contains("season")) return EmploymentType.Seasonal;
        if (t.Contains("temp")) return EmploymentType.Temporary;
        if (t.Contains("full") || t == "ft" || t.Contains("w2") || t.Contains("regular") || t.Contains("employee")) return EmploymentType.FullTime;
        row.Warnings.Add($"Employment type \"{text}\" not recognized; set to full-time.");
        return EmploymentType.FullTime;
    }

    private static PayFrequency ParseFrequency(string? text, PayFrequency fallback, EmployeeImportRow row)
    {
        if (text == null) return fallback;
        var t = Normalize(text);
        if (t.Contains("biweek") || t.Contains("every2") || t.Contains("everyother") || t == "26") return PayFrequency.BiWeekly;
        if (t.Contains("semimonth") || t.Contains("twiceamonth") || t == "24") return PayFrequency.SemiMonthly;
        if (t.Contains("week") || t == "52") return PayFrequency.Weekly;
        if (t.Contains("month") || t == "12") return PayFrequency.Monthly;
        row.Warnings.Add($"Pay frequency \"{text}\" not recognized; using the company default.");
        return fallback;
    }

    private static FilingStatus ParseFiling(string? text)
    {
        if (text == null) return FilingStatus.Single;
        var t = Normalize(text);
        if (t.Contains("head")) return FilingStatus.HeadOfHousehold;
        if (t.Contains("married") || t == "mfj" || t == "m") return FilingStatus.MarriedFilingJointly;
        return FilingStatus.Single;
    }

    private static string Normalize(string s)
    {
        var t = new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        // Whole-word prefixes only: stripping "emp" would turn "employment" into "loyment".
        foreach (var prefix in new[] { "employee", "legal", "worker" })
            if (t.StartsWith(prefix) && t.Length > prefix.Length) t = t[prefix.Length..];
        return t;
    }

    /// <summary>RFC 4180 CSV: quoted fields may contain commas, quotes ("") and line breaks. Returns (line number, cells).</summary>
    public static List<(int, List<string>)> ReadRecords(string text)
    {
        var records = new List<(int, List<string>)>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false, any = false;
        int line = 1, recordLine = 1;
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        // Excel in some regions saves with semicolons.
        var firstLine = text.Split('\n')[0];
        var sep = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else { if (c == '\n') line++; cell.Append(c); }
                continue;
            }
            if (c == '"') { quoted = true; any = true; }
            else if (c == sep) { cells.Add(cell.ToString()); cell.Clear(); any = true; }
            else if (c == '\r') { }
            else if (c == '\n')
            {
                cells.Add(cell.ToString()); cell.Clear();
                if (any || cells.Any(x => x.Length > 0)) records.Add((recordLine, cells));
                cells = []; any = false; line++; recordLine = line;
            }
            else { cell.Append(c); any = true; }
        }
        if (cell.Length > 0 || cells.Count > 0) { cells.Add(cell.ToString()); records.Add((recordLine, cells)); }
        return records;
    }

    private static readonly Dictionary<string, string> StateNames = new()
    {
        ["AL"] = "Alabama", ["AK"] = "Alaska", ["AZ"] = "Arizona", ["AR"] = "Arkansas", ["CA"] = "California", ["CO"] = "Colorado",
        ["CT"] = "Connecticut", ["DE"] = "Delaware", ["DC"] = "District of Columbia", ["FL"] = "Florida", ["GA"] = "Georgia",
        ["HI"] = "Hawaii", ["ID"] = "Idaho", ["IL"] = "Illinois", ["IN"] = "Indiana", ["IA"] = "Iowa", ["KS"] = "Kansas",
        ["KY"] = "Kentucky", ["LA"] = "Louisiana", ["ME"] = "Maine", ["MD"] = "Maryland", ["MA"] = "Massachusetts",
        ["MI"] = "Michigan", ["MN"] = "Minnesota", ["MS"] = "Mississippi", ["MO"] = "Missouri", ["MT"] = "Montana",
        ["NE"] = "Nebraska", ["NV"] = "Nevada", ["NH"] = "New Hampshire", ["NJ"] = "New Jersey", ["NM"] = "New Mexico",
        ["NY"] = "New York", ["NC"] = "North Carolina", ["ND"] = "North Dakota", ["OH"] = "Ohio", ["OK"] = "Oklahoma",
        ["OR"] = "Oregon", ["PA"] = "Pennsylvania", ["RI"] = "Rhode Island", ["SC"] = "South Carolina", ["SD"] = "South Dakota",
        ["TN"] = "Tennessee", ["TX"] = "Texas", ["UT"] = "Utah", ["VT"] = "Vermont", ["VA"] = "Virginia", ["WA"] = "Washington",
        ["WV"] = "West Virginia", ["WI"] = "Wisconsin", ["WY"] = "Wyoming",
    };
}
