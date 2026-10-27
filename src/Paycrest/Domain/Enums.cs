namespace Paycrest.Domain;

/// <summary>Value is the number of pay periods per year.</summary>
public enum PayFrequency { Weekly = 52, BiWeekly = 26, SemiMonthly = 24, Monthly = 12 }

public enum PayType { Hourly, Salary }

public enum EmploymentType { FullTime, PartTime, Seasonal, Temporary, Contractor1099 }

public enum EmployeeStatus { Active, OnLeave, Terminated }

public enum FilingStatus { Single, MarriedFilingJointly, HeadOfHousehold }

public enum BankAccountType { Checking, Savings }

public enum DepositSplitType { Remainder, FixedAmount, Percent }

public enum DeductionCategory { Retirement401k, Roth401k, HealthInsurance, DentalVision, Hsa, Fsa, LifeInsurance, Garnishment, Loan, Other }

public enum PayrollRunType { Regular, OffCycle }

public enum PayrollRunStatus { Draft, Approved, Paid, Voided }

public enum PaymentMethod { DirectDeposit, Check }

public enum PaycheckLineType { Earning, EmployeeTax, PreTaxDeduction, PostTaxDeduction, EmployerTax, EmployerContribution }

public enum ApprovalStatus { Pending, Approved, Rejected, Cancelled }

public enum AccrualMethod { None, PerPayPeriod, PerHourWorked, AnnualGrant }

public enum StateTaxMethod { NotConfigured, NoIncomeTax, FlatRate, Brackets }

public static class EnumText
{
    public static string Label(this PayFrequency f) => f switch
    {
        PayFrequency.BiWeekly => "Bi-weekly",
        PayFrequency.SemiMonthly => "Semi-monthly",
        _ => f.ToString()
    };

    public static string Label(this FilingStatus s) => s switch
    {
        FilingStatus.MarriedFilingJointly => "Married filing jointly",
        FilingStatus.HeadOfHousehold => "Head of household",
        _ => "Single or married filing separately"
    };

    public static string Label(this EmploymentType t) => t switch
    {
        EmploymentType.FullTime => "Full-time",
        EmploymentType.PartTime => "Part-time",
        EmploymentType.Contractor1099 => "Contractor (1099)",
        _ => t.ToString()
    };

    public static string Label(this DeductionCategory c) => c switch
    {
        DeductionCategory.Retirement401k => "401(k) pre-tax",
        DeductionCategory.Roth401k => "Roth 401(k)",
        DeductionCategory.HealthInsurance => "Health insurance",
        DeductionCategory.DentalVision => "Dental / vision",
        DeductionCategory.Hsa => "HSA",
        DeductionCategory.Fsa => "FSA",
        DeductionCategory.LifeInsurance => "Life insurance",
        _ => c.ToString()
    };
}
