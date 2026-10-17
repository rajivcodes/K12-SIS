using System.Globalization;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Schools;

namespace AttendanceApi.Domain.Attendance;

/// <summary>
/// Checks an attendance date against a school's date rules (plan decision 10, Q4). "Today" is the
/// current UTC date (Q2). A rejected date rejects the whole batch.
/// </summary>
public static class AttendanceDatePolicy
{
    /// <returns>Null when the date is allowed; otherwise the first rule it breaks.</returns>
    public static AttendanceDateRejection? Check(
        DateOnly date,
        DateOnly today,
        SchoolAttendanceSettings settings,
        SchoolYear schoolYear)
    {
        if (!schoolYear.Contains(date))
        {
            throw new ArgumentException("The school year must contain the date.", nameof(schoolYear));
        }

        if (date > today)
        {
            if (!settings.AllowFutureDates)
            {
                return Reject(AttendanceDateRejectionReason.FutureDateNotAllowed, $"{Format(date)} is in the future, and this school does not allow future dates.");
            }

            if (settings.MaxFutureDays is { } maxFuture && date > today.AddDays(maxFuture))
            {
                return Reject(AttendanceDateRejectionReason.TooFarInFuture, $"{Format(date)} is more than {maxFuture} day(s) after today ({Format(today)}).");
            }
        }

        if (settings.MaxBackdateDays is { } maxBack && date < today.AddDays(-maxBack))
        {
            return Reject(AttendanceDateRejectionReason.TooFarInPast, $"{Format(date)} is more than {maxBack} day(s) before today ({Format(today)}).");
        }

        if (!settings.AllowWeekends && AttendanceDates.IsWeekend(date))
        {
            return Reject(AttendanceDateRejectionReason.WeekendNotAllowed, $"{Format(date)} is a weekend, and this school does not allow weekend dates.");
        }

        if (!settings.AllowOutsideTerm && schoolYear.FindTerm(date) is null)
        {
            return Reject(AttendanceDateRejectionReason.OutsideTerm, $"{Format(date)} is not inside any term of school year {schoolYear.Label}.");
        }

        return null;
    }

    private static AttendanceDateRejection Reject(AttendanceDateRejectionReason reason, string message) => new(reason, message);

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
