using System.Runtime.InteropServices;
namespace Code;

public sealed class NativeFee
{
    [DllImport("rental_native", EntryPoint = "rental_service_fee", ExactSpelling = true,
        CallingConvention = CallingConvention.Cdecl)]
    private static extern int ServiceFee(int days);

    private readonly Func<int, int> call;
    public NativeFee() : this(ServiceFee) { }
    // Injection permits controlled loader failures without depending on DLL caching.
    public NativeFee(Func<int, int> call) => this.call = call;
    public Result<int, RentalError> Calculate(int days)
    {
        if (days < 1 || days > 366)
            return Result<int, RentalError>.Fail(new("FEE_DAYS_INVALID", "Service fee supports 1..366 days."));
        try
        {
            int fee = call(days);
            return fee < 0
                ? Result<int, RentalError>.Fail(new("NATIVE_REJECTED", "Native fee rejected the input."))
                : Result<int, RentalError>.Ok(fee);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return Result<int, RentalError>.Fail(new("NATIVE_UNAVAILABLE",
                "Cannot load the Windows x64 rental_native library: " + e.Message));
        }
    }
}
