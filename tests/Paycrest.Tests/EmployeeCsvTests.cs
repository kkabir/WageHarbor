using Paycrest.Domain;
using Paycrest.Services;

namespace Paycrest.Tests;

public class EmployeeCsvTests
{
    private static EmployeeImportPreview Parse(string csv) => EmployeeCsv.Parse(csv, "TX", PayFrequency.BiWeekly);

    [Fact]
    public void Template_imports_cleanly()
    {
        var p = Parse(EmployeeCsv.Template);
        Assert.Equal(2, p.Rows.Count);
        Assert.All(p.Rows, r => Assert.True(r.IsValid, string.Join("; ", r.Errors)));
        Assert.Empty(p.IgnoredColumns);

        var jane = p.Rows[0];
        Assert.Equal("Jane", jane.Employee.FirstName);
        Assert.Equal("123456789", jane.Ssn);
        Assert.Equal(new DateOnly(2024, 3, 1), jane.Employee.HireDate);
        Assert.Equal(PayType.Hourly, jane.Employee.PayType);
        Assert.Equal(18.50m, jane.Employee.PayRate);
        Assert.True(jane.HasBank);
        Assert.Equal(PaymentMethod.DirectDeposit, jane.Employee.PaymentMethod);

        var sam = p.Rows[1];
        Assert.Equal(PayType.Salary, sam.Employee.PayType);
        Assert.Equal(FilingStatus.MarriedFilingJointly, sam.Employee.FilingStatus);
        Assert.Equal(PaymentMethod.Check, sam.Employee.PaymentMethod);
    }

    [Fact]
    public void Recognizes_adp_and_gusto_style_headers_in_any_order()
    {
        const string csv = "Legal Last Name,Legal First Name,Associate ID,Annual Salary,Start Date,Worker Type,Zip Code,Home State,Personal Email\n" +
                           "Patel,Jordan,A100,\"82,000\",10/9/2022,Full Time,98101,Washington,jordan@example.com\n";
        var r = Assert.Single(Parse(csv).Rows);
        Assert.True(r.IsValid, string.Join("; ", r.Errors));
        Assert.Equal("Jordan", r.Employee.FirstName);
        Assert.Equal("Patel", r.Employee.LastName);
        Assert.Equal("A100", r.Employee.EmployeeNumber);
        Assert.Equal(82000m, r.Employee.PayRate);
        Assert.Equal(PayType.Salary, r.Employee.PayType);
        Assert.Equal(new DateOnly(2022, 10, 9), r.Employee.HireDate);
        Assert.Equal("WA", r.Employee.State);
        Assert.Equal("WA", r.Employee.WorkState);
    }

    [Fact]
    public void Reads_semicolon_files_and_quoted_line_breaks()
    {
        const string csv = "First Name;Last Name;Address\r\nAisha;Okafor;\"Unit 4\r\n12 Pier Rd\"\r\n";
        var r = Assert.Single(Parse(csv).Rows);
        Assert.Equal("Okafor", r.Employee.LastName);
        Assert.Contains("12 Pier Rd", r.Employee.AddressLine1);
    }

    [Fact]
    public void Flags_bad_rows_without_stopping_the_rest()
    {
        const string csv = "First Name,Last Name,SSN,Hire Date,Email\n" +
                           ",NoFirst,,,\n" +
                           "Bad,Ssn,12-34,,\n" +
                           "Bad,Date,,31/31/2024,\n" +
                           "Good,Row,,,a@example.com\n" +
                           "Dup,Email,,,a@example.com\n";
        var rows = Parse(csv).Rows;
        Assert.Equal(5, rows.Count);
        Assert.Contains(rows[0].Errors, e => e.Contains("First name"));
        Assert.Contains(rows[1].Errors, e => e.Contains("SSN"));
        Assert.Contains(rows[2].Errors, e => e.Contains("Hire date"));
        Assert.True(rows[3].IsValid);
        Assert.Contains(rows[4].Errors, e => e.Contains("more than once"));
        Assert.Equal(5, rows[4].LineNumber + 0 - 1); // header is line 1
    }

    [Fact]
    public void Skips_invalid_bank_details_with_a_warning()
    {
        const string csv = "First Name,Last Name,Routing Number,Account Number\nA,B,123456789,99887766\n";
        var r = Assert.Single(Parse(csv).Rows);
        Assert.True(r.IsValid);
        Assert.False(r.HasBank);
        Assert.Contains(r.Warnings, w => w.Contains("Routing"));
    }

    [Theory]
    [InlineData("011000015", true)]
    [InlineData("021000021", true)]
    [InlineData("123456789", false)]
    [InlineData("01100001", false)]
    public void Checks_routing_number_checksum(string routing, bool ok) => Assert.Equal(ok, EmployeeCsv.ValidRouting(routing));

    [Fact]
    public void Requires_name_columns() =>
        Assert.All(Parse("Email,Phone\na@b.com,1\n").Rows, r => Assert.Contains(r.Errors, e => e.Contains("First Name and Last Name")));
}

public class EmployeeCsvRateTests
{
    [Fact]
    public void Uses_whichever_of_salary_and_hourly_columns_has_a_value()
    {
        const string csv = "First Name,Last Name,Annual Salary,Hourly Rate\nA,Salaried,\"64,000\",\nB,Hourly,,21.75\n";
        var rows = EmployeeCsv.Parse(csv, "TX", Paycrest.Domain.PayFrequency.BiWeekly).Rows;
        Assert.Equal((Paycrest.Domain.PayType.Salary, 64000m), (rows[0].Employee.PayType, rows[0].Employee.PayRate));
        Assert.Equal((Paycrest.Domain.PayType.Hourly, 21.75m), (rows[1].Employee.PayType, rows[1].Employee.PayRate));
        Assert.All(rows, r => Assert.DoesNotContain(r.Warnings, w => w.Contains("No pay rate")));
    }
}
