using System.Text.Json;
using Code;

Console.WriteLine("Car Rental System - Assignment 7");
Console.WriteLine("Result, Option, Map, Bind, and safe JSON loading\n");

string dataFolder = Path.Combine(AppContext.BaseDirectory, "Data");
string dataFile = args.FirstOrDefault() switch
{
    "--demo-missing-file" => Path.Combine(dataFolder, "file-that-does-not-exist.json"),
    "--demo-malformed-file" => Path.Combine(dataFolder, "malformed.json"),
    _ => Path.Combine(dataFolder, "rental-data.json")
};

Result<RentalData, RentalError> loadResult = RentalDataLoader.Load(dataFile);
RentalData data;
switch (loadResult)
{
    case Result<RentalData, RentalError>.Success success:
        data = success.Value;
        Console.WriteLine($"Loaded {data.Cars.Count} cars, {data.Customers.Count} customers, " +
                          $"and {data.Rentals.Count} existing rentals from JSON.\n");
        break;

    case Result<RentalData, RentalError>.Failure failure:
        PrintError(failure.Error);
        Environment.ExitCode = 1;
        return;

    default:
        Console.WriteLine("Unexpected data-loading result.");
        Environment.ExitCode = 1;
        return;
}

Console.WriteLine("For the file-boundary demonstrations, run with --demo-missing-file or --demo-malformed-file.");
Console.Write("Customer ID: ");
string? customerIdText = Console.ReadLine();
Console.Write("Start date (dd-MM-yyyy): ");
string? startText = Console.ReadLine();
Console.Write("End date (dd-MM-yyyy): ");
string? endText = Console.ReadLine();

Result<RentalQuoteComparison, RentalError> workflowResult =
    RentalWorkflow.SearchQuotes(data, customerIdText, startText, endText);

switch (workflowResult)
{
    case Result<RentalQuoteComparison, RentalError>.Success success:
        PrintResult(success.Value.Standard);
        PrintResult(success.Value.Discounted);
        SaveResults(success.Value);
        break;

    case Result<RentalQuoteComparison, RentalError>.Failure failure:
        PrintError(failure.Error);
        Environment.ExitCode = 1;
        break;

    default:
        Console.WriteLine("Unexpected workflow result.");
        Environment.ExitCode = 1;
        break;
}

static void PrintError(RentalError error) =>
    Console.WriteLine($"Operation failed [{error.Code}]: {error.Message}");

static void PrintResult(RentalSearchResult result)
{
    Console.WriteLine($"\n{result.PolicyName} quotes:");
    foreach (CarQuote quote in result.Quotes)
    {
        Console.WriteLine(
            $"Car {quote.CarId:D3} - {quote.CarName}: {quote.DailyPrice:N0} KZT/day, " +
            $"total {quote.TotalPrice:N0} KZT for {quote.Days} days");
    }
}

static void SaveResults(RentalQuoteComparison comparison)
{
    string outputFile = Path.Combine(Environment.CurrentDirectory, "rental-quotes.json");
    string temporaryFile = outputFile + ".tmp";

    try
    {
        string json = JsonSerializer.Serialize(
            new { comparison.Standard, comparison.Discounted },
            new JsonSerializerOptions { WriteIndented = true });

        
        File.WriteAllText(temporaryFile, json);
        File.Move(temporaryFile, outputFile, overwrite: true);
        Console.WriteLine($"\nResults saved to {outputFile}");
    }
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine($"Could not save results: no permission to write '{outputFile}'.");
    }
    catch (IOException exception)
    {
        Console.WriteLine($"Could not save results to '{outputFile}': {exception.Message}");
    }
}
