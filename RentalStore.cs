using System.Text.Json;
using System.Text.Json.Serialization;

namespace Code;

public sealed class RentalStore(string path)
{
    public string PathName => path;
    public Result<RentalData, RentalError> Load() => RentalDataLoader.Load(path);

    public Result<Rental, RentalError> Update(Func<RentalData, Result<RentalChange, RentalError>> change)
    {
        try
        {
            using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            return Load().Bind(change).Bind(candidate =>
                Save(candidate.Data).Map(_ => candidate.Rental));
        }
        catch (Exception e) when (IsFileFailure(e))
        {
            return Result<Rental, RentalError>.Fail(new("DATA_SAVE_FAILED",
                "Cannot update rentals; the file may be busy, inaccessible, or the path invalid. " + e.Message));
        }
    }

    private Result<string, RentalError> Save(RentalData data)
    {
        string? temporary = null;
        try
        {
            string destination = Path.GetFullPath(path);
            temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, data, options);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: true);
            return Result<string, RentalError>.Ok(destination);
        }
        catch (Exception e) when (IsFileFailure(e) || e is JsonException)
        {
            return Result<string, RentalError>.Fail(new("DATA_SAVE_FAILED",
                "Rentals were not saved; the previous file is preserved. " + e.Message));
        }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (Exception e) when (IsFileFailure(e)) { /* The original remains intact. */ }
            }
        }
    }

    internal static bool IsFileFailure(Exception e) =>
        e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException
            or System.Security.SecurityException;
}

public sealed class RentalService(RentalStore store, NativeFee native)
{
    public Result<ReportBundle, RentalError> Search(BookingPeriod period) =>
        store.Load().Bind(data => native.Calculate(period.Days)
            .Map(fee => RentalOperations.Search(data, period, fee)));

    public Result<Rental, RentalError> Create(int customerId, int carId, BookingPeriod period) =>
        store.Update(data => RentalOperations.ValidateBooking(data, customerId, carId, period)
            .Bind(_ => native.Calculate(period.Days))
            .Bind(fee => RentalOperations.Create(data, customerId, carId, period, fee)));

    public Result<Rental, RentalError> Close(int id, DateOnly returned, string payment) =>
        store.Update(data => RentalOperations.Close(data, id, returned, payment));
}
