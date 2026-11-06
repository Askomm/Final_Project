using System.Globalization;

namespace Code;

public sealed class ValidatedRentalRequest
{
    public Customer Customer { get; }
    public RentalPeriod Period { get; }

    internal ValidatedRentalRequest(Customer customer, RentalPeriod period)
    {
        Customer = customer;
        Period = period;
    }
}

public abstract record RentalRequestCreationResult
{
    private RentalRequestCreationResult() { }

    public sealed record Valid(ValidatedRentalRequest Request) : RentalRequestCreationResult;

    public sealed record Invalid(IReadOnlyList<string> Reasons) : RentalRequestCreationResult;
}

public static class RentalRequestFactory
{
    public static RentalRequestCreationResult Create(
        IEnumerable<Customer> customers,
        int customerId,
        string? startText,
        string? endText)
    {
        List<string> errors = new();
        Customer? customer = null;

        if (customerId <= 0)
        {
            errors.Add("Customer ID must be a positive number.");
        }
        else
        {
            customer = customers.FirstOrDefault(item => item.Id == customerId);
            if (customer is null)
            {
                errors.Add("Customer was not found.");
            }
            else
            {
                if (customer.Age < 18)
                    errors.Add("Customer must be at least 18 years old.");
                if (!customer.HasDrivingLicense)
                    errors.Add("Customer must have a driving license.");
            }
        }

        bool startIsValid = DateOnly.TryParseExact(
            startText, "dd-MM-yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateOnly startDate);
        bool endIsValid = DateOnly.TryParseExact(
            endText, "dd-MM-yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateOnly endDate);

        if (!startIsValid || !endIsValid)
        {
            errors.Add("Both dates are required and must use dd-MM-yyyy.");
        }
        else if (endDate <= startDate)
        {
            errors.Add("End date must be later than start date.");
        }

        if (errors.Count > 0)
            return new RentalRequestCreationResult.Invalid(Array.AsReadOnly(errors.ToArray()));

        if (customer is null || !startIsValid || !endIsValid)
        {
            return new RentalRequestCreationResult.Invalid(
                Array.AsReadOnly(new[] { "Rental request could not be validated." }));
        }

        RentalPeriod period = new(startDate, endDate);
        return new RentalRequestCreationResult.Valid(new ValidatedRentalRequest(customer, period));
    }
}
