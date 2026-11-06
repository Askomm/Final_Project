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

Check("Factory success is used by the validated workflow", () =>
{
    RentalRequestCreationResult creation = RentalRequestFactory.Create(
        SampleCustomers(), 2, "14-10-2026", "16-10-2026");

    RentalSearchResult result = creation switch
    {
        RentalRequestCreationResult.Valid valid => Pipeline.RunValidated(
            SampleCars(), SampleRentals(), valid.Request, "Standard", PricingPolicies.Standard),
        RentalRequestCreationResult.Invalid invalid => throw new Exception(
            "Expected a valid request: " + string.Join("; ", invalid.Reasons)),
        _ => throw new Exception("Unexpected rental request result.")
    };

    Assert(result.CustomerId == 2, "The validated customer should be used by the workflow.");
    Assert(result.Quotes.Count == 2, "The safe workflow should produce two quotes.");
    Assert(result.Quotes.All(quote => quote.TotalPrice == 50000m),
        "The safe workflow should calculate the expected totals.");
});

Check("Factory rejects missing customer and invalid dates", () =>
{
    RentalRequestCreationResult missingCustomer = RentalRequestFactory.Create(
        SampleCustomers(), 999, "14-10-2026", "16-10-2026");
    Assert(missingCustomer is RentalRequestCreationResult.Invalid,
        "An unknown customer must not produce a validated request.");
    Assert(((RentalRequestCreationResult.Invalid)missingCustomer).Reasons.Any(
            reason => reason.Contains("not found", StringComparison.OrdinalIgnoreCase)),
        "The missing-customer result should explain the reason.");

    RentalRequestCreationResult invalidDates = RentalRequestFactory.Create(
        SampleCustomers(), 2, "14-10-2026", "14-10-2026");
    Assert(invalidDates is RentalRequestCreationResult.Invalid,
        "A period with equal start and end dates must be rejected.");
    Assert(((RentalRequestCreationResult.Invalid)invalidDates).Reasons.Any(
            reason => reason.Contains("later", StringComparison.OrdinalIgnoreCase)),
        "The invalid-date result should explain the date rule.");

    RentalRequestCreationResult invalidId = RentalRequestFactory.Create(
        SampleCustomers(), 0, "14-10-2026", "16-10-2026");
    Assert(invalidId is RentalRequestCreationResult.Invalid &&
           ((RentalRequestCreationResult.Invalid)invalidId).Reasons.Any(
               reason => reason.Contains("positive", StringComparison.OrdinalIgnoreCase)),
        "A non-positive customer ID must be rejected with a reason.");

    RentalRequestCreationResult badFormat = RentalRequestFactory.Create(
        SampleCustomers(), 2, "2026/10/14", "2026/10/16");
    Assert(badFormat is RentalRequestCreationResult.Invalid &&
           ((RentalRequestCreationResult.Invalid)badFormat).Reasons.Any(
               reason => reason.Contains("dd-MM-yyyy", StringComparison.OrdinalIgnoreCase)),
        "Dates outside the required format must be rejected with a reason.");

    Customer underage = new(7, "Taylor", 17, true, TransmissionType.Manual);
    RentalRequestCreationResult underageResult = RentalRequestFactory.Create(
        new[] { underage }, 7, "14-10-2026", "16-10-2026");
    Assert(underageResult is RentalRequestCreationResult.Invalid &&
           ((RentalRequestCreationResult.Invalid)underageResult).Reasons.Any(
               reason => reason.Contains("18", StringComparison.OrdinalIgnoreCase)),
        "A customer under 18 must be rejected with a reason.");
});

Check("Pattern matching handles both result cases and eligibility rules", () =>
{
    RentalRequestCreationResult valid = RentalRequestFactory.Create(
        SampleCustomers(), 2, "14-10-2026", "16-10-2026");
    string validCase = valid switch
    {
        RentalRequestCreationResult.Valid value => $"Valid customer {value.Request.Customer.Id}",
        RentalRequestCreationResult.Invalid => "Invalid",
        _ => throw new Exception("Unknown case.")
    };
    Assert(validCase == "Valid customer 2", "The valid case should carry the request payload.");

    Customer noLicense = new(8, "Sam", 25, false, TransmissionType.Automat);
    RentalRequestCreationResult invalid = RentalRequestFactory.Create(
        new[] { noLicense }, 8, "14-10-2026", "16-10-2026");
    string invalidCase = invalid switch
    {
        RentalRequestCreationResult.Valid => "Valid",
        RentalRequestCreationResult.Invalid value => string.Join("; ", value.Reasons),
        _ => throw new Exception("Unknown case.")
    };
    Assert(invalidCase.Contains("driving license", StringComparison.OrdinalIgnoreCase),
        "A customer without a license must be rejected with a reason.");
});

