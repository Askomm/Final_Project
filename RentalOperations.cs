using System.Globalization;

namespace Code;

// Private construction means an invalid period cannot enter the new workflows.
public sealed class BookingPeriod
{
    public DateOnly Start { get; }
    public DateOnly End { get; }
    public int Days => End.DayNumber - Start.DayNumber;
    private BookingPeriod(DateOnly start, DateOnly end) { Start = start; End = end; }

    public static Result<BookingPeriod, RentalError> Create(DateOnly start, DateOnly end) =>
        end <= start || end.DayNumber - start.DayNumber > 366
            ? Result<BookingPeriod, RentalError>.Fail(new("PERIOD_INVALID",
                "End date must be after start date; a rental may last 1 to 366 days."))
            : Result<BookingPeriod, RentalError>.Ok(new(start, end));
}

public static class ConsoleInput
{
    public static Result<int, RentalError> PositiveId(string? text, string label) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id > 0
            ? Result<int, RentalError>.Ok(id)
            : Result<int, RentalError>.Fail(new("ID_INVALID", label + " must be a positive whole number."));

    public static Result<DateOnly, RentalError> Date(string? text, string label) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? Result<DateOnly, RentalError>.Ok(date)
            : Result<DateOnly, RentalError>.Fail(new("DATE_INVALID",
                label + " must be a real date in yyyy-MM-dd format (for example 2026-12-01)."));

    public static Result<string, RentalError> Payment(string? text) =>
        text?.Trim().ToLowerInvariant() switch
        {
            "cash" => Result<string, RentalError>.Ok("Cash"),
            "card" => Result<string, RentalError>.Ok("Card"),
            "transfer" => Result<string, RentalError>.Ok("Transfer"),
            _ => Result<string, RentalError>.Fail(new("PAYMENT_INVALID",
                "Payment method must be Cash, Card, or Transfer."))
        };
}

public sealed record RentalChange(RentalData Data, Rental Rental);

public static class RentalOperations
{
    public static DateOnly OccupiedUntil(Rental rental) => rental.Closure switch
    {
        RentalClosure closed => closed.ReturnDate,
        null => rental.EndDate
    };

    public static bool Overlaps(Rental rental, BookingPeriod period) =>
        rental.StartDate < period.End && period.Start < OccupiedUntil(rental) &&
        rental.StartDate < OccupiedUntil(rental);

    // Stage 1: a pure, deferred availability query with no customer-specific filter.
    public static IEnumerable<Car> SelectAvailable(RentalData data, BookingPeriod period) =>
        data.Cars.Where(car => !data.Rentals.Any(r => r.CarId == car.Id && Overlaps(r, period)));

    // Stage 2 calls the existing yield-return transformation and configured policies.
    public static Offer[] PriceOffers(IEnumerable<Car> available, BookingPeriod period,
        int fee, Func<Car, decimal> policy) =>
        ReportQueries.CreateOffers(available, period.Days, fee, policy).ToArray();

    // Stage 3 materialises reusable, deterministically ordered report snapshots.
    public static ReportBundle BuildSearchReport(RentalData data, Offer[] standard, Offer[] discounted) =>
        new(ReportQueries.AffordableOffers(standard, decimal.MaxValue),
            ReportQueries.AffordableOffers(discounted, decimal.MaxValue),
            ReportQueries.FleetReport(data.Cars, ReportQueries.CreatePrice(0m)));

    public static ReportBundle Search(RentalData data, BookingPeriod period, int serviceFee)
    {
        Car[] available = SelectAvailable(data, period).ToArray();
        Offer[] standard = PriceOffers(available, period, serviceFee, ReportQueries.CreatePrice(0m));
        Offer[] discounted = PriceOffers(available, period, serviceFee, ReportQueries.CreatePrice(.10m));
        return BuildSearchReport(data, standard, discounted);
    }

