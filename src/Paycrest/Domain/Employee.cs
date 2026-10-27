using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Paycrest.Domain;

public class Employee
{
    public int Id { get; set; }
    [Required, StringLength(20)] public string EmployeeNumber { get; set; } = "";

    // Personal
    [Required, StringLength(60)] public string FirstName { get; set; } = "";
    [StringLength(60)] public string? MiddleName { get; set; }
    [Required, StringLength(60)] public string LastName { get; set; } = "";
    [StringLength(60)] public string? PreferredName { get; set; }
    [StringLength(120), EmailAddress] public string? Email { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    /// <summary>SSN encrypted with ASP.NET Data Protection. Never store in clear text.</summary>
    public string? SsnProtected { get; set; }
    [StringLength(4)] public string? SsnLast4 { get; set; }

    [StringLength(120)] public string? AddressLine1 { get; set; }
    [StringLength(120)] public string? AddressLine2 { get; set; }
    [StringLength(80)] public string? City { get; set; }
    [StringLength(2)] public string? State { get; set; }
    [StringLength(10)] public string? PostalCode { get; set; }

    [StringLength(120)] public string? EmergencyContactName { get; set; }
    [StringLength(30)] public string? EmergencyContactPhone { get; set; }
    [StringLength(40)] public string? EmergencyContactRelation { get; set; }

    // Job
    public DateOnly HireDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? TerminationDate { get; set; }
    [StringLength(250)] public string? TerminationReason { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    [StringLength(100)] public string? JobTitle { get; set; }
    public int? ManagerId { get; set; }
    public Employee? Manager { get; set; }
    [Required, StringLength(2)] public string WorkState { get; set; } = "TX";

    // Pay
    public PayType PayType { get; set; } = PayType.Hourly;
    /// <summary>Hourly rate, or annual salary when PayType is Salary.</summary>
    [Range(0, 10_000_000)] public decimal PayRate { get; set; }
    public PayFrequency PayFrequency { get; set; } = PayFrequency.BiWeekly;
    /// <summary>Hours paid automatically each period when no time is entered (hourly), or shown on the stub (salary).</summary>
    [Range(0, 400)] public decimal DefaultHours { get; set; } = 80m;
    /// <summary>FLSA exempt employees are not paid overtime.</summary>
    public bool IsExempt { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.DirectDeposit;

    // Federal W-4 (2020 and later)
    public FilingStatus FilingStatus { get; set; } = FilingStatus.Single;
    /// <summary>W-4 Step 2 checkbox (multiple jobs or spouse works).</summary>
    public bool W4MultipleJobs { get; set; }
    /// <summary>W-4 Step 3 annual dependent credits.</summary>
    [Range(0, 100000)] public decimal W4DependentsAmount { get; set; }
    /// <summary>W-4 Step 4(a) annual other income.</summary>
    [Range(0, 10_000_000)] public decimal W4OtherIncome { get; set; }
    /// <summary>W-4 Step 4(b) annual deductions.</summary>
    [Range(0, 10_000_000)] public decimal W4Deductions { get; set; }
    /// <summary>W-4 Step 4(c) extra withholding per pay period.</summary>
    [Range(0, 100000)] public decimal W4ExtraWithholding { get; set; }
    public bool FederalExempt { get; set; }

    // State withholding
    public FilingStatus StateFilingStatus { get; set; } = FilingStatus.Single;
    [Range(0, 20)] public int StateAllowances { get; set; }
    [Range(0, 100000)] public decimal StateExtraWithholding { get; set; }
    public bool StateExempt { get; set; }

    /// <summary>Linked Identity user for employee self-service.</summary>
    [StringLength(450)] public string? UserId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<EmployeeBankAccount> BankAccounts { get; set; } = [];
    public List<EmployeeDeduction> Deductions { get; set; } = [];
    public List<TimeOffBalance> TimeOffBalances { get; set; } = [];
    public List<EmployeeDocument> Documents { get; set; } = [];

    [NotMapped] public string FullName => $"{PreferredName ?? FirstName} {LastName}";
    [NotMapped] public string LegalName => string.Join(" ", new[] { FirstName, MiddleName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
    [NotMapped] public string Initials => $"{FirstName.FirstOrDefault()}{LastName.FirstOrDefault()}".ToUpperInvariant();
    [NotMapped] public bool IsContractor => EmploymentType == EmploymentType.Contractor1099;
    [NotMapped] public decimal AnnualizedPay => PayType == PayType.Salary ? PayRate : PayRate * DefaultHours * (int)PayFrequency;
}

public class EmployeeBankAccount
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    [StringLength(60)] public string? BankName { get; set; }
    [Required, RegularExpression(@"^\d{9}$", ErrorMessage = "Routing number must be 9 digits")]
    public string RoutingNumber { get; set; } = "";
    public string AccountNumberProtected { get; set; } = "";
    [StringLength(4)] public string AccountLast4 { get; set; } = "";
    public BankAccountType AccountType { get; set; } = BankAccountType.Checking;
    public DepositSplitType SplitType { get; set; } = DepositSplitType.Remainder;
    /// <summary>Fixed dollar amount, or percent (0-100) of net pay.</summary>
    [Range(0, 1_000_000)] public decimal SplitValue { get; set; }
    public int Priority { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public class DeductionType
{
    public int Id { get; set; }
    [Required, StringLength(12)] public string Code { get; set; } = "";
    [Required, StringLength(60)] public string Name { get; set; } = "";
    public DeductionCategory Category { get; set; }
    public bool ReducesFederalTaxable { get; set; }
    public bool ReducesFicaTaxable { get; set; }
    public bool ReducesStateTaxable { get; set; }
    /// <summary>W-2 box 12 code, e.g. D for 401(k), W for HSA, DD for employer health cost.</summary>
    [StringLength(3)] public string? W2Box12Code { get; set; }
    /// <summary>Default annual employee limit (e.g. IRS 402(g) limit for 401(k)).</summary>
    public decimal? AnnualLimit { get; set; }
    /// <summary>Employer match as a fraction of the employee contribution (1.0 = 100%).</summary>
    [Range(0, 5)] public decimal EmployerMatchRate { get; set; }
    /// <summary>Match is limited to this fraction of gross pay (0.04 = 4%).</summary>
    [Range(0, 1)] public decimal EmployerMatchCapPercent { get; set; }
    public bool IsActive { get; set; } = true;

    [NotMapped] public bool IsPreTax => ReducesFederalTaxable || ReducesFicaTaxable || ReducesStateTaxable;
}

public class EmployeeDeduction
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int DeductionTypeId { get; set; }
    public DeductionType? DeductionType { get; set; }
    /// <summary>When true, Amount is a percent (0-100) of gross pay.</summary>
    public bool IsPercentOfGross { get; set; }
    [Range(0, 1_000_000)] public decimal Amount { get; set; }
    /// <summary>Fixed employer contribution per pay period (e.g. employer share of health premium).</summary>
    [Range(0, 1_000_000)] public decimal EmployerAmount { get; set; }
    /// <summary>Overrides the deduction type's annual limit.</summary>
    public decimal? AnnualLimit { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    [StringLength(120)] public string? Reference { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EmployeeDocument
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    [Required, StringLength(200)] public string FileName { get; set; } = "";
    [StringLength(100)] public string ContentType { get; set; } = "application/octet-stream";
    [StringLength(40)] public string Category { get; set; } = "General";
    public long SizeBytes { get; set; }
    public byte[] Content { get; set; } = [];
    public bool VisibleToEmployee { get; set; }
    [StringLength(256)] public string? UploadedBy { get; set; }
    public DateTime UploadedUtc { get; set; } = DateTime.UtcNow;
}