Check("One-day boundary, repeated operation, and state preservation", () =>
{
    Car[] cars = SampleCars();
    Rental[] rentals = SampleRentals();
    Customer[] customers = SampleCustomers();
    Car[] originalCars = cars.ToArray();
    Rental[] originalRentals = rentals.ToArray();
    Customer[] originalCustomers = customers.ToArray();

    RentalRequestCreationResult creation = RentalRequestFactory.Create(
        customers, 2, "14-10-2026", "15-10-2026");
    Assert(creation is RentalRequestCreationResult.Valid,
        "A one-day rental period must be valid.");
    ValidatedRentalRequest request = ((RentalRequestCreationResult.Valid)creation).Request;

    RentalSearchResult first = Pipeline.RunValidated(
        cars, rentals, request, "Standard", PricingPolicies.Standard);
    RentalSearchResult second = Pipeline.RunValidated(
        cars, rentals, request, "Standard", PricingPolicies.Standard);

    Assert(first.Quotes.All(quote => quote.Days == 1),
        "The exact next-day boundary should produce one rental day.");
    Assert(first.Quotes.All(quote => quote.TotalPrice == 25000m),
        "A one-day Comfort Automat rental should cost 25,000 KZT.");
    Assert(first.Quotes.SequenceEqual(second.Quotes),
        "Repeating the same request should return the same quotes.");
    Assert(cars.SequenceEqual(originalCars), "Cars must remain unchanged after repeated operations.");
    Assert(rentals.SequenceEqual(originalRentals), "Rentals must remain unchanged after repeated operations.");

    RentalRequestCreationResult rejected = RentalRequestFactory.Create(
        customers, 2, "14-10-2026", "14-10-2026");
    Assert(rejected is RentalRequestCreationResult.Invalid,
        "A failed request must not create a validated state.");
    Assert(cars.SequenceEqual(originalCars) && rentals.SequenceEqual(originalRentals) &&
           customers.SequenceEqual(originalCustomers),
        "A rejected request must leave the original collections unchanged.");
});

Check("Assignment 7 integrated Result workflow succeeds", () =>
{
    Result<RentalQuoteComparison, RentalError> result = RentalWorkflow.SearchQuotes(
        SampleData(), "2", "14-10-2026", "16-10-2026");

    Assert(result is Result<RentalQuoteComparison, RentalError>.Success,
        "A valid request should complete the Result workflow.");
    RentalQuoteComparison comparison =
        ((Result<RentalQuoteComparison, RentalError>.Success)result).Value;
    Assert(comparison.Standard.Quotes.Count == 2, "The successful chain should return two Standard quotes.");
    Assert(comparison.Standard.Quotes.All(quote => quote.TotalPrice == 50000m),
        "The Standard total should be 50,000 KZT for each sample car.");
    Assert(comparison.Discounted.Quotes.All(quote => quote.TotalPrice == 45000m),
        "The discounted total should be 45,000 KZT for each sample car.");
});

Check("Assignment 7 reports distinct domain failures", () =>
{
    Result<RentalQuoteComparison, RentalError> invalidId = RentalWorkflow.SearchQuotes(
        SampleData(), "0", "14-10-2026", "16-10-2026");
    AssertError(invalidId, "CUSTOMER_ID_INVALID");

    Result<RentalQuoteComparison, RentalError> missingCustomer = RentalWorkflow.SearchQuotes(
        SampleData(), "999", "14-10-2026", "16-10-2026");
    AssertError(missingCustomer, "CUSTOMER_NOT_FOUND");

    Result<RentalQuoteComparison, RentalError> invalidPeriod = RentalWorkflow.SearchQuotes(
        SampleData(), "2", "14-10-2026", "14-10-2026");
    AssertError(invalidPeriod, "RENTAL_PERIOD_INVALID");

    Rental[] blockingRentals = SampleRentals().Append(
        new Rental(2, 3, 1, new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16))).ToArray();
    RentalData noCarsData = new(SampleCars(), SampleCustomers(), blockingRentals);
    Result<RentalQuoteComparison, RentalError> noCars = RentalWorkflow.SearchQuotes(
        noCarsData, "1", "14-10-2026", "16-10-2026");
    AssertError(noCars, "NO_CARS_AVAILABLE");
});

