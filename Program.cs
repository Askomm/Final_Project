using Code;

// Stable writable path: a rebuild must not reset live rentals copied into bin.
string path = Path.Combine(Environment.CurrentDirectory, "Data", "rental-data.json");
bool demoNative = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--data" && i + 1 < args.Length) path = args[++i];
    else if (args[i] == "--demo-native-failure") demoNative = true;
    else if (args[i] is "--demo-missing-file" or "--demo-malformed-file")
    {
        string file = args[i] == "--demo-missing-file" ? "missing.json" : "malformed.json";
        var result = RentalDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "Data", file));
        if (result is Result<RentalData, RentalError>.Failure failed)
            Console.WriteLine($"Error [{failed.Error.Code}]: {failed.Error.Message}");
        Environment.ExitCode = 1;
        return;
    }
    else
    {
        Console.WriteLine("Usage: dotnet run --project Code.csproj -- [--data path.json] [--demo-native-failure]");
        Environment.ExitCode = 1;
        return;
    }
}
var native = demoNative
    ? new NativeFee(_ => throw new DllNotFoundException("Controlled failure demonstration."))
    : new NativeFee();
try
{
    new ConsoleApplication(Console.In, Console.Out, new RentalStore(path), native).Run();
}
catch (IOException e)
{
    Console.Error.WriteLine("Console input/output is unavailable: " + e.Message);
    Environment.ExitCode = 1;
}
