using System.Reflection;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Students;

namespace AttendanceApi.Domain.Tests;

/// <summary>Builders for entities whose IDs are normally assigned by the database.</summary>
internal static class TestData
{
    public static readonly DateTime UtcNow = new(2026, 10, 17, 9, 30, 0, DateTimeKind.Utc);

    public static readonly Actor Clerk = new(7, "clerk.jones");

    public static T WithId<T>(this T entity, int id)
        where T : class
    {
        var property = typeof(T).GetProperty("Id", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id property.");
        property.SetValue(entity, id);
        return entity;
    }

    public static Student Student(int id, bool isActive = true)
    {
        var student = Domain.Students.Student.Create("First" + id, "Last" + id, "05", null).WithId(id);
        if (!isActive)
        {
            student.Deactivate();
        }

        return student;
    }

    public static AttendanceCode Code(int id, string code, bool isAbsent, bool isExcused, bool isActive = true) =>
        AttendanceCode.Create(code, "Code " + code, isAbsent, isExcused, isActive).WithId(id);

    /// <summary>The standard set from plan §4 plus one inactive code.</summary>
    public static IReadOnlyList<AttendanceCode> StandardCodes() =>
    [
        Code(1, "P", isAbsent: false, isExcused: false),
        Code(2, "T", isAbsent: false, isExcused: false),
        Code(3, "TE", isAbsent: false, isExcused: true),
        Code(4, "A", isAbsent: true, isExcused: false),
        Code(5, "E", isAbsent: true, isExcused: true),
        Code(6, "M", isAbsent: true, isExcused: true),
        Code(7, "OSS", isAbsent: true, isExcused: false),
        Code(8, "ISS", isAbsent: false, isExcused: false),
        Code(9, "FT", isAbsent: false, isExcused: false),
        Code(10, "X", isAbsent: true, isExcused: false, isActive: false),
    ];

    public static StudentEnrollment Enrollment(int studentId, int schoolId, DateOnly entry, DateOnly? exit = null, int id = 0) =>
        StudentEnrollment.Create(studentId, schoolId, entry, exit).WithId(id);
}