Check("Option lookup distinguishes present and absent customers", () =>
{
    Option<Customer> present = RentalWorkflow.FindCustomer(SampleCustomers(), 2);
    Assert(present is Option<Customer>.Some, "A known customer should produce Some.");
    Assert(((Option<Customer>.Some)present).Value.Id == 2, "Some should carry the requested customer.");

    Option<Customer> absent = RentalWorkflow.FindCustomer(SampleCustomers(), 999);
    Assert(absent is Option<Customer>.None, "An unknown customer should produce None.");

    Customer noLicense = new(9, "Taylor", 25, false, TransmissionType.Automat);
    Option<Customer> customerWithFalseLicense = RentalWorkflow.FindCustomer(new[] { noLicense }, 9);
    Assert(customerWithFalseLicense is Option<Customer>.Some,
        "A stored false license flag is not the same as a missing customer.");
    Assert(!((Option<Customer>.Some)customerWithFalseLicense).Value.HasDrivingLicense,
        "The real false value should be preserved.");

    Result<RentalQuoteComparison, RentalError> rejected = RentalWorkflow.SearchQuotes(
        new RentalData(SampleCars(), new[] { noLicense }, Array.Empty<Rental>()),
        "9", "14-10-2026", "16-10-2026");
    AssertError(rejected, "CUSTOMER_NOT_ELIGIBLE");
});

Check("Map transforms a successful value", () =>
{
    bool called = false;
    Result<int, RentalError> source = Result<int, RentalError>.Ok(21);
    Result<string, RentalError> mapped = source.Map(value =>
    {
        called = true;
        return $"Value: {value * 2}";
    });

    Assert(called, "Map should invoke the pure transformation for success.");
    Assert(mapped is Result<string, RentalError>.Success,
        "Map should preserve the success case.");
    Assert(((Result<string, RentalError>.Success)mapped).Value == "Value: 42",
        "Map should return the transformed value.");
});

Check("Bind continues with the next Result operation", () =>
{
    Result<int, RentalError> source = Result<int, RentalError>.Ok(5);
    Result<int, RentalError> bound = source.Bind(value =>
        Result<int, RentalError>.Ok(value + 7));

    Assert(bound is Result<int, RentalError>.Success, "Bind should preserve a successful next result.");
    Assert(((Result<int, RentalError>.Success)bound).Value == 12,
        "The next operation should receive the previous value.");

    Result<int, RentalError> rejectedNext = source.Bind<int>(_ =>
        Result<int, RentalError>.Fail(new RentalError("NEXT_STEP_REJECTED", "Next step rejected.")));
    AssertError(rejectedNext, "NEXT_STEP_REJECTED");
});

Check("Map and Bind propagate failure without invoking functions", () =>
{
    int calls = 0;
    RentalError expected = new("STOPPED", "Previous step failed.");
    Result<int, RentalError> source = Result<int, RentalError>.Fail(expected);

    Result<int, RentalError> mapped = source.Map(value =>
    {
        calls++;
        return value + 1;
    });
    Result<string, RentalError> bound = source.Bind(value =>
    {
        calls++;
        return Result<string, RentalError>.Ok(value.ToString());
    });

    AssertError(mapped, "STOPPED");
    AssertError(bound, "STOPPED");
    Assert(calls == 0, "Neither Map nor Bind may call its function after failure.");
});

Check("Valid JSON file loads as one complete dataset", () =>
{
    string path = Path.Combine(Directory.GetCurrentDirectory(), "Data", "rental-data.json");
    Result<RentalData, RentalError> result = RentalDataLoader.Load(path);

    Assert(result is Result<RentalData, RentalError>.Success,
        "The sample JSON file should be read successfully.");
    RentalData data = ((Result<RentalData, RentalError>.Success)result).Value;
    Assert(data.Cars.Count == 5 && data.Customers.Count == 3 && data.Rentals.Count == 1,
        "All three data collections should be loaded together.");
});

