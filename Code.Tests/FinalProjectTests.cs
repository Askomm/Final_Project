using Code;
using System.Text.Json.Nodes;

internal static class FinalProjectTests
{
    public static void Run(Action<string, Action> check)
    {
        check("Final ISO dates, leap years, blank and invalid dates", () =>
        {
            Equal(new DateOnly(2028, 2, 29), Value(ConsoleInput.Date("2028-02-29", "Start")));
            foreach (string? text in new[] { null, "", "29-02-2028", "2026-02-29", "2026-13-01", "1" })
                Error(ConsoleInput.Date(text, "Start"), "DATE_INVALID");
        });
        check("Final IDs reject overflow, negatives, decimals and empty input", () =>
        {
            Equal(42, Value(ConsoleInput.PositiveId("42", "Car ID")));
            foreach (string? text in new[] { null, "", "-1", "0", "1.5", "2147483648", "abc" })
                Error(ConsoleInput.PositiveId(text, "Car ID"), "ID_INVALID");
        });
        check("Final validated period boundaries", () =>
        {
            var start = new DateOnly(2026, 1, 1);
            Equal(366, Value(BookingPeriod.Create(start, start.AddDays(366))).Days);
            foreach (int days in new[] { -1, 0, 367 })
                Error(BookingPeriod.Create(start, start.AddDays(days)), "PERIOD_INVALID");
        });
        check("Final search detects containing, partial, exact and adjacent overlaps", () =>
        {
            var data = Data();
            foreach (var dates in new[] { ("2026-12-01", "2026-12-03"), ("2026-11-30", "2026-12-02"),
                ("2026-12-02", "2026-12-04"), ("2026-11-30", "2026-12-04"), ("2026-12-02", "2026-12-03") })
                Equal(false, RentalOperations.SelectAvailable(data, Period(dates.Item1, dates.Item2)).Any(c => c.Id == 1));
            Equal(true, RentalOperations.SelectAvailable(data, Period("2026-12-03", "2026-12-04")).Any(c => c.Id == 1));
            Equal(true, RentalOperations.SelectAvailable(data, Period("2026-11-30", "2026-12-01")).Any(c => c.Id == 1));
        });
        check("Final search is independent of customer transmission preference", () =>
            Equal(2, RentalOperations.Search(Data(), Period(), 500).Standard.Count));
        check("Final empty search and fleet reports are successful", () =>
        {
            var empty = new RentalData(Array.Empty<Car>(), Array.Empty<Customer>(), Array.Empty<Rental>());
            var reports = RentalOperations.Search(empty, Period(), 500);
            Equal(0, reports.Standard.Count); Equal(0, reports.Discounted.Count); Equal(0, reports.Fleet.Count);
        });
        check("Final real policies, native fee and fleet aggregation", () =>
        {
            var reports = RentalOperations.Search(Data(), Period(), Value(new NativeFee().Calculate(2)));
            Equal(30500m, reports.Standard[0].TotalPrice);
            Equal(27500m, reports.Discounted[0].TotalPrice);
            Equal(40000m, reports.Fleet.Sum(f => f.DailyPotential));
        });
        check("Final immutable create and unique IDs", () =>
        {
            var data = Data();
            var changed = Value(RentalOperations.Create(data, 1, 1, Period(), 500));
            Equal(2, changed.Rental.Id); Equal(1, data.Rentals.Count); Equal(2, changed.Data.Rentals.Count);
            Equal(15000m, changed.Rental.DailyPrice); Equal(500, changed.Rental.ServiceFee);
        });
        check("Final missing customer and car use explicit outcomes", () =>
        {
            Error(RentalOperations.Create(Data(), 999, 1, Period(), 500), "CUSTOMER_NOT_FOUND");
            Error(RentalOperations.Create(Data(), 1, 999, Period(), 500), "CAR_NOT_FOUND");
        });
        check("Final age, license and luxury eligibility", () =>
        {
            var data = Data();
            foreach (var customer in new[] { data.Customers[0] with { Age = 17 },
                data.Customers[0] with { HasDrivingLicense = false } })
                Error(RentalOperations.Create(data with { Customers = new[] { customer } }, 1, 1, Period(), 500),
                    "CUSTOMER_NOT_ELIGIBLE");
            var luxury = data with { Cars = new[] { data.Cars[0] with { Type = CarType.Luxury } } };
            Error(RentalOperations.Create(luxury, 1, 1, Period(), 500), "CUSTOMER_NOT_ELIGIBLE");
            Equal(2, Value(RentalOperations.Create(luxury with {
                Customers = new[] { data.Customers[0] with { Age = 21 } } }, 1, 1, Period(), 500)).Rental.Id);
        });
        check("Final duplicate booking and exhausted IDs", () =>
        {
            Error(RentalOperations.Create(Data(), 1, 1, Period("2026-12-01", "2026-12-02"), 500), "CAR_UNAVAILABLE");
            Error(RentalOperations.Create(Data() with { Rentals = new[] { Data().Rentals[0] with { Id = int.MaxValue } } },
                1, 1, Period(), 500), "ID_EXHAUSTED");
            Error(RentalOperations.Create(Data(), 1, 1, Period(), -1), "FEE_INVALID");
        });
        check("Final close records normalized payment and preserves input", () =>
        {
            var data = Data();
            var changed = Value(RentalOperations.Close(data, 1, new(2026, 12, 3), " card "));
            Equal("Card", changed.Rental.Closure!.PaymentMethod);
            Equal(30500m, changed.Rental.Closure.TotalPaid);
            Equal(true, data.Rentals[0].Closure is null);
        });
        check("Final close rejects missing ID, repeat close, invalid date and payment", () =>
        {
            Error(RentalOperations.Close(Data(), 999, new(2026, 12, 3), "Cash"), "RENTAL_NOT_FOUND");
            Error(RentalOperations.Close(Data(), 1, new(2026, 11, 30), "Cash"), "RETURN_DATE_INVALID");
            foreach (var payment in new[] { "", "bitcoin", "1" })
                Error(RentalOperations.Close(Data(), 1, new(2026, 12, 3), payment), "PAYMENT_INVALID");
            var closed = Value(RentalOperations.Close(Data(), 1, new(2026, 12, 3), "Cash"));
            Error(RentalOperations.Close(closed.Data, 1, new(2026, 12, 3), "Card"), "RENTAL_ALREADY_CLOSED");
        });
        check("Final early return frees remaining days and keeps occupied history", () =>
        {
            var closed = Value(RentalOperations.Close(Data(), 1, new(2026, 12, 2), "Transfer"));
            Equal(30500m, closed.Rental.Closure!.TotalPaid);
            Equal(true, RentalOperations.SelectAvailable(closed.Data, Period("2026-12-02", "2026-12-03")).Any(c => c.Id == 1));
            Equal(false, RentalOperations.SelectAvailable(closed.Data, Period("2026-12-01", "2026-12-02")).Any(c => c.Id == 1));
        });
        check("Final late return charges extra days and extends occupied history", () =>
        {
            var closed = Value(RentalOperations.Close(Data(), 1, new(2026, 12, 5), "Cash"));
            Equal(60500m, closed.Rental.Closure!.TotalPaid);
            Equal(false, RentalOperations.SelectAvailable(closed.Data, Period("2026-12-04", "2026-12-05")).Any(c => c.Id == 1));
            Equal(true, RentalOperations.SelectAvailable(closed.Data, Period("2026-12-05", "2026-12-06")).Any(c => c.Id == 1));
        });
        check("Final same-day return occupies no full day but retains minimum booked charge", () =>
        {
            var closed = Value(RentalOperations.Close(Data(), 1, new(2026, 12, 1), "Cash"));
            Equal(30500m, closed.Rental.Closure!.TotalPaid);
            Equal(true, RentalOperations.SelectAvailable(closed.Data, Period("2026-12-01", "2026-12-02")).Any(c => c.Id == 1));
        });
        check("Final full create-save-restart-search workflow", () => InTemp(path =>
        {
            var service = new RentalService(new(path), new NativeFee());
            Equal(2, Value(service.Create(1, 1, Period())).Id);
            var restarted = new RentalService(new(path), new NativeFee());
            Equal(false, Value(restarted.Search(Period())).Standard.Any(c => c.CarId == 1));
            Equal(2, Value(RentalDataLoader.Load(path)).Rentals.Count);
        }));
        check("Final full close-save-restart-availability workflow", () => InTemp(path =>
        {
            var service = new RentalService(new(path), new NativeFee());
            Value(service.Close(1, new(2026, 12, 2), "Cash"));
            var restarted = new RentalService(new(path), new NativeFee());
            Equal(true, Value(restarted.Search(Period("2026-12-02", "2026-12-03"))).Standard.Any(c => c.CarId == 1));
            Equal("Cash", Value(RentalDataLoader.Load(path)).Rentals[0].Closure!.PaymentMethod);
            Error(restarted.Close(1, new(2026, 12, 3), "Card"), "RENTAL_ALREADY_CLOSED");
        }));
        check("Final failures preserve bytes and skip native calls", () => InTemp(path =>
        {
            string before = File.ReadAllText(path); int calls = 0;
            var service = new RentalService(new(path), new NativeFee(_ => { calls++; return 500; }));
            Error(service.Create(999, 1, Period()), "CUSTOMER_NOT_FOUND");
            Error(service.Create(1, 1, Period("2026-12-01", "2026-12-02")), "CAR_UNAVAILABLE");
            Equal(0, calls); Equal(before, File.ReadAllText(path));
            Error(new RentalService(new(path), new NativeFee(_ => throw new DllNotFoundException()))
                .Create(1, 1, Period()), "NATIVE_UNAVAILABLE");
            Equal(before, File.ReadAllText(path));
        }));
        check("Final locked save fails atomically and releases owned resources", () => InTemp(path =>
        {
            string before = File.ReadAllText(path);
            var service = new RentalService(new(path), new NativeFee());
            using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Error(service.Create(1, 1, Period()), "DATA_SAVE_FAILED");
            Equal(before, File.ReadAllText(path));
            Equal(0, Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Length);
            Equal(2, Value(service.Create(1, 1, Period())).Id);
        }));
        check("Final concurrent writer is rejected before changing data", () => InTemp(path =>
        {
            string before = File.ReadAllText(path);
            using (var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                Error(new RentalService(new(path), new NativeFee()).Create(1, 1, Period()), "DATA_SAVE_FAILED");
            Equal(before, File.ReadAllText(path));
            Value(new RentalService(new(path), new NativeFee()).Create(1, 1, Period()));
        }));
        check("Final missing and malformed files are never replaced", () => InTemp(path =>
        {
            string missing = path + ".missing";
            Error(new RentalService(new(missing), new NativeFee()).Search(Period()), "DATA_FILE_MISSING");
            Error(new RentalService(new(missing), new NativeFee()).Create(1, 1, Period()), "DATA_FILE_MISSING");
            Equal(false, File.Exists(missing));
            File.WriteAllText(path, "{bad json");
            Error(new RentalService(new(path), new NativeFee()).Create(1, 1, Period()), "DATA_FILE_MALFORMED");
            Equal("{bad json", File.ReadAllText(path));
        }));
        check("Final stored closures and overlapping active bookings are validated", () => InTemp(path =>
        {
            string original = File.ReadAllText(path);
            foreach (var closure in new[] {
                """{"returnDate":"2026-11-30","paymentMethod":"Cash","totalPaid":10}""",
                """{"returnDate":"2026-12-03","paymentMethod":"unknown","totalPaid":10}""",
                """{"returnDate":"2026-12-03","paymentMethod":"Card","totalPaid":-1}""",
                """{"paymentMethod":"Cash","totalPaid":10}""" })
            {
                var node = JsonNode.Parse(original)!;
                node["rentals"]![0]!["closure"] = JsonNode.Parse(closure);
                File.WriteAllText(path, node.ToJsonString());
                Error(RentalDataLoader.Load(path), "DATA_FILE_MALFORMED");
            }
            var duplicate = JsonNode.Parse(original)!;
            var row = duplicate["rentals"]![0]!.DeepClone(); row["id"] = 2;
            duplicate["rentals"]!.AsArray().Add(row);
            File.WriteAllText(path, duplicate.ToJsonString());
            Error(RentalDataLoader.Load(path), "DATA_FILE_MALFORMED");
        }));
        check("Final menu invalid input returns to menu and accepts next command", () => InTemp(path =>
        {
            string text = Menu(path, "wrong\n2\nabc\n1\n2026-02-30\n3\n-2\n0\n");
            foreach (string code in new[] { "CHOICE_INVALID", "ID_INVALID", "DATE_INVALID", "Goodbye." })
                Contains(text, code);
            Equal(1, Value(RentalDataLoader.Load(path)).Rentals.Count);
            Equal(false, text.Contains("Car ID:"));
        }));
        check("Final menu create, overlap failure, close and next search", () => InTemp(path =>
        {
            string text = Menu(path, "2\n1\n1\n2026-12-10\n2026-12-12\n" +
                "2\n1\n1\n2026-12-10\n2026-12-12\n" +
                "3\n2\n2026-12-11\nCard\n1\n2026-12-11\n2026-12-12\n0\n");
            foreach (string expected in new[] { "Rental created. ID = 2", "CAR_UNAVAILABLE", "Rental closed. ID = 2",
                "Search complete.", "Goodbye." }) Contains(text, expected);
            Equal(2, Value(RentalDataLoader.Load(path)).Rentals.Count);
        }));
        check("Final menu invalid payment leaves rental active", () => InTemp(path =>
        {
            Contains(Menu(path, "3\n1\n2026-12-03\ninvalid\n0\n"), "PAYMENT_INVALID");
            Equal(true, Value(RentalDataLoader.Load(path)).Rentals[0].Closure is null);
        }));
        check("Final end-of-input at every prompt exits without saving", () => InTemp(path =>
        {
            string before = File.ReadAllText(path);
            foreach (string text in new[] { "", "1\n", "1\n2026-12-10\n", "2\n", "2\n1\n", "2\n1\n1\n",
                "2\n1\n1\n2026-12-10\n", "3\n", "3\n1\n", "3\n1\n2026-12-03\n" })
                Contains(Menu(path, text), "Goodbye.");
            Equal(before, File.ReadAllText(path));
        }));
        check("Final zero exits without performing later input", () => InTemp(path =>
        {
            string before = File.ReadAllText(path);
            Contains(Menu(path, "0\n2\n1\n1\n2026-12-10\n2026-12-12\n"), "Goodbye.");
            Equal(before, File.ReadAllText(path));
        }));
        check("Final invalid path is a Result instead of an exception", () =>
            Error(RentalDataLoader.Load("bad\0path"), "DATA_FILE_PATH_INVALID"));
    }

    private static BookingPeriod Period(string start = "2026-12-10", string end = "2026-12-12") =>
        Value(BookingPeriod.Create(DateOnly.Parse(start), DateOnly.Parse(end)));
    private static RentalData Data() => new(
        new[] { new Car(1, "Manual car", CarType.Default, "White", TransmissionType.Manual),
            new Car(2, "Automatic car", CarType.Comfort, "Black", TransmissionType.Automat) },
        new[] { new Customer(1, "Driver", 20, true, TransmissionType.Manual) },
        new[] { new Rental(1, 1, 1, new(2026, 12, 1), new(2026, 12, 3), DailyPrice: 15000m, ServiceFee: 500) });

    private static void InTemp(Action<string> test)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rental-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "rentals.json");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(Data(),
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
        try { test(path); } finally { Directory.Delete(dir, recursive: true); }
    }
    private static string Menu(string path, string commands)
    {
        using var input = new StringReader(commands);
        using var output = new StringWriter();
        new ConsoleApplication(input, output, new(path), new NativeFee()).Run();
        return output.ToString();
    }
    private static T Value<T>(Result<T, RentalError> result) => result switch
    {
        Result<T, RentalError>.Success ok => ok.Value,
        Result<T, RentalError>.Failure error => throw new Exception(error.Error.ToString()),
        _ => throw new Exception("Unknown result")
    };
    private static void Error<T>(Result<T, RentalError> result, string code)
    {
        if (result is not Result<T, RentalError>.Failure failed || failed.Error.Code != code)
            throw new Exception($"Expected {code}, got {result}.");
    }
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected {expected}, got {actual}.");
    }
    private static void Contains(string actual, string expected)
    {
        if (!actual.Contains(expected)) throw new Exception("Missing output: " + expected + "\n" + actual);
    }
}
