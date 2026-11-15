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
        RespectRequiredConstructorParameters = true,
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
                    rental.CustomerId <= 0 || rental.CarId <= 0 ||
                    BookingPeriod.Create(rental.StartDate, rental.EndDate) is Result<BookingPeriod, RentalError>.Failure))
            {
                return Malformed(path, "The rentals array contains an invalid rental record.");
            }

            if (document.Cars.Select(c => c.Id).Distinct().Count() != document.Cars.Count ||
                document.Customers.Select(c => c.Id).Distinct().Count() != document.Customers.Count ||
                document.Rentals.Select(r => r.Id).Distinct().Count() != document.Rentals.Count ||
                document.Customers.Any(c => c.Age < 0) ||
                document.Rentals.Any(r => !document.Cars.Any(c => c.Id == r.CarId) ||
                    !document.Customers.Any(c => c.Id == r.CustomerId)))
                return Malformed(path, "Duplicate IDs, invalid age, or unknown rental reference.");

            if (document.Rentals.Any(r => r.DailyPrice < 0m || r.DailyPrice > 1000000000m ||
                    r.ServiceFee < 0 || r.Closure is { } closed &&
                    (closed.ReturnDate < r.StartDate || closed.TotalPaid < 0m ||
                     ConsoleInput.Payment(closed.PaymentMethod) is Result<string, RentalError>.Failure)))
                return Malformed(path, "Invalid rental price, return date, payment method, or total.");

            if (document.Rentals.Any(r => r.Closure is { } closure &&
                    closure.TotalPaid != RentalOperations.ClosingTotal(r,
                        document.Cars.Single(c => c.Id == r.CarId), closure.ReturnDate)))
                return Malformed(path, "Stored payment total does not match the rental rate, dates and fee.");

            if (document.Rentals.Where(r => r.Closure is null).GroupBy(r => r.CarId).Any(group =>
                group.OrderBy(r => r.StartDate).Zip(group.OrderBy(r => r.StartDate).Skip(1),
                    (left, right) => left.EndDate > right.StartDate).Any(overlap => overlap)))
                return Malformed(path, "Active bookings for the same car overlap.");

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
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return Result<RentalData, RentalError>.Fail(
                new RentalError("DATA_FILE_PATH_INVALID", $"Invalid or inaccessible JSON path: {exception.Message}"));
        }
    }

    private static Result<RentalData, RentalError> Malformed(string path, string details) =>
        Result<RentalData, RentalError>.Fail(
            new RentalError("DATA_FILE_MALFORMED", $"Input JSON file '{path}' is invalid. {details}"));
}