Check("Missing input file becomes an explicit failure", () =>
{
    string directory = Path.Combine(Path.GetTempPath(), "assignment7-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        Result<RentalData, RentalError> result = RentalDataLoader.Load(
            Path.Combine(directory, "missing.json"));
        AssertError(result, "DATA_FILE_MISSING");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
});

Check("Malformed JSON becomes a failure without partial data", () =>
{
    string path = Path.Combine(Path.GetTempPath(), "assignment7-" + Guid.NewGuid().ToString("N") + ".json");
    File.WriteAllText(path, "{ cars: [ this is not JSON");
    try
    {
        Result<RentalData, RentalError> result = RentalDataLoader.Load(path);
        AssertError(result, "DATA_FILE_MALFORMED");
    }
    finally
    {
        File.Delete(path);
    }
});

Check("Result cases reject a missing payload", () =>
{
    bool successRejectedNull = false;
    bool failureRejectedNull = false;
    try
    {
        Result<string, RentalError>.Ok(null!);
    }
    catch (ArgumentNullException)
    {
        successRejectedNull = true;
    }

    try
    {
        Result<int, RentalError>.Fail(null!);
    }
    catch (ArgumentNullException)
    {
        failureRejectedNull = true;
    }

    Assert(successRejectedNull, "Success must not contain a missing required payload.");
    Assert(failureRejectedNull, "Failure must not contain a missing error.");
});


