using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Students;

public sealed class Student
{
    public const int MaxNameLength = 100;
    public const int MaxGradeLength = 10;

    private Student()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
        Grade = string.Empty;
    }

    private Student(string firstName, string lastName, string grade, DateOnly? dateOfBirth)
    {
        FirstName = Guard.Required(firstName, MaxNameLength, "First name");
        LastName = Guard.Required(lastName, MaxNameLength, "Last name");
        Grade = Guard.Required(grade, MaxGradeLength, "Grade");
        DateOfBirth = dateOfBirth;
        IsActive = true;
    }

    public int Id { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string Grade { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public bool IsActive { get; private set; }

    public static Student Create(string firstName, string lastName, string grade, DateOnly? dateOfBirth) =>
        new(firstName, lastName, grade, dateOfBirth);

    public void ChangeGrade(string grade) => Grade = Guard.Required(grade, MaxGradeLength, "Grade");

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
