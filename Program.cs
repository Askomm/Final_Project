using Code;

Console.WriteLine("Car Rental System - Assignment 8");
string folder = Path.Combine(AppContext.BaseDirectory, "Data");
string file = args.Contains("--demo-missing-file") ? "missing.json" :
    args.Contains("--demo-malformed-file") ? "malformed.json" : "rental-data.json";
var loaded = RentalDataLoader.Load(Path.Combine(folder, file));
if (loaded is Result<RentalData, RentalError>.Failure loadError)
{ Fail(loadError.Error); return; }
var data = ((Result<RentalData, RentalError>.Success)loaded).Value;
Console.Write("Customer ID: ");
string customer = Console.ReadLine() ?? "";
Console.Write("Start (dd-MM-yyyy): ");
string start = Console.ReadLine() ?? "";
Console.Write("End (dd-MM-yyyy): ");
string end = Console.ReadLine() ?? "";
var standard = ReportQueries.CreatePrice(0m);
var discounted = ReportQueries.CreatePrice(0.10m);
var native = args.Contains("--demo-native-failure")
    ? new NativeFee(_ => throw new DllNotFoundException("Controlled failure demonstration."))
    : new NativeFee();
var outcome = ReportWorkflow.Run(data, customer, start, end, 60000m, standard, discounted, native);
if (outcome is Result<ReportBundle, RentalError>.Failure error)
{ Fail(error.Error); return; }
var reports = ((Result<ReportBundle, RentalError>.Success)outcome).Value;
Console.WriteLine($"Customer {customer}; {start} to {end}; budget 60,000 KZT.");
PrintOffers("Affordable rental offers - standard", reports.Standard);
PrintOffers("Affordable rental offers - 10% discount", reports.Discounted);
Console.WriteLine("\nFleet daily revenue potential (all cars, before service fees):");
foreach (var row in reports.Fleet)
    Console.WriteLine($"{row.Type}: {row.Cars} cars; {row.DailyPotential:0.00} KZT/day");
var saved = ReportExporter.Save(Path.Combine(Environment.CurrentDirectory, "rental-reports.json"), reports);
if (saved is Result<string, RentalError>.Success success) Console.WriteLine("\nSaved " + success.Value);
else Fail(((Result<string, RentalError>.Failure)saved).Error);

static void Fail(RentalError error)
{ Console.WriteLine($"Operation failed [{error.Code}]: {error.Message}"); Environment.ExitCode = 1; }
static void PrintOffers(string title, IReadOnlyList<Offer> offers)
{
    Console.WriteLine("\n" + title);
    if (offers.Count == 0) Console.WriteLine("No offers within the budget.");
    foreach (var offer in offers)
        Console.WriteLine($"{offer.CarId:D3} {offer.CarName}: {offer.DailyPrice:0.00} KZT/day x {offer.Days}" +
            $" + {offer.ServiceFee} KZT native service fee = {offer.TotalPrice:0.00} KZT");
}