Check("A8 budget extension includes boundary and excludes expensive offers", () =>
{
    var rows = ReportQueries.CreateOffers(SampleCars(), 2, 500, ReportQueries.CreatePrice(0m)).ToArray();
    Assert(rows.WithinBudget(30500m).Single().CarId == 1, "Inclusive budget must select car 1.");
    Assert(rows.WithinBudget(0).Count() == 0, "Zero budget should have no matches.");
});
Check("A8 fleet extension groups and sums", () =>
{
    var rows = SampleCars().SummarizeFleet(ReportQueries.CreatePrice(0m)).ToArray();
    var comfort = rows.Single(r => r.Type == CarType.Comfort);
    Assert(comfort.Cars == 3 && comfort.DailyPotential == 75000m, "Comfort aggregate incorrect.");
    Assert(rows.Sum(r => r.DailyPotential) == 125000m, "Fleet sum incorrect.");
});
Check("A8 both curried functions differ on same input", () =>
{
    var standard = ReportQueries.CreatePrice(0m);
    var discount = ReportQueries.CreatePrice(0.10m);
    Assert(standard(SampleCars()[1]) == 25000m && discount(SampleCars()[1]) == 22500m,
        "Configured prices must be 25000 and 22500 on the same car.");
});
Check("A8 invalid configuration rejected", () =>
{
    foreach (decimal rate in new[] { -0.1m, 1.1m })
    {
        bool rejected = false;
        try { ReportQueries.CreatePrice(rate); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Assert(rejected, "Invalid discount accepted.");
    }
});
Check("A8 both reports accept empty input", () =>
{
    Assert(ReportQueries.AffordableOffers(Array.Empty<Offer>(), 60000m).Length == 0, "Empty offers.");
    Assert(ReportQueries.FleetReport(Array.Empty<Car>(), ReportQueries.CreatePrice(0)).Length == 0, "Empty fleet.");
});
Check("A8 reports with no matches", () =>
{
    Assert(ReportQueries.AffordableOffers(ReportQueries.CreateOffers(SampleCars(), 2, 500,
        ReportQueries.CreatePrice(0)), 1m).Length == 0, "No matching offers expected.");
    Assert(ReportQueries.FleetReport(SampleCars().Where(c => c.Id > 999),
        ReportQueries.CreatePrice(0)).Length == 0, "No matching fleet expected.");
});
Check("A8 deterministic tied offer ordering", () =>
{
    Offer[] rows = { new(4, "Same", 25, 2, 0, 50), new(2, "Same", 25, 2, 0, 50), new(3, "A", 25, 2, 0, 50) };
    Assert(ReportQueries.AffordableOffers(rows, 50).Select(r => r.CarId).SequenceEqual(new[] { 3, 2, 4 }), "Tie order.");
    Assert(ReportQueries.AffordableOffers(rows.Reverse(), 50).Select(r => r.CarId).SequenceEqual(new[] { 3, 2, 4 }), "Input order leaked.");
});
Check("A8 deterministic tied fleet ordering", () =>
{
    var rows = ReportQueries.FleetReport(new[] { SampleCars()[4], SampleCars()[0] }, _ => 100m);
    Assert(rows[0].Type == CarType.Default && rows[1].Type == CarType.Luxury, "Category tie-break.");
});
Check("A8 deferred timing and snapshot reuse", () =>
{
    int calls = 0;
    var lazy = ReportQueries.CreateOffers(SampleCars(), 2, 500, car => { calls++; return PricingPolicies.Standard(car); });
    Assert(calls == 0, "Creation must not calculate.");
    Assert(lazy.First().TotalPrice == 30500m && calls == 1, "First advances only one item.");
    var snapshot = lazy.ToArray();
    Assert(calls == 6 && snapshot.Length == 5, "Materialisation processes all five again.");
    _ = snapshot.Sum(x => x.TotalPrice); _ = snapshot.Sum(x => x.TotalPrice);
    Assert(calls == 6, "Snapshot reuse must not recalculate.");
    _ = lazy.ToArray(); Assert(calls == 11, "Deferred re-enumeration recalculates predictably.");
});
Check("A8 real native call matches manual fee", () =>
{
    var fee = new NativeFee();
    Assert(Value(fee.Calculate(2)) == 500, "2 * 250 = 500 KZT (real DLL).");
    Assert(Value(fee.Calculate(1)) == 250 && Value(fee.Calculate(366)) == 91500, "Boundary fee.");
});
Check("A8 adapter rejects days before calling native", () =>
{
    int calls = 0;
    var fee = new NativeFee(d => { calls++; return d * 250; });
    AssertError(fee.Calculate(0), "FEE_DAYS_INVALID"); AssertError(fee.Calculate(367), "FEE_DAYS_INVALID");
    Assert(calls == 0, "Invalid days must not reach native code.");
});
Check("A8 native binding failures become Result failures", () =>
{
    foreach (Exception exception in new Exception[] { new DllNotFoundException("test"),
        new EntryPointNotFoundException("test"), new BadImageFormatException("test") })
        AssertError(new NativeFee(_ => throw exception).Calculate(2), "NATIVE_UNAVAILABLE");
    AssertError(new NativeFee(_ => -1).Calculate(2), "NATIVE_REJECTED");
});
Check("A8 complete JSON to reports native and export workflow", () =>
{
    var data = Value(RentalDataLoader.Load(Path.Combine("Data", "rental-data.json")));
    var reports = Value(RunReports(data, new NativeFee()));
    Assert(reports.Standard.Select(x => x.CarId).SequenceEqual(new[] { 4, 2 }), "Expected BMW then Toyota.");
    Assert(reports.Standard.All(x => x.TotalPrice == 50500m && x.ServiceFee == 500), "Standard totals.");
    Assert(reports.Discounted.All(x => x.TotalPrice == 45500m), "Discounted totals.");
    Assert(reports.Fleet.Select(x => x.DailyPotential).SequenceEqual(new[] { 75000m, 35000m, 15000m }), "Fleet totals.");
    InTempDirectory(dir =>
    {
        string file = Path.Combine(dir, "report.json");
        Assert(Value(ReportExporter.Save(file, reports)) == file, "Export result.");
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
        Assert(json.RootElement.GetProperty("Standard")[0].GetProperty("TotalPrice").GetDecimal() == 50500m,
            "Exported total must match report.");
        using var exclusive = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert(exclusive.Length > 0, "Export writer must release its handle.");
    });
});
Check("A8 complete failure workflow preserves data and previous export", () =>
{
    InTempDirectory(dir =>
    {
        string path = Path.Combine(dir, "existing.json"); File.WriteAllText(path, "previous content");
        var data = Value(RentalDataLoader.Load(Path.Combine("Data", "rental-data.json")));
        var cars = data.Cars.ToArray(); var rentals = data.Rentals.ToArray();
        var result = RunReports(data, new NativeFee(_ => throw new DllNotFoundException("controlled")));
        // Same fail-fast export gate as the application.
        if (result is Result<ReportBundle, RentalError>.Success ok) ReportExporter.Save(path, ok.Value);
        AssertError(result, "NATIVE_UNAVAILABLE");
        Assert(File.ReadAllText(path) == "previous content", "Failure changed previous output.");
        Assert(data.Cars.SequenceEqual(cars) && data.Rentals.SequenceEqual(rentals), "Failure changed domain state.");
    });
});
Check("A8 export failure cleans temporary file and releases writer", () =>
{
    InTempDirectory(dir =>
    {
        string target = Path.Combine(dir, "occupied"); Directory.CreateDirectory(target);
        AssertError(ReportExporter.Save(target, new ReportBundle([], [], [])), "REPORT_EXPORT_FAILED");
        Assert(Directory.GetFiles(dir).Length == 0, "Temporary file must be deletable after move failure.");
        Directory.Delete(target); Assert(!Directory.Exists(target), "Destination remains usable.");
    });
});
Check("A8 input file handles released after success and parsing failure", () =>
{
    InTempDirectory(dir =>
    {
        string path = Path.Combine(dir, "input.json");
        foreach (bool malformed in new[] { false, true })
        {
            File.WriteAllText(path, malformed ? "{" : File.ReadAllText(Path.Combine("Data", "rental-data.json")));
            var loaded = RentalDataLoader.Load(path);
            if (malformed) AssertError(loaded, "DATA_FILE_MALFORMED"); else _ = Value(loaded);
            using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert(exclusive.CanWrite, "Read convenience API leaked handle.");
            File.Delete(path); Assert(!File.Exists(path), "Cleanup failed.");
        }
    });
});
Check("A8 duplicate and dangling data rejected", () =>
{
    InTempDirectory(dir =>
    {
        string valid = File.ReadAllText(Path.Combine("Data", "rental-data.json"));
        foreach (string bad in new[] { valid.Replace("\"id\": 5", "\"id\": 4"),
            valid.Replace("\"carId\": 3", "\"carId\": 999"),
            valid.Replace("2026-10-17", "2026-10-12"), valid.Replace("\"type\": \"Luxury\"", "\"type\": \"Unknown\"") })
        {
            string path = Path.Combine(dir, "bad.json"); File.WriteAllText(path, bad);
            AssertError(RentalDataLoader.Load(path), "DATA_FILE_MALFORMED");
        }
    });
});
Check("A8 budget and excessive period failures", () =>
{
    AssertError(ReportWorkflow.Run(SampleData(), "2", "14-10-2026", "16-10-2026", -1m,
        ReportQueries.CreatePrice(0), ReportQueries.CreatePrice(.1m), new NativeFee()), "BUDGET_INVALID");
    AssertError(ReportWorkflow.Run(SampleData(), "2", "14-10-2026", "16-10-2027", 60000m,
        ReportQueries.CreatePrice(0), ReportQueries.CreatePrice(.1m), new NativeFee()), "FEE_DAYS_INVALID");
});

Check("A8 missing car fields are rejected instead of defaulted", () =>
    AssertMissingFields("cars", "id", "name", "type", "color", "transmission"));
Check("A8 missing customer fields are rejected instead of defaulted", () =>
    AssertMissingFields("customers", "id", "name", "age", "hasDrivingLicense", "preferredTransmission"));
Check("A8 missing rental fields are rejected instead of defaulted", () =>
    AssertMissingFields("rentals", "id", "customerId", "carId", "startDate", "endDate"));
Check("A8 explicit zero and false fields remain valid stored data", () =>
{
    InTempDirectory(dir =>
    {
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText("Data/rental-data.json"))!;
        document["customers"]![0]!["age"] = 0;
        document["customers"]![0]!["hasDrivingLicense"] = false;
        string path = Path.Combine(dir, "explicit-values.json");
        File.WriteAllText(path, document.ToJsonString());
        var data = Value(RentalDataLoader.Load(path));
        Assert(data.Customers[0].Age == 0 && !data.Customers[0].HasDrivingLicense,
            "Explicit values must not be confused with absent fields.");
        Assert(data.Cars[0].Type == CarType.Default && data.Cars[0].Transmission == TransmissionType.Manual,
            "Zero-valued enum members remain valid when explicitly provided.");
        AssertError(RentalWorkflow.SearchQuotes(data, "1", "14-10-2026", "16-10-2026"), "CUSTOMER_NOT_ELIGIBLE");
    });
});
Check("A8 failed request skips native and configured calculations", () =>
{
    int nativeCalls = 0, priceCalls = 0;
    var result = ReportWorkflow.Run(SampleData(), "999", "14-10-2026", "16-10-2026", 60000m,
        _ => { priceCalls++; return 1m; }, _ => { priceCalls++; return 1m; },
        new NativeFee(_ => { nativeCalls++; return 500; }));
    AssertError(result, "CUSTOMER_NOT_FOUND");
    Assert(nativeCalls == 0 && priceCalls == 0, "Dependent work ran after request failure.");
});
Check("A8 actual application failures preserve an existing report", () =>
{
    InTempDirectory(dir =>
    {
        string report = Path.Combine(dir, "rental-reports.json");
        File.WriteAllText(report, "previous report");
        foreach (var scenario in new[] {
            (Flag: "--demo-missing-file", Error: "DATA_FILE_MISSING"),
            (Flag: "--demo-malformed-file", Error: "DATA_FILE_MALFORMED"),
            (Flag: "--demo-native-failure", Error: "NATIVE_UNAVAILABLE") })
        {
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            {
                WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add("exec");
            start.ArgumentList.Add("--runtimeconfig");
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Code.Tests.runtimeconfig.json"));
            start.ArgumentList.Add(typeof(ReportWorkflow).Assembly.Location);
            start.ArgumentList.Add(scenario.Flag);
            using var process = System.Diagnostics.Process.Start(start)
                ?? throw new Exception("Could not start application.");
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            if (scenario.Flag == "--demo-native-failure")
                process.StandardInput.WriteLine("2\n14-10-2026\n16-10-2026");
            process.StandardInput.Close();
            if (!process.WaitForExit(15000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                throw new Exception("Application did not finish within 15 seconds.");
            }
            Assert(process.ExitCode == 1, "Failure must exit with code 1: " + errors.GetAwaiter().GetResult());
            Assert(output.GetAwaiter().GetResult().Contains(scenario.Error), "Expected application error missing.");
            Assert(File.ReadAllText(report) == "previous report", "Application overwrote the previous report.");
            Assert(Directory.GetFiles(dir).Length == 1, "Failure left unexpected output files.");
        }
    });
});

static void AssertMissingFields(string collection, params string[] fields)
{
    InTempDirectory(dir =>
    {
        foreach (string field in fields)
        {
            var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText("Data/rental-data.json"))!;
            Assert(document[collection]![0]!.AsObject().Remove(field), "Fixture field was not found: " + field);
            string path = Path.Combine(dir, "missing-field.json");
            File.WriteAllText(path, document.ToJsonString());
            AssertError(RentalDataLoader.Load(path), "DATA_FILE_MALFORMED");
        }
    });
}

static T Value<T>(Result<T, RentalError> result) => result switch
{
    Result<T, RentalError>.Success ok => ok.Value,
    Result<T, RentalError>.Failure error => throw new Exception(error.Error.ToString()),
    _ => throw new Exception("Unknown result")
};
static Result<ReportBundle, RentalError> RunReports(RentalData data, NativeFee native) =>
    ReportWorkflow.Run(data, "2", "14-10-2026", "16-10-2026", 60000m,
        ReportQueries.CreatePrice(0), ReportQueries.CreatePrice(.1m), native);
static void InTempDirectory(Action<string> test)
{
    string dir = Path.Combine(Directory.GetCurrentDirectory(), ".test-assignment8-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try { test(dir); } finally { Directory.Delete(dir, recursive: true); }
}
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

static RentalData SampleData() => new(SampleCars(), SampleCustomers(), SampleRentals());

static void AssertError<T>(Result<T, RentalError> result, string expectedCode)
{
    Assert(result is Result<T, RentalError>.Failure,
        $"Expected failure code {expectedCode}, but got success.");
    RentalError error = ((Result<T, RentalError>.Failure)result).Error;
    Assert(error.Code == expectedCode,
        $"Expected error code {expectedCode}, got {error.Code}: {error.Message}");
    Assert(!string.IsNullOrWhiteSpace(error.Message), "A domain error should explain the failure.");
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

static Customer[] SampleCustomers() => new[]
{
    new Customer(1, "Alex", 18, true, TransmissionType.Manual),
    Customer20()
};

static Customer Customer20() => new(2, "Egor", 20, true, TransmissionType.Automat);

static RentalPeriod SamplePeriod() => new(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16));
