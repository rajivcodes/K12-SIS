using AttendanceApi.Application.Chronic;
using AttendanceApi.Domain.Alerts;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Students;
using AttendanceApi.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Infrastructure.Persistence.Seeding;

/// <summary>
/// Fills an empty database with a small, realistic data set for trying the API: two schools with calendars,
/// eleven students (one transferring mid-year), attendance up to yesterday, recalculated totals and
/// chronic alerts. Deterministic: the same "today" always gives the same data.
/// </summary>
public sealed class DemoDataSeeder
{
    public static readonly Actor DemoActor = new(null, "demo-data");

    private readonly AttendanceDbContext _db;
    private readonly IPasswordHasher<AppUser> _passwordHasher;

    public DemoDataSeeder(AttendanceDbContext db, IPasswordHasher<AppUser> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    private enum Pattern
    {
        Regular,
        Tardy,
        AtRisk,
        Chronic,
        Transfer,
    }

    /// <returns>False when the database already has schools (nothing is changed).</returns>
    public async Task<bool> SeedAsync(DateOnly today, DateTime utcNow, string? demoUserPassword, CancellationToken cancellationToken)
    {
        if (await _db.Schools.AnyAsync(cancellationToken))
        {
            return false;
        }

        var label = SchoolYearLabel.ForDate(today);
        var lincoln = School.Create("Lincoln School");
        var roosevelt = School.Create("Roosevelt School");
        _db.Schools.AddRange(lincoln, roosevelt);
        await _db.SaveChangesAsync(cancellationToken);

        var rooseveltSettings = SchoolAttendanceSettings.CreateDefault(roosevelt.Id);
        rooseveltSettings.SetChronicRules(ChronicMethod.Either, SchoolAttendanceSettings.DefaultChronicCountThreshold, SchoolAttendanceSettings.DefaultChronicRateThreshold);
        _db.SchoolAttendanceSettings.AddRange(SchoolAttendanceSettings.CreateDefault(lincoln.Id), rooseveltSettings);

        var calendars = new Dictionary<int, SchoolCalendar>();
        foreach (var school in new[] { lincoln, roosevelt })
        {
            var year = SchoolYear.Create(school.Id, label.DefaultStartDate, label.DefaultEndDate);
            year.AddTerm("Semester 1", label.DefaultStartDate, new DateOnly(label.EndYear, 1, 22));
            year.AddTerm("Semester 2", new DateOnly(label.EndYear, 1, 25), new DateOnly(label.EndYear, 6, 18));
            _db.SchoolYears.Add(year);
            var closures = new[] { new DateOnly(label.StartYear, 11, 26), new DateOnly(label.StartYear, 11, 27), new DateOnly(label.EndYear, 1, 18) };
            _db.NonInstructionalDays.AddRange(closures.Select(d => NonInstructionalDay.Create(school.Id, d, "Holiday")));
            calendars[school.Id] = new SchoolCalendar(year, closures);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var students = new (string First, string Last, string Grade, School School, Pattern Pattern)[]
        {
            ("Ava", "Thompson", "04", lincoln, Pattern.Regular),
            ("Liam", "Carter", "04", lincoln, Pattern.Chronic),
            ("Sofia", "Nguyen", "04", lincoln, Pattern.Tardy),
            ("Noah", "Patel", "05", lincoln, Pattern.AtRisk),
            ("Emma", "Rodriguez", "05", lincoln, Pattern.Regular),
            ("Maya", "Johnson", "05", lincoln, Pattern.Transfer),
            ("Olivia", "Brooks", "06", roosevelt, Pattern.Regular),
            ("Ethan", "Rivera", "06", roosevelt, Pattern.AtRisk),
            ("Isabella", "Chen", "07", roosevelt, Pattern.Tardy),
            ("Lucas", "Martin", "07", roosevelt, Pattern.Regular),
            ("Mia", "Wilson", "07", roosevelt, Pattern.Chronic),
        };

        // Maya moves from Lincoln to Roosevelt six weeks into the year (if that date has been reached).
        var transferDate = label.DefaultStartDate.AddDays(42);
        var created = new List<(Student Student, Pattern Pattern, List<StudentEnrollment> Enrollments)>();
        foreach (var s in students)
        {
            var student = Student.Create(s.First, s.Last, s.Grade, new DateOnly(label.StartYear - 5 - int.Parse(s.Grade, System.Globalization.CultureInfo.InvariantCulture), 3, 1 + created.Count));
            _db.Students.Add(student);
            created.Add((student, s.Pattern, []));
        }

        await _db.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < students.Length; i++)
        {
            var (student, pattern, enrollments) = created[i];
            if (pattern == Pattern.Transfer && transferDate <= today)
            {
                enrollments.Add(StudentEnrollment.Create(student.Id, lincoln.Id, label.DefaultStartDate, transferDate.AddDays(-1)));
                enrollments.Add(StudentEnrollment.Create(student.Id, roosevelt.Id, transferDate));
            }
            else
            {
                enrollments.Add(StudentEnrollment.Create(student.Id, students[i].School.Id, label.DefaultStartDate));
            }

            _db.StudentEnrollments.AddRange(enrollments);
        }

        var codes = await _db.AttendanceCodes.ToDictionaryAsync(c => c.Code, cancellationToken);
        foreach (var (student, pattern, enrollments) in created)
        {
            var dayIndex = 0;
            for (var day = label.DefaultStartDate; day < today; day = day.AddDays(1))
            {
                var enrollment = enrollments.Find(e => e.IsEnrolledOn(day));
                if (enrollment is null || !calendars[enrollment.SchoolId].IsSchoolDay(day))
                {
                    continue;
                }

                var (code, minutesLate, notes) = DayFor(pattern, dayIndex++);
                var year = calendars[enrollment.SchoolId].Year;
                var placement = new AttendancePlacement(enrollment.SchoolId, day, year.Id, year.FindTerm(day)?.Id);
                _db.AttendanceRecords.Add(AttendanceRecord.Create(student.Id, placement, codes[code], minutesLate, notes, DemoActor, utcNow));
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await RecalculateTotalsAndAlertsAsync(created.Select(c => c.Student.Id).ToList(), label, today, utcNow, cancellationToken);

        if (!string.IsNullOrEmpty(demoUserPassword))
        {
            AddUser("lincoln.clerk", "Lincoln Attendance Clerk", UserRole.AttendanceClerk, demoUserPassword, lincoln.Id);
            AddUser("roosevelt.clerk", "Roosevelt Attendance Clerk", UserRole.AttendanceClerk, demoUserPassword, roosevelt.Id);
            AddUser("district.viewer", "District Viewer", UserRole.Viewer, demoUserPassword, lincoln.Id, roosevelt.Id);
        }

        _db.AuditEntries.Add(AuditEntry.Record(DemoActor, utcNow, "DemoDataSeeded", "Database", label.ToString(), $"{students.Length} students, 2 schools"));
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static (string Code, int MinutesLate, string? Notes) DayFor(Pattern pattern, int i) => pattern switch
    {
        Pattern.Chronic when i % 4 == 1 => i % 8 == 1 ? ("E", 0, "Parent called in sick") : ("A", 0, null),
        Pattern.AtRisk when i % 9 == 4 => ("A", 0, null),
        Pattern.Transfer when i % 5 == 3 => ("A", 0, null),
        Pattern.Tardy when i % 6 == 2 => ("T", 5 + (i % 4 * 5), "Late bus"),
        _ when i % 25 == 12 => ("M", 0, "Medical appointment"),
        _ => ("P", 0, null),
    };

    private async Task RecalculateTotalsAndAlertsAsync(List<int> studentIds, SchoolYearLabel label, DateOnly today, DateTime utcNow, CancellationToken cancellationToken)
    {
        var evaluations = await new ChronicStatusCalculator(_db).EvaluateAsync(studentIds, label, today, cancellationToken);
        foreach (var evaluation in evaluations.Values)
        {
            _db.StudentAttendanceSummaries.Add(StudentAttendanceSummary.Create(evaluation.StudentId, label, evaluation.Totals, utcNow));
            if (evaluation.CurrentSchoolId is { } schoolId && StudentAlert.ShouldRaiseChronicAbsence(evaluation.Result, hasOpenAlert: false))
            {
                _db.StudentAlerts.Add(StudentAlert.RaiseChronicAbsence(evaluation.StudentId, schoolId, label, evaluation.Result, utcNow));
            }
        }
    }

    private void AddUser(string userName, string displayName, UserRole role, string password, params int[] schoolIds)
    {
        var user = AppUser.Create(userName, displayName, null, "pending", role);
        user.SetPasswordHash(_passwordHasher.HashPassword(user, password));
        user.AssignSchools(schoolIds);
        _db.AppUsers.Add(user);
    }
}
