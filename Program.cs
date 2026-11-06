using System.Text.Json;
using Code;

Console.WriteLine("Car Rental System - Assignment 5");
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

Customer? customer = customers.FirstOrDefault(item => item.Id == customerId);
if (customer is null)
{
    Console.WriteLine("Customer was not found.");
    return;
}

Console.Write("Start date (dd-MM-yyyy): ");
string? startText = Console.ReadLine();
Console.Write("End date (dd-MM-yyyy): ");
string? endText = Console.ReadLine();

if (!TryParse.TryParseRentalPeriod(startText, endText, out RentalPeriod? period, out string dateError) || period is null)
{
    Console.WriteLine(dateError);
    return;
}

RentalSearchResult standard = Pipeline.Run(
    cars, rentals, customer, period, "Standard", PricingPolicies.Standard);
RentalSearchResult discounted = Pipeline.Run(
    cars, rentals, customer, period, "10% discount", PricingPolicies.Discounted);

PrintResult(standard);
PrintResult(discounted);

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
