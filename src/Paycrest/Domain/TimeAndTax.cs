using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Paycrest.Domain;

public class TimeEntry
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly WorkDate { get; set; }
    public int EarningTypeId { get; set; }
    public EarningType? EarningType { get; set; }
    [Range(0, 24)] public decimal Hours { get; set; }
    public DateTime? ClockInUtc { get; set; }
    public DateTime? ClockOutUtc { get; set; }
    [StringLength(250)] public string? Notes { get; set; }
    [StringLength(20)] public string Source { get; set; } = "Manual";
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    [StringLength(256)] public string? ApprovedBy { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    /// <summary>Set when the entry has been paid; paid entries are locked.</summary>
    public int? PayrollRunId { get; set; }
    public int? TimeOffRequestId { get; set; }

    [NotMapped] public bool IsOpenPunch => ClockInUtc.HasValue && !ClockOutUtc.HasValue;
    [NotMapped] public bool IsLocked => PayrollRunId.HasValue;
}

public class TimeOffPolicy
{
    public int Id { get; set; }
    [Required, StringLength(60)] public string Name { get; set; } = "";
    /// <summary>Earning type used when time off is paid (e.g. PTO, SICK).</summary>
    public int EarningTypeId { get; set; }
    public EarningType? EarningType { get; set; }
    public AccrualMethod AccrualMethod { get; set; } = AccrualMethod.PerPayPeriod;
    /// <summary>Hours per pay period, hours per hour worked, or hours granted each January.</summary>
    [Range(0, 1000)] public decimal AccrualRate { get; set; }
    public decimal? MaxBalance { get; set; }
    public bool AllowNegativeBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TimeOffBalance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int TimeOffPolicyId { get; set; }
    public TimeOffPolicy? TimeOffPolicy { get; set; }
    public decimal Balance { get; set; }
    public decimal AccruedYtd { get; set; }
    public decimal UsedYtd { get; set; }
}

public class TimeOffRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int TimeOffPolicyId { get; set; }
    public TimeOffPolicy? TimeOffPolicy { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    [Range(0.5, 2000)] public decimal Hours { get; set; }
    [StringLength(500)] public string? Reason { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public DateTime RequestedUtc { get; set; } = DateTime.UtcNow;
    [StringLength(256)] public string? ReviewedBy { get; set; }
    public DateTime? ReviewedUtc { get; set; }
    [StringLength(500)] public string? ReviewNote { get; set; }
}

/// <summary>Time-off ledger: accruals (+), usage (-) and manual adjustments.</summary>
public class TimeOffTransaction
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int TimeOffPolicyId { get; set; }
    public TimeOffPolicy? TimeOffPolicy { get; set; }
    public DateOnly Date { get; set; }
    public decimal Hours { get; set; }
    [StringLength(20)] public string Kind { get; set; } = "Adjustment";
    public int? PayrollRunId { get; set; }
    public int? TimeOffRequestId { get; set; }
    [StringLength(250)] public string? Note { get; set; }
    [StringLength(256)] public string? CreatedBy { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Federal payroll tax constants for one calendar year. Verify against IRS Publication 15 / 15-T every January.</summary>
public class TaxYear
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Year { get; set; }
    public decimal SocialSecurityRate { get; set; } = 0.062m;
    public decimal SocialSecurityWageBase { get; set; }
    public decimal MedicareRate { get; set; } = 0.0145m;
    public decimal AdditionalMedicareRate { get; set; } = 0.009m;
    public decimal AdditionalMedicareThreshold { get; set; } = 200000m;
    public decimal FutaRate { get; set; } = 0.006m;
    public decimal FutaWageBase { get; set; } = 7000m;
    public decimal SupplementalRate { get; set; } = 0.22m;
    public decimal StandardDeductionSingle { get; set; }
    public decimal StandardDeductionMarried { get; set; }
    public decimal StandardDeductionHeadOfHousehold { get; set; }
    [StringLength(500)] public string? Notes { get; set; }

    public List<FederalTaxBracket> Brackets { get; set; } = [];

    public decimal StandardDeduction(FilingStatus status) => status switch
    {
        FilingStatus.MarriedFilingJointly => StandardDeductionMarried,
        FilingStatus.HeadOfHousehold => StandardDeductionHeadOfHousehold,
        _ => StandardDeductionSingle
    };
}

public class FederalTaxBracket
{
    public int Id { get; set; }
    public int Year { get; set; }
    public FilingStatus FilingStatus { get; set; }
    /// <summary>Annual taxable income at which this rate begins.</summary>
    public decimal Threshold { get; set; }
    public decimal Rate { get; set; }
}

public class StateTaxRule
{
    public int Id { get; set; }
    [Required, StringLength(2)] public string StateCode { get; set; } = "";
    public int Year { get; set; }
    public StateTaxMethod Method { get; set; }
    public decimal FlatRate { get; set; }
    /// <summary>Annual standard deduction subtracted before applying the rate.</summary>
    public decimal StandardDeduction { get; set; }
    /// <summary>Annual amount per state allowance claimed.</summary>
    public decimal AllowanceAmount { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
    public List<StateTaxBracket> Brackets { get; set; } = [];
}

public class StateTaxBracket
{
    public int Id { get; set; }
    public int StateTaxRuleId { get; set; }
    public FilingStatus FilingStatus { get; set; }
    public decimal Threshold { get; set; }
    public decimal Rate { get; set; }
}