    public static Result<Car, RentalError> ValidateBooking(RentalData data, int customerId,
        int carId, BookingPeriod period) =>
        RentalWorkflow.FindCustomer(data.Customers, customerId)
            .ToResult(new RentalError("CUSTOMER_NOT_FOUND", $"Customer {customerId} was not found."))
            .Bind(customer => Option<Car>.FromNullable(data.Cars.FirstOrDefault(c => c.Id == carId))
                .ToResult(new RentalError("CAR_NOT_FOUND", $"Car {carId} was not found."))
                .Bind(car => customer switch
                {
                    { Age: < 18 } => FailCar("CUSTOMER_NOT_ELIGIBLE", "Customer must be at least 18."),
                    { HasDrivingLicense: false } => FailCar("CUSTOMER_NOT_ELIGIBLE", "Customer needs a driving license."),
                    { Age: < 21 } when car.Type == CarType.Luxury =>
                        FailCar("CUSTOMER_NOT_ELIGIBLE", "Luxury cars require age 21 or older."),
                    _ when data.Rentals.Any(r => r.CarId == carId && Overlaps(r, period)) =>
                        FailCar("CAR_UNAVAILABLE", "This car is already booked for part of the selected period."),
                    _ => Result<Car, RentalError>.Ok(car)
                }));

    public static Result<RentalChange, RentalError> Create(RentalData data, int customerId,
        int carId, BookingPeriod period, int serviceFee) =>
        ValidateBooking(data, customerId, carId, period).Bind(car =>
        {
            if (serviceFee < 0)
                return Result<RentalChange, RentalError>.Fail(new("FEE_INVALID", "Service fee cannot be negative."));
            int maxId = data.Rentals.Select(r => r.Id).DefaultIfEmpty(0).Max();
            if (maxId == int.MaxValue)
                return Result<RentalChange, RentalError>.Fail(new("ID_EXHAUSTED", "No more rental IDs are available."));
            var rental = new Rental(maxId + 1, customerId, carId, period.Start, period.End,
                DailyPrice: PricingPolicies.Standard(car), ServiceFee: serviceFee);
            return Result<RentalChange, RentalError>.Ok(new(
                Snapshot(data, data.Rentals.Append(rental)), rental));
        });

    public static Result<RentalChange, RentalError> Close(RentalData data, int rentalId,
        DateOnly returned, string? payment) =>
        Option<Rental>.FromNullable(data.Rentals.FirstOrDefault(r => r.Id == rentalId))
            .ToResult(new RentalError("RENTAL_NOT_FOUND", $"Rental {rentalId} was not found."))
            .Bind(rental => rental.Closure switch
            {
                RentalClosure => Result<RentalChange, RentalError>.Fail(new("RENTAL_ALREADY_CLOSED",
                    $"Rental {rentalId} has already been closed.")),
                null => CloseActive(data, rental, returned, payment)
            });

    private static Result<RentalChange, RentalError> CloseActive(RentalData data, Rental rental,
        DateOnly returned, string? payment)
    {
        if (returned < rental.StartDate)
            return Result<RentalChange, RentalError>.Fail(new("RETURN_DATE_INVALID",
                "Return date cannot be before the rental start date."));
        return ConsoleInput.Payment(payment).Bind(method =>
            Option<Car>.FromNullable(data.Cars.FirstOrDefault(c => c.Id == rental.CarId))
                .ToResult(new RentalError("CAR_NOT_FOUND", "The rental's car no longer exists."))
                .Map(car =>
                {
                    // No refund for an early return; a late return adds the booked daily rate.
                    int billedDays = Math.Max(rental.EndDate.DayNumber, returned.DayNumber) - rental.StartDate.DayNumber;
                    decimal daily = rental.DailyPrice > 0 ? rental.DailyPrice : PricingPolicies.Standard(car);
                    var closed = rental with { Closure = new(returned, method, daily * billedDays + rental.ServiceFee) };
                    return new RentalChange(Snapshot(data,
                        data.Rentals.Select(r => r.Id == rental.Id ? closed : r)), closed);
                }));
    }

    private static RentalData Snapshot(RentalData data, IEnumerable<Rental> rentals) =>
        new(Array.AsReadOnly(data.Cars.ToArray()), Array.AsReadOnly(data.Customers.ToArray()),
            Array.AsReadOnly(rentals.ToArray()));

    private static Result<Car, RentalError> FailCar(string code, string message) =>
        Result<Car, RentalError>.Fail(new(code, message));
}
