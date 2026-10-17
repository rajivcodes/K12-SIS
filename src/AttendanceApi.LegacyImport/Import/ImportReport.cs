using System.Globalization;
using System.Text;

namespace AttendanceApi.LegacyImport.Import;

/// <summary>Counts per legacy table plus every row that was skipped, changed or needs a person's attention.</summary>
public sealed class ImportReport
{
    private readonly Dictionary<string, TableCounts> _tables = [];

    public ImportReport(DateTime startedAt, bool dryRun)
    {
        StartedAt = startedAt;
        DryRun = dryRun;
    }

    public DateTime StartedAt { get; }

    public bool DryRun { get; }

    public DateTime? FinishedAt { get; set; }

    public List<ImportIssue> Issues { get; } = [];

    public IReadOnlyDictionary<string, TableCounts> Tables => _tables;

    public TableCounts For(string table)
    {
        if (!_tables.TryGetValue(table, out var counts))
        {
            counts = new TableCounts();
            _tables[table] = counts;
        }

        return counts;
    }

    public void Issue(string table, string category, string legacyKey, string message) =>
        Issues.Add(new ImportIssue(table, category, legacyKey, message));

    public int CountIssues(string category) => Issues.Count(i => i.Category == category);

    public string ToMarkdown()
    {
        var md = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        md.AppendLine("# Legacy import report").AppendLine();
        md.AppendLine(inv, $"* Started: {StartedAt:yyyy-MM-dd HH:mm:ss} UTC");
        md.AppendLine(inv, $"* Finished: {FinishedAt:yyyy-MM-dd HH:mm:ss} UTC");
        md.AppendLine(inv, $"* Mode: {(DryRun ? "**dry run – all changes rolled back**" : "committed")}").AppendLine();
        md.AppendLine("| Legacy table | Read | Inserted | Updated | Unchanged | Skipped |").AppendLine("|---|---|---|---|---|---|");
        foreach (var (table, c) in _tables)
        {
            md.AppendLine(inv, $"| {table} | {c.Read} | {c.Inserted} | {c.Updated} | {c.Unchanged} | {c.Skipped} |");
        }

        md.AppendLine();
        foreach (var category in Issues.GroupBy(i => i.Category).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            md.AppendLine(inv, $"## {category.Key} ({category.Count()})").AppendLine();
            md.AppendLine("| Table | Legacy key | Detail |").AppendLine("|---|---|---|");
            foreach (var issue in category)
            {
                md.AppendLine(inv, $"| {issue.Table} | {issue.LegacyKey} | {issue.Message.Replace("|", "\\|", StringComparison.Ordinal)} |");
            }

            md.AppendLine();
        }

        return md.ToString();
    }
}

public sealed class TableCounts
{
    public int Read { get; set; }

    public int Inserted { get; set; }

    public int Updated { get; set; }

    public int Unchanged { get; set; }

    public int Skipped { get; set; }
}

public sealed record ImportIssue(string Table, string Category, string LegacyKey, string Message);

/// <summary>Issue categories in the report.</summary>
public static class IssueCategories
{
    public const string Orphan = "Orphan rows (skipped)";
    public const string Duplicate = "Duplicate student + date (older rows skipped)";
    public const string UnknownCode = "Unknown attendance codes";
    public const string LegacyCode = "Legacy codes not in the standard set (imported)";
    public const string CodeDifference = "Code flags differ from the standard set (standard kept)";
    public const string InvalidData = "Invalid data (skipped)";
    public const string Adjusted = "Values adjusted on import";
    public const string Calendar = "Calendar problems";
    public const string KeptNewSystem = "Records entered in the new system (kept, legacy row ignored)";
    public const string DuplicateAlert = "Second open alert for the same student and year (skipped)";
    public const string Assumption = "Assumptions";
}
