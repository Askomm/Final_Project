using Code;

int passed = 0;
int failed = 0;

Check("Normal workflow", () =>
{
    RentalSearchResult result = Pipeline.Run(
        SampleCars(), SampleRentals(), Customer20(), SamplePeriod(),
        "Standard", PricingPolicies.Standard);

    Assert(result.Quotes.Count == 2, "Expected two available cars.");
    Assert(result.Quotes.All(quote => quote.CarId == 2 || quote.CarId == 4),
        "Only cars 002 and 004 should be available.");
    Assert(result.Quotes.All(quote => quote.TotalPrice == 50000m),
        "Each Comfort Automat car should cost 50,000 KZT for two days.");
});

Check("Empty dataset", () =>
{
    List<Car> emptyCars = TryParse.ParseCars(Array.Empty<string>(), out List<string> errors);
    Assert(emptyCars.Count == 0 && errors.Count == 0, "An empty file should be a valid empty dataset.");

    RentalSearchResult result = Pipeline.Run(
        emptyCars, Array.Empty<Rental>(), Customer20(), SamplePeriod(),
        "Standard", PricingPolicies.Standard);
    Assert(result.Quotes.Count == 0, "The empty dataset should produce no quotes.");
});

Check("Age 18 boundary", () =>
{
    Car regularCar = new(1, "Toyota Camry", CarType.Default, "White", TransmissionType.Manual);
    Customer age18 = new(1, "Alex", 18, true, TransmissionType.Manual);
    RentalSearchResult result = Pipeline.Run(
        new[] { regularCar }, Array.Empty<Rental>(), age18, SamplePeriod(),
        "Standard", PricingPolicies.Standard);

    Assert(result.Quotes.Count == 1, "A licensed 18-year-old should be allowed to rent a regular car.");
});

Check("Invalid and missing input", () =>
{
    Assert(!TryParse.TryParseCar(null, out _, out _), "A missing car row should be rejected.");
    Assert(!TryParse.TryParseCar("001|Car|0|White|Manual", out _, out _),
        "Numeric enum values must be rejected.");
    Assert(!TryParse.TryParseCustomer("001|Alex|17|True|Manual", out _, out _),
        "A customer under 18 should be rejected.");
    Assert(!TryParse.TryParsePositiveId(null, out _, out _), "A missing customer ID should be rejected.");
    Assert(!TryParse.TryParseRentalPeriod("14-10-2026", "14-10-2026", out _, out _),
        "End date must be later than start date.");
});

Check("Two pricing policies", () =>
{
    RentalSearchResult standard = Pipeline.Run(
        SampleCars(), SampleRentals(), Customer20(), SamplePeriod(),
        "Standard", PricingPolicies.Standard);
    RentalSearchResult discounted = Pipeline.Run(
        SampleCars(), SampleRentals(), Customer20(), SamplePeriod(),
        "10% discount", PricingPolicies.Discounted);

    Assert(standard.Quotes[0].TotalPrice == 50000m, "Standard total should be 50,000 KZT.");
    Assert(discounted.Quotes[0].TotalPrice == 45000m, "Discounted total should be 45,000 KZT.");
});

Check("Stages and input collections stay unchanged", () =>
{
    Car[] cars = SampleCars();
    Rental[] rentals = SampleRentals();
    Car[] originalCars = cars.ToArray();
    Rental[] originalRentals = rentals.ToArray();

    List<Car> selected = Pipeline.SelectAvailableCars(cars, rentals, Customer20(), SamplePeriod());
    List<CarQuote> quotes = Pipeline.CalculateQuotes(selected, SamplePeriod(), PricingPolicies.Standard);
    RentalSearchResult summary = Pipeline.CreateSummary(
        Customer20().Id, SamplePeriod(), "Standard", quotes);

    Assert(selected.Count == 2, "Stage 1 should select two cars.");
    Assert(quotes.Count == 2, "Stage 2 should calculate two quotes.");
    Assert(summary.Quotes.Count == 2, "Stage 3 should create a result containing two quotes.");
    Assert(cars.SequenceEqual(originalCars), "The original car array changed.");
    Assert(rentals.SequenceEqual(originalRentals), "The original rental array changed.");
});

Console.WriteLine($"\nTests: {passed} passed, {failed} failed.");
Environment.ExitCode = failed == 0 ? 0 : 1;

void Check(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"FAIL: {name} - {exception.Message}");
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

static Car[] SampleCars() => new[]
{
    new Car(1, "Toyota Camry 40", CarType.Default, "White", TransmissionType.Manual),
    new Car(2, "Toyota Camry 70", CarType.Comfort, "Black", TransmissionType.Automat),
    new Car(3, "Audi A6", CarType.Comfort, "Gray", TransmissionType.Automat),
    new Car(4, "BMW 5 G30", CarType.Comfort, "Black", TransmissionType.Automat),
    new Car(5, "Mercedes S Class", CarType.Luxury, "Black", TransmissionType.Automat)
};

static Rental[] SampleRentals() => new[]
{
    new Rental(1, 3, 3, new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 17))
};

static Customer Customer20() => new(2, "Egor", 20, true, TransmissionType.Automat);

static RentalPeriod SamplePeriod() => new(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16));
