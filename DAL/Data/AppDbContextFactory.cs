using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DAL.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var values = LoadEnvironmentValues();
        var connectionString = GetValue(values, "ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString =
                $"Host={GetRequiredValue(values, "DB_HOST")};" +
                $"Port={GetRequiredValue(values, "DB_PORT")};" +
                $"Database={GetRequiredValue(values, "DB_NAME")};" +
                $"Username={GetRequiredValue(values, "DB_USERNAME")};" +
                $"Password={GetValue(values, "DB_PASSWORD") ?? string.Empty}";
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }

    private static Dictionary<string, string> LoadEnvironmentValues()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var currentDirectory = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(currentDirectory, ".env"),
            Path.Combine(currentDirectory, "API", ".env"),
            Path.Combine(currentDirectory, "..", "API", ".env")
        };

        var envFile = candidates.FirstOrDefault(File.Exists);

        if (envFile is not null)
        {
            foreach (var line in File.ReadLines(envFile))
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = trimmedLine.IndexOf('=');

                if (separatorIndex > 0)
                {
                    values[trimmedLine[..separatorIndex].Trim()] =
                        trimmedLine[(separatorIndex + 1)..].Trim();
                }
            }
        }

        return values;
    }

    private static string? GetValue(
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        return Environment.GetEnvironmentVariable(key)
            ?? (values.TryGetValue(key, out var value) ? value : null);
    }

    private static string GetRequiredValue(
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        return GetValue(values, key)
            ?? throw new InvalidOperationException(
                $"Konfigurasi database '{key}' belum diatur.");
    }
}
