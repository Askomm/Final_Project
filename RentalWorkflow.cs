namespace Code;

public sealed record RentalQuoteComparison(
    RentalSearchResult Standard,
    RentalSearchResult Discounted);

public static class RentalWorkflow
{
    public static Result<RentalQuoteComparison, RentalError> SearchQuotes(
        RentalData data,
        string? customerIdText,
        string? startText,
        string? endText)
    {
        if (data is null)
        {
            return Result<RentalQuoteComparison, RentalError>.Fail(
                new RentalError("DATASET_MISSING", "Rental data has not been loaded."));
        }

        return ParsePositiveCustomerId(customerIdText)
            .Bind(id => FindCustomer(data.Customers, id).ToResult(
                new RentalError("CUSTOMER_NOT_FOUND", $"Customer {id} was not found.")))
            .Bind(customer => CreateValidatedRequest(customer, startText, endText))
            .Bind(request => FindAvailableCars(data.Cars, data.Rentals, request)
                .Map(availableCars => CreateComparison(request, availableCars)));
    }

    public static Option<Customer> FindCustomer(IEnumerable<Customer> customers, int customerId)
    {
        ArgumentNullException.ThrowIfNull(customers);
        Customer? customer = customers.FirstOrDefault(item => item.Id == customerId);
        return Option<Customer>.FromNullable(customer);
    }

    private static Result<int, RentalError> ParsePositiveCustomerId(string? text)
    {
        if (!int.TryParse(text, out int customerId) || customerId <= 0)
        {
            return Result<int, RentalError>.Fail(
                new RentalError("CUSTOMER_ID_INVALID", "Customer ID must be a positive integer."));
        }

        return Result<int, RentalError>.Ok(customerId);
    }

    private static Result<ValidatedRentalRequest, RentalError> CreateValidatedRequest(
        Customer customer,
        string? startText,
        string? endText)
    {
        RentalRequestCreationResult creation = RentalRequestFactory.CreateForCustomer(
            customer, startText, endText);

        return creation switch
        {
            RentalRequestCreationResult.Valid valid =>
                Result<ValidatedRentalRequest, RentalError>.Ok(valid.Request),
            RentalRequestCreationResult.Invalid invalid =>
                Result<ValidatedRentalRequest, RentalError>.Fail(ConvertValidationError(invalid.Reasons)),
            _ => Result<ValidatedRentalRequest, RentalError>.Fail(
                new RentalError("REQUEST_VALIDATION_FAILED", "The rental request could not be validated."))
        };
    }

    private static RentalError ConvertValidationError(IReadOnlyList<string> reasons)
    {
        string message = string.Join("; ", reasons);

        if (reasons.Any(reason => reason.Contains("Customer must", StringComparison.OrdinalIgnoreCase)))
            return new RentalError("CUSTOMER_NOT_ELIGIBLE", message);
        if (reasons.Any(reason => reason.Contains("End date must be later", StringComparison.OrdinalIgnoreCase)))
            return new RentalError("RENTAL_PERIOD_INVALID", message);
        if (reasons.Any(reason => reason.Contains("Both dates", StringComparison.OrdinalIgnoreCase)))
            return new RentalError("RENTAL_DATES_REQUIRED_OR_FORMAT", message);

        return new RentalError("REQUEST_VALIDATION_FAILED", message);
    }

    private static Result<List<Car>, RentalError> FindAvailableCars(
        IEnumerable<Car> cars,
        IEnumerable<Rental> rentals,
        ValidatedRentalRequest request)
    {
        List<Car> available = Pipeline.SelectAvailableCars(
            cars, rentals, request.Customer, request.Period);

        if (available.Count == 0)
        {
            return Result<List<Car>, RentalError>.Fail(
                new RentalError("NO_CARS_AVAILABLE", "No cars match the request for the selected dates."));
        }

        return Result<List<Car>, RentalError>.Ok(available);
    }

    private static RentalQuoteComparison CreateComparison(
        ValidatedRentalRequest request,
        IEnumerable<Car> availableCars)
    {
        List<CarQuote> standardQuotes = Pipeline.CalculateQuotes(
            availableCars, request.Period, PricingPolicies.Standard);
        List<CarQuote> discountedQuotes = Pipeline.CalculateQuotes(
            availableCars, request.Period, PricingPolicies.Discounted);

        RentalSearchResult standard = Pipeline.CreateSummary(
            request.Customer.Id, request.Period, "Standard", standardQuotes);
        RentalSearchResult discounted = Pipeline.CreateSummary(
            request.Customer.Id, request.Period, "10% discount", discountedQuotes);

        return new RentalQuoteComparison(standard, discounted);
    }
}
