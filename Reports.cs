namespace Code;

public sealed record Offer(int CarId, string CarName, decimal DailyPrice, int Days,
    int ServiceFee, decimal TotalPrice);
public sealed record FleetTotal(CarType Type, int Cars, decimal DailyPotential);
public sealed record ReportBundle(IReadOnlyList<Offer> Standard, IReadOnlyList<Offer> Discounted,
    IReadOnlyList<FleetTotal> Fleet);

public static class RentalExtensions
{
    // A reusable price constraint on a sequence of customer offers.
    public static IEnumerable<Offer> WithinBudget(this IEnumerable<Offer> offers, decimal budget) =>
        offers.Where(offer => offer.TotalPrice <= budget);

    // A reusable business projection on the fleet; no shared state changes.
    public static IEnumerable<FleetTotal> SummarizeFleet(this IEnumerable<Car> cars, Func<Car, decimal> price) =>
        cars.GroupBy(car => car.Type)
            .Select(group => new FleetTotal(group.Key, group.Count(), group.Sum(price)));
}

public static class ReportQueries
{
    // Curried: discount -> car -> price. Only a decimal is captured by the closure.
    public static Func<Car, decimal> CreatePrice(decimal discount)
    {
        if (discount < 0m || discount > 1m) throw new ArgumentOutOfRangeException(nameof(discount));
        return car => decimal.Round(PricingPolicies.Standard(car) * (1m - discount),
            2, MidpointRounding.AwayFromZero);
    }

    public static IEnumerable<Offer> CreateOffers(IEnumerable<Car> cars, int days,
        int serviceFee, Func<Car, decimal> price)
    {
        foreach (Car car in cars)
        {
            decimal daily = price(car);
            yield return new Offer(car.Id, car.Name, daily, days, serviceFee, daily * days + serviceFee);
        }
    }

    public static Offer[] AffordableOffers(IEnumerable<Offer> offers, decimal budget) =>
        offers.WithinBudget(budget)
            .OrderBy(offer => offer.TotalPrice)
            .ThenBy(offer => offer.CarName, StringComparer.Ordinal)
            .ThenBy(offer => offer.CarId).ToArray();

    public static FleetTotal[] FleetReport(IEnumerable<Car> cars, Func<Car, decimal> price) =>
        cars.SummarizeFleet(price).OrderByDescending(row => row.DailyPotential)
            .ThenBy(row => row.Type).ToArray();
}

public static class ReportWorkflow
{
    public static Result<ReportBundle, RentalError> Run(RentalData data, string customerId,
        string start, string end, decimal budget, Func<Car, decimal> standard,
        Func<Car, decimal> discounted, NativeFee native)
    {
        if (budget < 0m) return Result<ReportBundle, RentalError>.Fail(new("BUDGET_INVALID", "Budget cannot be negative."));
        return RentalWorkflow.SearchQuotes(data, customerId, start, end).Bind(comparison =>
        {
            int days = comparison.Standard.EndDate.DayNumber - comparison.Standard.StartDate.DayNumber;
            return native.Calculate(days).Map(fee =>
            {
                var ids = comparison.Standard.Quotes.Select(q => q.CarId).ToHashSet();
                // Bounded snapshot reused by both configurations; no I/O in lazy calculations.
                Car[] available = data.Cars.Where(car => ids.Contains(car.Id)).ToArray();
                Offer[] first = ReportQueries.CreateOffers(available, days, fee, standard).ToArray();
                Offer[] second = ReportQueries.CreateOffers(available, days, fee, discounted).ToArray();
                return new ReportBundle(ReportQueries.AffordableOffers(first, budget),
                    ReportQueries.AffordableOffers(second, budget), ReportQueries.FleetReport(data.Cars, standard));
            });
        });
    }
}
