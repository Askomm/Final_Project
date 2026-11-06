using System.Globalization;

namespace Code;

public static class TryParse
{
    public static bool TryParsePositiveId(string? text, out int id, out string error)
    {
        if (!int.TryParse(text, out id) || id <= 0)
        {
            id = 0;
            error = "ID must be a positive number.";
            return false;
        }

        error = "";
        return true;
    }

    public static bool TryParseCar(string? line, out Car? car, out string error)
    {
        car = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            error = "Car row is empty.";
            return false;
        }

        string[] fields = line.Split('|', StringSplitOptions.TrimEntries);
        if (fields.Length != 5)
        {
            error = "A car must have exactly 5 fields.";
            return false;
        }

        if (!TryParsePositiveId(fields[0], out int id, out error))
            return false;
        if (string.IsNullOrWhiteSpace(fields[1]) || string.IsNullOrWhiteSpace(fields[3]))
        {
            error = "Car name and color are required.";
            return false;
        }
        if (!TryCarType(fields[2], out CarType type))
        {
            error = "Type must be Default, Comfort or Luxury.";
            return false;
        }
        if (!TryTransmission(fields[4], out TransmissionType transmission))
        {
            error = "Transmission must be Manual or Automat.";
            return false;
        }

        car = new Car(id, fields[1], type, fields[3], transmission);
        error = "";
        return true;
    }

    public static bool TryParseCustomer(string? line, out Customer? customer, out string error)
    {
        customer = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            error = "Customer row is empty.";
            return false;
        }

        string[] fields = line.Split('|', StringSplitOptions.TrimEntries);
        if (fields.Length != 5)
        {
            error = "A customer must have exactly 5 fields.";
            return false;
        }

        if (!TryParsePositiveId(fields[0], out int id, out error))
            return false;
        if (string.IsNullOrWhiteSpace(fields[1]))
        {
            error = "Customer name is required.";
            return false;
        }
        if (!int.TryParse(fields[2], out int age) || age < 18)
        {
            error = "Customer age must be at least 18.";
            return false;
        }
        if (!bool.TryParse(fields[3], out bool hasLicense))
        {
            error = "Driving license must be True or False.";
            return false;
        }
        if (!TryTransmission(fields[4], out TransmissionType transmission))
        {
            error = "Transmission must be Manual or Automat.";
            return false;
        }

        customer = new Customer(id, fields[1], age, hasLicense, transmission);
        error = "";
        return true;
    }

    public static bool TryParseRental(string? line, out Rental? rental, out string error)
    {
        rental = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            error = "Rental row is empty.";
            return false;
        }

        string[] fields = line.Split('|', StringSplitOptions.TrimEntries);
        if (fields.Length != 5)
        {
            error = "A rental must have exactly 5 fields.";
            return false;
        }

        if (!TryParsePositiveId(fields[0], out int id, out error) ||
            !TryParsePositiveId(fields[1], out int customerId, out error) ||
            !TryParsePositiveId(fields[2], out int carId, out error))
            return false;

        if (!DateOnly.TryParseExact(fields[3], "dd-MM-yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateOnly startDate) ||
            !DateOnly.TryParseExact(fields[4], "dd-MM-yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateOnly endDate))
        {
            error = "Rental dates must use dd-MM-yyyy.";
            return false;
        }
        if (endDate <= startDate)
        {
            error = "Rental end date must be later than start date.";
            return false;
        }

        rental = new Rental(id, customerId, carId, startDate, endDate);
        error = "";
        return true;
    }

    public static bool TryParseRentalPeriod(
        string? startText,
        string? endText,
        out RentalPeriod? period,
        out string error)
    {
        period = null;
        if (string.IsNullOrWhiteSpace(startText) || string.IsNullOrWhiteSpace(endText))
        {
            error = "Both start and end dates are required.";
            return false;
        }

        if (!DateOnly.TryParseExact(startText, "dd-MM-yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateOnly startDate) ||
            !DateOnly.TryParseExact(endText, "dd-MM-yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateOnly endDate))
        {
            error = "Dates must use dd-MM-yyyy.";
            return false;
        }
        if (endDate <= startDate)
        {
            error = "End date must be later than start date.";
            return false;
        }

        period = new RentalPeriod(startDate, endDate);
        error = "";
        return true;
    }

    public static List<Car> ParseCars(IEnumerable<string?> lines, out List<string> errors)
    {
        List<Car> cars = new();
        errors = new List<string>();
        int lineNumber = 0;

        foreach (string? line in lines)
        {
            lineNumber++;
            if (TryParseCar(line, out Car? car, out string error) && car is not null)
                cars.Add(car);
            else
                errors.Add($"Cars line {lineNumber}: {error}");
        }

        return cars;
    }

    public static List<Customer> ParseCustomers(IEnumerable<string?> lines, out List<string> errors)
    {
        List<Customer> customers = new();
        errors = new List<string>();
        int lineNumber = 0;

        foreach (string? line in lines)
        {
            lineNumber++;
            if (TryParseCustomer(line, out Customer? customer, out string error) && customer is not null)
                customers.Add(customer);
            else
                errors.Add($"Customers line {lineNumber}: {error}");
        }

        return customers;
    }

    public static List<Rental> ParseRentals(IEnumerable<string?> lines, out List<string> errors)
    {
        List<Rental> rentals = new();
        errors = new List<string>();
        int lineNumber = 0;

        foreach (string? line in lines)
        {
            lineNumber++;
            if (TryParseRental(line, out Rental? rental, out string error) && rental is not null)
                rentals.Add(rental);
            else
                errors.Add($"Rentals line {lineNumber}: {error}");
        }

        return rentals;
    }

    private static bool TryCarType(string text, out CarType type)
    {
        switch (text)
        {
            case "Default": type = CarType.Default; return true;
            case "Comfort": type = CarType.Comfort; return true;
            case "Luxury": type = CarType.Luxury; return true;
            default: type = default; return false;
        }
    }

    private static bool TryTransmission(string text, out TransmissionType transmission)
    {
        switch (text)
        {
            case "Manual": transmission = TransmissionType.Manual; return true;
            case "Automat": transmission = TransmissionType.Automat; return true;
            default: transmission = default; return false;
        }
    }
}
