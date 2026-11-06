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
    DateOnly EndDate);

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
