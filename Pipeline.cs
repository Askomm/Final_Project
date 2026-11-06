namespace Code;

public static class PricingPolicies
{
    public static decimal Standard(Car car)
    {
        decimal price = car.Type switch
        {
            CarType.Default => 15000m,
            CarType.Comfort => 20000m,
            CarType.Luxury => 30000m,
            _ => throw new ArgumentOutOfRangeException(nameof(car))
        };

        if (car.Transmission == TransmissionType.Automat)
            price += 5000m;

        return price;
    }

    public static decimal Discounted(Car car) => Standard(car) * 0.90m;
}

public static class Pipeline
{
    
    public static List<Car> SelectAvailableCars(
        IEnumerable<Car> cars,
        IEnumerable<Rental> rentals,
        Customer customer,
        RentalPeriod period)
    {
        List<Car> available = new();

        foreach (Car car in cars)
        {
            if (customer.Age < 18 || !customer.HasDrivingLicense)
                continue;
            if (car.Type == CarType.Luxury && customer.Age < 21)
                continue;
            if (car.Transmission != customer.PreferredTransmission)
                continue;

            bool isRented = rentals.Any(rental =>
                rental.CarId == car.Id &&
                rental.StartDate < period.EndDate &&
                period.StartDate < rental.EndDate);

            if (!isRented)
                available.Add(car);
        }

        return available;
    }

    
    public static List<CarQuote> CalculateQuotes(
        IEnumerable<Car> cars,
        RentalPeriod period,
        Func<Car, decimal> pricePolicy)
    {
        if (period.Days <= 0)
            throw new ArgumentException("End date must be later than start date.", nameof(period));

        List<CarQuote> quotes = new();
        foreach (Car car in cars)
        {
            decimal dailyPrice = pricePolicy(car);
            quotes.Add(new CarQuote(
                car.Id,
                car.Name,
                dailyPrice,
                period.Days,
                dailyPrice * period.Days));
        }

        return quotes;
    }

   
    public static RentalSearchResult CreateSummary(
        int customerId,
        RentalPeriod period,
        string policyName,
        IEnumerable<CarQuote> quotes)
    {
        CarQuote[] orderedQuotes = quotes
            .OrderBy(quote => quote.TotalPrice)
            .ThenBy(quote => quote.CarName)
            .ToArray();

        return new RentalSearchResult(
            customerId,
            period.StartDate,
            period.EndDate,
            policyName,
            Array.AsReadOnly(orderedQuotes));
    }

    public static RentalSearchResult Run(
        IEnumerable<Car> cars,
        IEnumerable<Rental> rentals,
        Customer customer,
        RentalPeriod period,
        string policyName,
        Func<Car, decimal> pricePolicy)
    {
        List<Car> available = SelectAvailableCars(cars, rentals, customer, period);
        List<CarQuote> quotes = CalculateQuotes(available, period, pricePolicy);
        return CreateSummary(customer.Id, period, policyName, quotes);
    }
}
