using System.Text.Json;
using System.Text.Json.Serialization;

namespace Code;

public sealed record RentalData(
    IReadOnlyList<Car> Cars,
    IReadOnlyList<Customer> Customers,
    IReadOnlyList<Rental> Rentals);

public sealed record RentalError(string Code, string Message);

public sealed record RentalDataDocument(
    List<Car>? Cars,
    List<Customer>? Customers,
    List<Rental>? Rentals);

public static class RentalDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Result<RentalData, RentalError> Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result<RentalData, RentalError>.Fail(
                new RentalError("DATA_FILE_PATH_INVALID", "The input JSON path is empty."));
        }

        try
        {
            
            string json = File.ReadAllText(path);
            RentalDataDocument? document = JsonSerializer.Deserialize<RentalDataDocument>(json, JsonOptions);

            if (document is null || document.Cars is null ||
                document.Customers is null || document.Rentals is null)
            {
                return Malformed(path,
                    "The JSON must contain non-null 'cars', 'customers', and 'rentals' arrays.");
            }

            if (document.Cars.Any(car => car is null || car.Id <= 0 ||
                    string.IsNullOrWhiteSpace(car.Name) || string.IsNullOrWhiteSpace(car.Color) ||
                    !Enum.IsDefined(typeof(CarType), car.Type) ||
                    !Enum.IsDefined(typeof(TransmissionType), car.Transmission)))
            {
                return Malformed(path, "The cars array contains an invalid car record.");
            }

            if (document.Customers.Any(customer => customer is null || customer.Id <= 0 ||
                    string.IsNullOrWhiteSpace(customer.Name) ||
                    !Enum.IsDefined(typeof(TransmissionType), customer.PreferredTransmission)))
            {
                return Malformed(path, "The customers array contains an invalid customer record.");
            }

            if (document.Rentals.Any(rental => rental is null || rental.Id <= 0 ||
                    rental.CustomerId <= 0 || rental.CarId <= 0 || rental.EndDate <= rental.StartDate))
            {
                return Malformed(path, "The rentals array contains an invalid rental record.");
            }

            RentalData data = new(
                Array.AsReadOnly(document.Cars.ToArray()),
                Array.AsReadOnly(document.Customers.ToArray()),
                Array.AsReadOnly(document.Rentals.ToArray()));

            return Result<RentalData, RentalError>.Ok(data);
        }
        catch (FileNotFoundException)
        {
            return Result<RentalData, RentalError>.Fail(
                new RentalError("DATA_FILE_MISSING", $"Input JSON file was not found: '{path}'."));
        }
        catch (DirectoryNotFoundException)
        {
            return Result<RentalData, RentalError>.Fail(
                new RentalError("DATA_FILE_MISSING", $"Input directory for JSON file was not found: '{path}'."));
        }
        catch (JsonException exception)
        {
            return Malformed(path, $"JSON parsing failed: {exception.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            return Result<RentalData, RentalError>.Fail(
                new RentalError("DATA_FILE_ACCESS_DENIED", $"No permission to read input JSON file: '{path}'."));
        }
        catch (IOException exception)
        {
            return Result<RentalData, RentalError>.Fail(
                new RentalError("DATA_FILE_READ_FAILED", $"Could not read '{path}': {exception.Message}"));
        }
    }

    private static Result<RentalData, RentalError> Malformed(string path, string details) =>
        Result<RentalData, RentalError>.Fail(
            new RentalError("DATA_FILE_MALFORMED", $"Input JSON file '{path}' is invalid. {details}"));
}
