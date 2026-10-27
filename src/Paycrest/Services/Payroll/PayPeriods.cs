using Paycrest.Domain;

namespace Paycrest.Services.Payroll;

public static class PayPeriods
{
    /// <summary>The period following one that ended on <paramref name="lastEnd"/>.</summary>
    public static (DateOnly Start, DateOnly End) Next(PayFrequency freq, DateOnly lastEnd)
    {
        var start = lastEnd.AddDays(1);
        return (start, EndFor(freq, start));
    }

    /// <summary>The period containing <paramref name="date"/>, anchored on a Sunday week start for weekly schedules.</summary>
    public static (DateOnly Start, DateOnly End) Containing(PayFrequency freq, DateOnly date, DayOfWeek weekStart = DayOfWeek.Sunday)
    {
        switch (freq)
        {
            case PayFrequency.Weekly:
            {
                var s = PayrollCalculator.WeekStart(date, weekStart);
                return (s, s.AddDays(6));
            }
            case PayFrequency.BiWeekly:
            {
                // Anchor bi-weekly periods to the first week-start of 2024.
                var anchor = PayrollCalculator.WeekStart(new DateOnly(2024, 1, 7), weekStart);
                var days = date.DayNumber - anchor.DayNumber;
                var offset = ((days % 14) + 14) % 14;
                var s = date.AddDays(-offset);
                return (s, s.AddDays(13));
            }
            case PayFrequency.SemiMonthly:
                return date.Day <= 15
                    ? (new DateOnly(date.Year, date.Month, 1), new DateOnly(date.Year, date.Month, 15))
                    : (new DateOnly(date.Year, date.Month, 16), LastDay(date));
            default:
                return (new DateOnly(date.Year, date.Month, 1), LastDay(date));
        }
    }

    public static DateOnly EndFor(PayFrequency freq, DateOnly start) => freq switch
    {
        PayFrequency.Weekly => start.AddDays(6),
        PayFrequency.BiWeekly => start.AddDays(13),
        PayFrequency.SemiMonthly => start.Day <= 15 ? new DateOnly(start.Year, start.Month, 15) : LastDay(start),
        _ => start.AddMonths(1).AddDays(-1)
    };

    /// <summary>Pay date = period end + offset, moved back to the previous business day if it falls on a weekend or holiday.</summary>
    public static DateOnly PayDate(DateOnly periodEnd, int offsetDays, IEnumerable<DateOnly>? holidays = null)
    {
        var set = holidays?.ToHashSet() ?? [];
        var d = periodEnd.AddDays(offsetDays);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || set.Contains(d)) d = d.AddDays(-1);
        return d;
    }

    private static DateOnly LastDay(DateOnly d) => new(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));
}
