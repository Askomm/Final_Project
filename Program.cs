using System.Text.Json;
using Code;

Console.WriteLine("Car Rental System - Assignment 6");
Console.WriteLine("Find available cars and compare two prices.\n");

string dataFolder = Path.Combine(AppContext.BaseDirectory, "Data");
string carsFile = Path.Combine(dataFolder, "cars.txt");
string customersFile = Path.Combine(dataFolder, "customers.txt");
string rentalsFile = Path.Combine(dataFolder, "rentals.txt");

if (!File.Exists(carsFile) || !File.Exists(customersFile) || !File.Exists(rentalsFile))
{
    Console.WriteLine("Input data file is missing. Check the Data folder.");
    return;
}

List<Car> cars = TryParse.ParseCars(File.ReadAllLines(carsFile), out List<string> carErrors);
List<Customer> customers = TryParse.ParseCustomers(File.ReadAllLines(customersFile), out List<string> customerErrors);
List<Rental> rentals = TryParse.ParseRentals(File.ReadAllLines(rentalsFile), out List<string> rentalErrors);

PrintErrors(carErrors);
PrintErrors(customerErrors);
PrintErrors(rentalErrors);
Console.WriteLine($"Loaded {cars.Count} cars, {customers.Count} customers and {rentals.Count} rentals.\n");

Console.Write("Customer ID: ");
if (!TryParse.TryParsePositiveId(Console.ReadLine(), out int customerId, out string idError))
{
    Console.WriteLine(idError);
    return;
}

Console.Write("Start date (dd-MM-yyyy): ");
string? startText = Console.ReadLine();
Console.Write("End date (dd-MM-yyyy): ");
string? endText = Console.ReadLine();

RentalRequestCreationResult creation = RentalRequestFactory.Create(
    customers, customerId, startText, endText);

RentalSearchResult standard;
RentalSearchResult discounted;

switch (creation)
{
    case RentalRequestCreationResult.Valid valid:
    {       
        standard = Pipeline.RunValidated(
            cars, rentals, valid.Request, "Standard", PricingPolicies.Standard);
        discounted = Pipeline.RunValidated(
            cars, rentals, valid.Request, "10% discount", PricingPolicies.Discounted);

        PrintResult(standard);
        PrintResult(discounted);
        break;
    }
    case RentalRequestCreationResult.Invalid invalid:
        foreach (string reason in invalid.Reasons)
            Console.WriteLine($"Rental request rejected: {reason}");
        return;
    default:
        throw new InvalidOperationException("Unknown rental request result.");
}

string outputFile = Path.Combine(Environment.CurrentDirectory, "rental-quotes.json");
try
{
    string json = JsonSerializer.Serialize(
        new { Standard = standard, Discounted = discounted },
        new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(outputFile, json);
    Console.WriteLine($"\nResults saved to {outputFile}");
}
catch (IOException)
{
    Console.WriteLine("Could not write rental-quotes.json.");
}
catch (UnauthorizedAccessException)
{
    Console.WriteLine("No permission to write rental-quotes.json.");
}

static void PrintErrors(List<string> errors)
{
    foreach (string error in errors)
        Console.WriteLine($"Rejected input: {error}");
}

static void PrintResult(RentalSearchResult result)
{
    Console.WriteLine($"\n{result.PolicyName} quotes:");
    if (result.Quotes.Count == 0)
    {
        Console.WriteLine("No cars match the request.");
        return;
    }

    foreach (CarQuote quote in result.Quotes)
    {
        Console.WriteLine(
            $"Car {quote.CarId:D3} - {quote.CarName}: {quote.DailyPrice:N0} KZT/day, " +
            $"total {quote.TotalPrice:N0} KZT for {quote.Days} days");
    }
}
