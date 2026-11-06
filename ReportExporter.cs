using System.Text.Json;
namespace Code;

public static class ReportExporter
{
    public static Result<string, RentalError> Save(string path, ReportBundle reports)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            // Serialize before opening a file; close before replacing the destination.
            string json = JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true });
            using (var writer = new StreamWriter(temporary)) { writer.Write(json); }
            File.Move(temporary, path, overwrite: true);
            return Result<string, RentalError>.Ok(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Result<string, RentalError>.Fail(new("REPORT_EXPORT_FAILED", e.Message));
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
