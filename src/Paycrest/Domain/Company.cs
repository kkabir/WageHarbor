using System.ComponentModel.DataAnnotations;

namespace Paycrest.Domain;

public class Company
{
    public int Id { get; set; }

    [Required, StringLength(120)] public string Name { get; set; } = "";
    [StringLength(160)] public string? LegalName { get; set; }
    [StringLength(10), RegularExpression(@"^\d{2}-?\d{7}$", ErrorMessage = "EIN format is 12-3456789")]
    public string? Ein { get; set; }
    [StringLength(40)] public string? StateTaxId { get; set; }
    [StringLength(40)] public string? StateUnemploymentId { get; set; }

    [StringLength(120)] public string? AddressLine1 { get; set; }
    [StringLength(120)] public string? AddressLine2 { get; set; }
    [StringLength(80)] public string? City { get; set; }
    [StringLength(2)] public string? State { get; set; }
    [StringLength(10)] public string? PostalCode { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(120), EmailAddress] public string? Email { get; set; }

    [StringLength(64)] public string TimeZoneId { get; set; } = "America/New_York";

    // Payroll defaults
    public PayFrequency DefaultPayFrequency { get; set; } = PayFrequency.BiWeekly;
    /// <summary>Days after period end that paychecks are dated.</summary>
    [Range(0, 30)] public int PayDateOffsetDays { get; set; } = 5;
    public DayOfWeek WorkWeekStart { get; set; } = DayOfWeek.Sunday;
    [Range(1, 80)] public decimal OvertimeThresholdHours { get; set; } = 40m;
    /// <summary>State unemployment (SUTA) rate as a fraction, e.g. 0.027 = 2.7%.</summary>
    [Range(0, 0.2)] public decimal SutaRate { get; set; } = 0.027m;
    [Range(0, 200000)] public decimal SutaWageBase { get; set; } = 7000m;
    public int NextCheckNumber { get; set; } = 1001;

    // ACH / direct deposit origination
    [StringLength(60)] public string? BankName { get; set; }
    [StringLength(9), RegularExpression(@"^\d{9}$", ErrorMessage = "Routing number must be 9 digits")]
    public string? BankRoutingNumber { get; set; }
    public string? BankAccountNumberProtected { get; set; }
    [StringLength(4)] public string? BankAccountLast4 { get; set; }
    /// <summary>ACH Company ID assigned by your bank (often "1" + EIN).</summary>
    [StringLength(10)] public string? AchCompanyId { get; set; }
    /// <summary>Immediate origin assigned by your bank. Defaults to the company ID.</summary>
    [StringLength(10)] public string? AchImmediateOrigin { get; set; }

    public bool SetupCompleted { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public string AddressSingleLine =>
        string.Join(", ", new[] { AddressLine1, AddressLine2, City, $"{State} {PostalCode}".Trim() }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public class Department
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = "";
    [StringLength(20)] public string? Code { get; set; }
    [StringLength(250)] public string? Description { get; set; }
    public List<Employee> Employees { get; set; } = [];
}

public class Holiday
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = "";
    public bool IsPaid { get; set; } = true;
}

public class Announcement
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string Title { get; set; } = "";
    [Required, StringLength(4000)] public string Body { get; set; } = "";
    public bool IsPinned { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    [StringLength(256)] public string? PostedBy { get; set; }
    public DateTime PostedUtc { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    [StringLength(256)] public string? UserName { get; set; }
    [StringLength(40)] public string Action { get; set; } = "";
    [StringLength(80)] public string EntityType { get; set; } = "";
    [StringLength(40)] public string? EntityId { get; set; }
    public string? Details { get; set; }
}
