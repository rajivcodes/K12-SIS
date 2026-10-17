using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Schools;

public sealed class School
{
    public const int MaxNameLength = 200;

    private School()
    {
        Name = string.Empty;
    }

    private School(string name)
    {
        Name = Guard.Required(name, MaxNameLength, "School name");
        IsActive = true;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public static School Create(string name) => new(name);

    public void Rename(string name) => Name = Guard.Required(name, MaxNameLength, "School name");

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
