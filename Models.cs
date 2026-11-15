namespace Code;

public sealed record Car(
    int Id,
    string Name,
    CarType Type,
    string Color,
    TransmissionType Transmission);

public sealed record Customer(
    int Id,
    string Name,
    int Age,
    bool HasDrivingLicense,
    TransmissionType PreferredTransmission);

public sealed record Rental(
    int Id,
    int CustomerId,
    int CarId,
    DateOnly StartDate,
    DateOnly EndDate,
    RentalClosure? Closure = null,
    decimal DailyPrice = 0m,
    int ServiceFee = 0);

public sealed record RentalClosure(DateOnly ReturnDate, string PaymentMethod, decimal TotalPaid);

public sealed record RentalPeriod(DateOnly StartDate, DateOnly EndDate)
{
    public int Days => EndDate.DayNumber - StartDate.DayNumber;
}

public sealed record CarQuote(
    int CarId,
    string CarName,
    decimal DailyPrice,
    int Days,
    decimal TotalPrice);

public sealed record RentalSearchResult(
    int CustomerId,
    DateOnly StartDate,
    DateOnly EndDate,
    string PolicyName,
    IReadOnlyList<CarQuote> Quotes);
