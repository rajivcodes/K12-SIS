using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Attendance;

/// <summary>
/// One student's attendance for one calendar day (BR-01). Unique per (StudentId, Date). The absent and
/// excused flags are copied from the code at save time (BR-03), so later code changes don't rewrite history.
/// </summary>
public sealed class AttendanceRecord
{
    public const int MaxNotesLength = 500;

    private AttendanceRecord()
    {
        CreatedBy = string.Empty;
    }

    private AttendanceRecord(int studentId, DateOnly date, Actor actor, DateTime utcNow)
    {
        StudentId = studentId;
        Date = date;
        CreatedByUserId = actor.UserId;
        CreatedBy = actor.UserName;
        CreatedAt = Guard.Utc(utcNow, nameof(utcNow));
    }

    public int Id { get; private set; }

    public int StudentId { get; private set; }

    public int SchoolId { get; private set; }

    public DateOnly Date { get; private set; }

    public int SchoolYearId { get; private set; }

    public int? TermId { get; private set; }

    public int AttendanceCodeId { get; private set; }

    public bool IsAbsent { get; private set; }

    public bool IsExcused { get; private set; }

    public int MinutesLate { get; private set; }

    public string? Notes { get; private set; }

    public int? CreatedByUserId { get; private set; }

    public string CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public int? ModifiedByUserId { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTime? ModifiedAt { get; private set; }

    /// <summary>Optimistic-concurrency token maintained by SQL Server.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public static AttendanceRecord Create(
        int studentId,
        AttendancePlacement placement,
        AttendanceCode code,
        int minutesLate,
        string? notes,
        Actor actor,
        DateTime utcNow)
    {
        var record = new AttendanceRecord(studentId, placement.Date, actor, utcNow);
        record.Apply(placement, code, minutesLate, notes);
        return record;
    }

    /// <summary>
    /// Creates a record from legacy data (plan §8): the absent/excused flags stored on the legacy row are kept
    /// as they were, and the legacy audit user and timestamps are preserved. The code may be inactive.
    /// </summary>
    public static AttendanceRecord Import(int studentId, AttendancePlacement placement, ImportedAttendance imported)
    {
        var record = new AttendanceRecord(studentId, placement.Date, imported.CreatedBy, imported.CreatedAt);
        record.ApplyImported(placement, imported);
        return record;
    }

    /// <summary>Refreshes a record created by an earlier import run from the legacy row.</summary>
    public void ReplaceImported(AttendancePlacement placement, ImportedAttendance imported)
    {
        if (placement.Date != Date)
        {
            throw new DomainException("An attendance record's date cannot change.");
        }

        CreatedByUserId = imported.CreatedBy.UserId;
        CreatedBy = imported.CreatedBy.UserName;
        CreatedAt = Guard.Utc(imported.CreatedAt, "createdAt");
        ApplyImported(placement, imported);
    }

    /// <summary>Overwrites the record with a new entry for the same student and date (BR-04).</summary>
    public void Update(
        AttendancePlacement placement,
        AttendanceCode code,
        int minutesLate,
        string? notes,
        Actor actor,
        DateTime utcNow)
    {
        if (placement.Date != Date)
        {
            throw new DomainException("An attendance record's date cannot change.");
        }

        Apply(placement, code, minutesLate, notes);
        ModifiedByUserId = actor.UserId;
        ModifiedBy = actor.UserName;
        ModifiedAt = Guard.Utc(utcNow, nameof(utcNow));
    }

    private void ApplyImported(AttendancePlacement placement, ImportedAttendance imported)
    {
        if (imported.MinutesLate < 0)
        {
            throw new DomainException("Minutes late cannot be negative.");
        }

        SchoolId = placement.SchoolId;
        SchoolYearId = placement.SchoolYearId;
        TermId = placement.TermId;
        AttendanceCodeId = imported.CodeId;
        IsAbsent = imported.IsAbsent;
        IsExcused = imported.IsExcused;
        MinutesLate = imported.MinutesLate;
        Notes = string.IsNullOrWhiteSpace(imported.Notes) ? null
            : imported.Notes.Length > MaxNotesLength ? imported.Notes[..MaxNotesLength]
            : imported.Notes;
        ModifiedByUserId = imported.ModifiedBy?.UserId;
        ModifiedBy = imported.ModifiedBy?.UserName;
        ModifiedAt = imported.ModifiedAt is { } modifiedAt ? Guard.Utc(modifiedAt, "modifiedAt") : null;
    }

    private void Apply(AttendancePlacement placement, AttendanceCode code, int minutesLate, string? notes)
    {
        if (!code.IsActive)
        {
            throw new DomainException($"Attendance code '{code.Code}' is inactive.");
        }

        if (minutesLate < 0)
        {
            throw new DomainException("Minutes late cannot be negative.");
        }

        if (notes?.Length > MaxNotesLength)
        {
            throw new DomainException($"Notes must be at most {MaxNotesLength} characters.");
        }

        SchoolId = placement.SchoolId;
        SchoolYearId = placement.SchoolYearId;
        TermId = placement.TermId;
        AttendanceCodeId = code.Id;
        IsAbsent = code.IsAbsent;
        IsExcused = code.IsExcused;
        MinutesLate = minutesLate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;
    }
}
