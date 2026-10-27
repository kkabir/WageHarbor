using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Paycrest.Domain;

public class EarningType
{
    public int Id { get; set; }
    [Required, StringLength(12)] public string Code { get; set; } = "";
    [Required, StringLength(60)] public string Name { get; set; } = "";
    /// <summary>Rate multiplier applied to the hourly rate (1.5 for overtime).</summary>
    [Range(0, 5)] public decimal Multiplier { get; set; } = 1m;
    public bool IsHoursBased { get; set; } = true;
    public bool IsTaxable { get; set; } = true;
    /// <summary>Supplemental wages (bonus, commission) withheld at the federal flat supplemental rate.</summary>
    public bool IsSupplemental { get; set; }
    /// <summary>Built-in types used by the payroll engine; code cannot change.</summary>
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;

    public const string Regular = "REG";
    public const string Overtime = "OT";
}

public class PayrollRun
{
    public int Id { get; set; }
    public PayrollRunType RunType { get; set; } = PayrollRunType.Regular;
    public PayFrequency PayFrequency { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly PayDate { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    /// <summary>Off-cycle runs can skip recurring benefit deductions.</summary>
    public bool ApplyDeductions { get; set; } = true;
    [StringLength(500)] public string? Notes { get; set; }

    public int EmployeeCount { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalEmployeeTaxes { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalEmployerTaxes { get; set; }
    public decimal TotalEmployerContributions { get; set; }
    public string? Warnings { get; set; }

    [StringLength(256)] public string? CreatedBy { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CalculatedUtc { get; set; }
    [StringLength(256)] public string? ApprovedBy { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    public DateTime? PaidUtc { get; set; }

    public List<Paycheck> Paychecks { get; set; } = [];
    public List<PayrollAdjustment> Adjustments { get; set; } = [];

    [NotMapped] public decimal TotalCost => TotalGross + TotalEmployerTaxes + TotalEmployerContributions;
    [NotMapped] public string PeriodLabel => $"{PeriodStart:MMM d} – {PeriodEnd:MMM d, yyyy}";
    [NotMapped] public bool IsEditable => Status == PayrollRunStatus.Draft;
}

/// <summary>One-time earning (bonus, commission, reimbursement, extra hours) for an employee in a run.</summary>
public class PayrollAdjustment
{
    public int Id { get; set; }
    public int PayrollRunId { get; set; }
    public PayrollRun? PayrollRun { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int EarningTypeId { get; set; }
    public EarningType? EarningType { get; set; }
    public decimal? Hours { get; set; }
    public decimal Amount { get; set; }
    [StringLength(200)] public string? Note { get; set; }
}

public class Paycheck
{
    public int Id { get; set; }
    public int PayrollRunId { get; set; }
    public PayrollRun? PayrollRun { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [StringLength(130)] public string EmployeeName { get; set; } = "";
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly PayDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public int? CheckNumber { get; set; }

    public decimal TotalHours { get; set; }
    public decimal GrossPay { get; set; }
    public decimal PreTaxDeductions { get; set; }
    public decimal PostTaxDeductions { get; set; }
    public decimal EmployeeTaxes { get; set; }
    public decimal NetPay { get; set; }

    public decimal FederalTaxableWages { get; set; }
    public decimal SocialSecurityWages { get; set; }
    public decimal MedicareWages { get; set; }
    public decimal StateTaxableWages { get; set; }
    public decimal FutaWages { get; set; }
    public decimal SutaWages { get; set; }

    public decimal FederalIncomeTax { get; set; }
    public decimal SocialSecurityTax { get; set; }
    public decimal MedicareTax { get; set; }
    public decimal AdditionalMedicareTax { get; set; }
    [StringLength(2)] public string? StateCode { get; set; }
    public decimal StateIncomeTax { get; set; }

    public decimal EmployerSocialSecurity { get; set; }
    public decimal EmployerMedicare { get; set; }
    public decimal FutaTax { get; set; }
    public decimal SutaTax { get; set; }
    public decimal EmployerContributions { get; set; }

    public string? Warnings { get; set; }

    public List<PaycheckLine> Lines { get; set; } = [];
    public List<PaycheckDeposit> Deposits { get; set; } = [];

    [NotMapped] public decimal EmployerTaxes => EmployerSocialSecurity + EmployerMedicare + FutaTax + SutaTax;
}

public class PaycheckLine
{
    public int Id { get; set; }
    public int PaycheckId { get; set; }
    public PaycheckLineType LineType { get; set; }
    [StringLength(12)] public string Code { get; set; } = "";
    [StringLength(80)] public string Description { get; set; } = "";
    public decimal? Hours { get; set; }
    public decimal? Rate { get; set; }
    public decimal Amount { get; set; }
    public int? EarningTypeId { get; set; }
    public int? DeductionTypeId { get; set; }
}

/// <summary>Snapshot of where net pay was deposited, used for ACH file generation.</summary>
public class PaycheckDeposit
{
    public int Id { get; set; }
    public int PaycheckId { get; set; }
    [StringLength(60)] public string? BankName { get; set; }
    [StringLength(9)] public string RoutingNumber { get; set; } = "";
    public string AccountNumberProtected { get; set; } = "";
    [StringLength(4)] public string AccountLast4 { get; set; } = "";
    public BankAccountType AccountType { get; set; }
    public decimal Amount { get; set; }
}
