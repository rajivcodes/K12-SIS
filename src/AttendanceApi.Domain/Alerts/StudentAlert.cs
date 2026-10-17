using System.Globalization;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Chronic;
using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Schools;

namespace AttendanceApi.Domain.Alerts;

/// <summary>
/// An alert about a student. At most one open CHRONIC_ABSENCE alert per student per school year (BR-09),
/// enforced in the database by a filtered unique index.
/// </summary>
public sealed class StudentAlert
{
    public const string ChronicAbsenceType = "CHRONIC_ABSENCE";
    public const int MaxAlertTypeLength = 50;
    public const int MaxMessageLength = 500;

    private StudentAlert()
    {
        AlertType = string.Empty;
        SchoolYearLabel = string.Empty;
        Message = string.Empty;
    }

    private StudentAlert(int studentId, int schoolId, string alertType, SchoolYearLabel label, string message, DateTime utcNow)
    {
        StudentId = studentId;
        SchoolId = schoolId;
        AlertType = alertType;
        SchoolYearLabel = label.ToString();
        Message = Guard.Required(message, MaxMessageLength, "Message");
        RaisedAt = Guard.Utc(utcNow, nameof(utcNow));
    }

    public int Id { get; private set; }

    public int StudentId { get; private set; }

    public int SchoolId { get; private set; }

    public string AlertType { get; private set; }

    public string SchoolYearLabel { get; private set; }

    public DateTime RaisedAt { get; private set; }

    public string Message { get; private set; }

    public DateTime? ResolvedAt { get; private set; }

    public int? ResolvedByUserId { get; private set; }

    public string? ResolvedBy { get; private set; }

    public bool IsOpen => ResolvedAt is null;

    /// <summary>True when the result is chronic and the student has no open chronic-absence alert for the year.</summary>
    public static bool ShouldRaiseChronicAbsence(ChronicAbsenceResult result, bool hasOpenAlert) =>
        result.IsChronicallyAbsent && !hasOpenAlert;

    /// <param name="studentId">The student.</param>
    /// <param name="currentSchoolId">The student's current school for the year (Q3).</param>
    /// <param name="label">The school year.</param>
    /// <param name="result">A chronic result; must be <see cref="ChronicStatus.Chronic"/>.</param>
    /// <param name="utcNow">The current UTC time.</param>
    public static StudentAlert RaiseChronicAbsence(
        int studentId,
        int currentSchoolId,
        SchoolYearLabel label,
        ChronicAbsenceResult result,
        DateTime utcNow)
    {
        if (!result.IsChronicallyAbsent)
        {
            throw new DomainException("A chronic-absence alert needs a chronic result.");
        }

        return new StudentAlert(studentId, currentSchoolId, ChronicAbsenceType, label, ChronicMessage(label, result), utcNow);
    }

    /// <summary>Creates an alert from legacy data, keeping its type, message and resolution.</summary>
    public static StudentAlert Import(
        int studentId,
        int schoolId,
        string alertType,
        SchoolYearLabel label,
        string message,
        DateTime raisedAt,
        DateTime? resolvedAt,
        Actor? resolvedBy)
    {
        var alert = new StudentAlert(
            studentId,
            schoolId,
            Guard.Required(alertType, MaxAlertTypeLength, "Alert type").ToUpperInvariant(),
            label,
            string.IsNullOrWhiteSpace(message) ? "(imported alert without a message)" : message.Length > MaxMessageLength ? message[..MaxMessageLength] : message,
            raisedAt);
        if (resolvedAt is { } resolved)
        {
            alert.ResolvedAt = Guard.Utc(resolved, nameof(resolvedAt));
            alert.ResolvedByUserId = resolvedBy?.UserId;
            alert.ResolvedBy = resolvedBy?.UserName;
        }

        return alert;
    }

    public void Resolve(Actor actor, DateTime utcNow)
    {
        if (!IsOpen)
        {
            throw new DomainException("The alert is already resolved.");
        }

        ResolvedAt = Guard.Utc(utcNow, nameof(utcNow));
        ResolvedByUserId = actor.UserId;
        ResolvedBy = actor.UserName;
    }

    private static string ChronicMessage(SchoolYearLabel label, ChronicAbsenceResult result)
    {
        var parts = new List<string>(2);
        if (result.Method != ChronicMethod.Rate && result.Count.IsChronic)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{result.Count.TotalAbsences} absences (threshold {result.Count.Threshold})"));
        }

        if (result.Method != ChronicMethod.Count && result.Rate.IsChronic)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{result.Rate.RatePercent:0.#}% of {result.Rate.EnrolledDays} enrolled school days absent (threshold {result.Rate.ThresholdPercent:0.#}%)"));
        }

        return $"Student is chronically absent in {label}: {string.Join("; ", parts)}.";
    }
}
