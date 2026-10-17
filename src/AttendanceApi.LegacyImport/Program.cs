using System.Globalization;
using AttendanceApi.Infrastructure.Persistence;
using AttendanceApi.LegacyImport.Import;
using AttendanceApi.LegacyImport.Legacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

// Legacy import tool (plan §8). Usage:
//   dotnet run --project src/AttendanceApi.LegacyImport -- --legacy "<legacy connection>" --target "<new connection>"
//       [--dry-run] [--apply-migrations] [--report <file.md>]
// Connection strings can also come from user-secrets or environment variables
// (ConnectionStrings__Legacy, ConnectionStrings__AttendanceDb). Exit codes: 0 done, 1 failed, 2 bad arguments.

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("legacyimport.settings.json", optional: true)
        .AddUserSecrets(typeof(LegacyImporter).Assembly, optional: true)
        .AddEnvironmentVariables()
        .AddCommandLine(
            args.Select(a => a is "--dry-run" or "--apply-migrations" ? $"{a}=true" : a).ToArray(),
            new Dictionary<string, string>
            {
                ["--legacy"] = "ConnectionStrings:Legacy",
                ["--target"] = "ConnectionStrings:AttendanceDb",
                ["--dry-run"] = "Import:DryRun",
                ["--apply-migrations"] = "Import:ApplyMigrations",
                ["--report"] = "Import:ReportPath",
            })
        .Build();

    var legacyConnection = configuration.GetConnectionString("Legacy");
    var targetConnection = configuration.GetConnectionString("AttendanceDb");
    if (string.IsNullOrWhiteSpace(legacyConnection) || string.IsNullOrWhiteSpace(targetConnection))
    {
        Log.Error("Both connection strings are required: --legacy \"<legacy database>\" --target \"<new database>\"");
        return 2;
    }

    var options = new ImportOptions(
        DryRun: configuration.GetValue<bool>("Import:DryRun"),
        ApplyMigrations: configuration.GetValue<bool>("Import:ApplyMigrations"),
        StudentBatchSize: configuration.GetValue("Import:StudentBatchSize", 100));
    var reportPath = configuration["Import:ReportPath"]
        ?? $"legacy-import-report-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.md";

    await using var legacy = new LegacyDbContext(new DbContextOptionsBuilder<LegacyDbContext>()
        .UseSqlServer(legacyConnection, sql => sql.CommandTimeout(600)).Options);
    await using var target = new AttendanceDbContext(new DbContextOptionsBuilder<AttendanceDbContext>()
        .UseSqlServer(targetConnection, sql => sql.CommandTimeout(600)).Options);
    using var loggerFactory = new SerilogLoggerFactory(Log.Logger);

    Log.Information("Importing legacy data (dry run: {DryRun}, apply migrations: {ApplyMigrations})", options.DryRun, options.ApplyMigrations);
    var report = await new LegacyImporter(legacy, target, loggerFactory.CreateLogger<LegacyImporter>()).RunAsync(options);

    await File.WriteAllTextAsync(reportPath, report.ToMarkdown());
    foreach (var (table, c) in report.Tables)
    {
        Log.Information("{Table}: read {Read}, inserted {Inserted}, updated {Updated}, unchanged {Unchanged}, skipped {Skipped}", table, c.Read, c.Inserted, c.Updated, c.Unchanged, c.Skipped);
    }

    foreach (var category in report.Issues.GroupBy(i => i.Category))
    {
        Log.Warning("{Category}: {Count}", category.Key, category.Count());
    }

    Log.Information("Report written to {ReportPath}", Path.GetFullPath(reportPath));
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Legacy import failed; nothing was committed");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
