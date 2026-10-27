using Paycrest.Domain;
using Paycrest.Services.Payroll;

namespace Paycrest.Tests;

public class NachaFileBuilderTests
{
    private static Company Company() => new()
    {
        Name = "Acme Widgets LLC", Ein = "12-3456789", BankName = "First Demo Bank", BankRoutingNumber = "011000015"
    };

    private static readonly NachaEntry[] Entries =
    [
        new("Jane Doe", "1001", "021000021", "123456789", BankAccountType.Checking, 1234.56m),
        new("John Smith", "1002", "011000015", "987654321", BankAccountType.Savings, 100.00m),
    ];

    private static string[] Lines(string file) => file.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Every_record_is_94_chars_and_file_is_blocked_by_10()
    {
        var lines = Lines(NachaFileBuilder.Build(Company(), new DateOnly(2026, 3, 20), Entries, new DateTime(2026, 3, 18, 9, 30, 0)));
        Assert.All(lines, l => Assert.Equal(94, l.Length));
        Assert.Equal(0, lines.Length % 10);
        Assert.Equal(new[] { '1', '5', '6', '6', '8', '9' }, lines.Take(6).Select(l => l[0]));
    }

    [Fact]
    public void Entries_use_correct_transaction_codes_and_amounts()
    {
        var lines = Lines(NachaFileBuilder.Build(Company(), new DateOnly(2026, 3, 20), Entries));
        var entries = lines.Where(l => l[0] == '6').ToList();
        Assert.Equal("22", entries[0][1..3]);           // checking credit
        Assert.Equal("02100002", entries[0][3..11]);   // RDFI routing (8)
        Assert.Equal('1', entries[0][11]);             // check digit
        Assert.Equal("0000123456", entries[0][29..39]); // amount in cents
        Assert.Equal("32", entries[1][1..3]);           // savings credit
        Assert.Equal("01100001", entries[0][79..87]);  // trace = ODFI
    }

    [Fact]
    public void Control_records_total_correctly()
    {
        var lines = Lines(NachaFileBuilder.Build(Company(), new DateOnly(2026, 3, 20), Entries));
        var batch = lines.Single(l => l[0] == '8');
        var file = lines.First(l => l[0] == '9' && !l.All(c => c == '9'));
        var hash = (2100002L + 1100001L).ToString("D10");
        Assert.Equal("000002", batch[4..10]);
        Assert.Equal(hash, batch[10..20]);
        Assert.Equal("000000133456", batch[32..44]);  // total credits
        Assert.Equal("1123456789", batch[44..54]);    // company id = 1 + EIN
        Assert.Equal("000001", file[1..7]);            // batch count
        Assert.Equal("000001", file[7..13]);           // block count
        Assert.Equal("00000002", file[13..21]);        // entry count
        Assert.Equal("000000133456", file[43..55]);
    }

    [Fact]
    public void Missing_company_bank_throws()
    {
        var c = Company(); c.BankRoutingNumber = null;
        Assert.Throws<InvalidOperationException>(() => NachaFileBuilder.Build(c, new DateOnly(2026, 3, 20), Entries));
    }

    [Theory]
    [InlineData("011000015", true)]
    [InlineData("021000021", true)]
    [InlineData("021000022", false)]
    [InlineData("12345", false)]
    public void Routing_checksum(string routing, bool valid) => Assert.Equal(valid, NachaFileBuilder.IsValidRouting(routing));
}
