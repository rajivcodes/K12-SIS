namespace AttendanceApi.Application.Abstractions;

/// <summary>Classifies database exceptions without the Application layer depending on the SQL Server client.</summary>
public interface IDatabaseErrors
{
    /// <summary>
    /// True when a save lost a race with another save: a unique-key collision (e.g. two saves inserting the
    /// same student and date) or an optimistic-concurrency conflict. The save flow retries once.
    /// </summary>
    bool IsConcurrentWriteConflict(Exception exception);
}
