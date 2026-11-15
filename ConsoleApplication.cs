namespace Code;

public sealed class ConsoleApplication(TextReader input, TextWriter output,
    RentalStore store, NativeFee native)
{
    private readonly RentalService service = new(store, native);
    private bool endOfInput;

    public void Run()
    {
        output.WriteLine("Car Rental System - Final Project");
        output.WriteLine("Dates: yyyy-MM-dd. Prices: KZT. Data: " + store.PathName);
        ShowReferenceData();
        while (!endOfInput)
        {
            output.WriteLine("\n1. Search cars\n2. Create rental\n3. Close rental\n4. Search within budget\n0. Exit");
            string? choice = Read("Choice: ");
            if (choice is null || choice == "0") break;
            Result<string, RentalError> outcome = choice switch
            {
                "1" => ReadPeriod().Bind(service.Search).Map(PrintReports),
                "4" => ReadPeriod().Bind(period => ConsoleInput.Budget(Read("Maximum total budget (KZT): "))
                    .Bind(budget => service.Search(period, budget))).Map(PrintReports),
                "2" => ReadId("Customer ID").Bind(customer =>
                    ReadId("Car ID").Bind(car => ReadPeriod()
                        .Bind(period => service.Create(customer, car, period))))
                    .Map(r => $"Rental created. ID = {r.Id}. Saved to JSON. " +
                        $"Booked total: {r.DailyPrice * (r.EndDate.DayNumber - r.StartDate.DayNumber) + r.ServiceFee:0.00} KZT."),
                "3" => ReadId("Rental ID").Bind(id => ReadDate("Return date").Bind(returned =>
                    ReadPayment().Bind(payment => service.CloseDetailed(id, returned, payment))))
                    .Map(receipt => $"Rental closed. ID = {receipt.Rental.Id}. Paid: {receipt.Rental.Closure!.TotalPaid:0.00} KZT " +
                        $"by {receipt.Rental.Closure.PaymentMethod}. Saved to JSON." +
                        (receipt.ConflictingRentalIds.Count == 0 ? "" :
                            "\nWarning: late return conflicts with reservation IDs: " +
                            string.Join(", ", receipt.ConflictingRentalIds) + ". Staff must resolve these reservations.")),
                _ => Result<string, RentalError>.Fail(new("CHOICE_INVALID", "Choose 0, 1, 2, 3, or 4."))
            };
            switch (outcome)
            {
                case Result<string, RentalError>.Success ok: output.WriteLine(ok.Value); break;
                case Result<string, RentalError>.Failure failed:
                    if (!endOfInput) output.WriteLine($"Error [{failed.Error.Code}]: {failed.Error.Message}");
                    break;
            }
        }
        output.WriteLine("Goodbye.");
    }

    private string? Read(string prompt)
    {
        output.Write(prompt);
        string? value = input.ReadLine();
        if (value is null) endOfInput = true;
        return value?.Trim();
    }
    private Result<int, RentalError> ReadId(string label) => ConsoleInput.PositiveId(Read(label + ": "), label);
    private Result<DateOnly, RentalError> ReadDate(string label) => ConsoleInput.Date(Read(label + ": "), label);
    private Result<string, RentalError> ReadPayment() => ConsoleInput.Payment(Read("Payment method (Cash/Card/Transfer): "));
    private Result<BookingPeriod, RentalError> ReadPeriod() =>
        ReadDate("Start date").Bind(start => ReadDate("End date")
            .Bind(end => BookingPeriod.Create(start, end)));

    private string PrintReports(ReportBundle reports)
    {
        output.WriteLine("\nAvailable cars - standard booking price:");
        PrintOffers(reports.Standard);
        output.WriteLine("\nPolicy comparison - 10% discount (quote only):");
        PrintOffers(reports.Discounted);
        output.WriteLine("\nFleet daily revenue potential (all cars, before service fees):");
        if (reports.Fleet.Count == 0) output.WriteLine("Fleet is empty.");
        foreach (var row in reports.Fleet)
            output.WriteLine($"{row.Type}: {row.Cars} cars; {row.DailyPotential:0.00} KZT/day");
        return "Search complete. Choose 2 to book at the standard price.";
    }
    private void PrintOffers(IReadOnlyList<Offer> offers)
    {
        if (offers.Count == 0) output.WriteLine("No cars match this period and budget.");
        foreach (var offer in offers)
            output.WriteLine($"{offer.CarId}: {offer.CarName}; {offer.DailyPrice:0.00} KZT/day x {offer.Days}" +
                $" + {offer.ServiceFee} service fee = {offer.TotalPrice:0.00} KZT");
    }
    private void ShowReferenceData()
    {
        switch (store.Load())
        {
            case Result<RentalData, RentalError>.Success ok:
                output.WriteLine("Customers: " + string.Join(", ", ok.Value.Customers.Select(c => $"{c.Id} = {c.Name}")));
                output.WriteLine("Cars: " + string.Join(", ", ok.Value.Cars.Select(c => $"{c.Id} = {c.Name}")));
                output.WriteLine("Active rental IDs: " + string.Join(", ",
                    ok.Value.Rentals.Where(r => r.Closure is null).Select(r => r.Id)));
                break;
            case Result<RentalData, RentalError>.Failure failed:
                output.WriteLine($"Error [{failed.Error.Code}]: {failed.Error.Message}");
                output.WriteLine("Correct the data file, then retry; no data will be replaced automatically.");
                break;
        }
    }
}
