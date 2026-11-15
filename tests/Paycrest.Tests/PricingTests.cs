using Paycrest.Services;

namespace Paycrest.Tests;

public class PricingTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 15)]
    [InlineData(10, 27)]
    [InlineData(25, 49.5)]
    public void Monthly_price_is_free_for_one_person_then_base_plus_per_person(int people, double expected) =>
        Assert.Equal((decimal)expected, Pricing.MonthlyPrice(people));

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(200)]
    public void Paid_plans_cost_at_most_about_a_quarter_of_comparable_apps(int people) =>
        Assert.InRange(Pricing.MonthlyPrice(people) / Pricing.CompetitorPrice(people), 0.20m, 0.26m);
}
