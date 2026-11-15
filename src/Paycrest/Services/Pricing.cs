namespace Paycrest.Services;

/// <summary>
/// WageHarbor's prices: free for a company with one person on payroll, then a small base fee plus a per-person fee,
/// set at about a quarter of the published prices of popular payroll apps.
/// </summary>
public static class Pricing
{
    /// <summary>People on payroll (employees and contractors) a company can have for free.</summary>
    public const int FreePeople = 1;
    public const decimal BaseFee = 12m;
    public const decimal PerPerson = 1.50m;

    /// <summary>Published list price of comparable apps (Gusto Simple and OnPay, November 2026), for the comparison.</summary>
    public const decimal CompetitorBase = 49m;
    public const decimal CompetitorPerPerson = 6m;
    public const string CompetitorAsOf = "November 2026";

    public static decimal MonthlyPrice(int people) => people <= FreePeople ? 0m : BaseFee + PerPerson * people;
    public static decimal CompetitorPrice(int people) => people <= 0 ? 0m : CompetitorBase + CompetitorPerPerson * people;
    public static string PlanName(int people) => people <= FreePeople ? "Solo (free)" : "Team";
}
