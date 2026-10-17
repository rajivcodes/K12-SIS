namespace AttendanceApi.LegacyImport.Legacy;

// Read-only views of the legacy tables. Shapes for Schools, SchoolTerms and the exact column names of
// Students, AttendanceCodes and StudentAlerts are INFERRED (Legacy-System-Analysis §1.1, G-07) and must be
// checked against the real database before an import; see Legacy/assumed-legacy-schema.sql.
// The legacy schema has no constraints, so almost every column is treated as nullable.

public sealed class LegacySchool
{
    public int SchoolId { get; set; }

    public string? SchoolName { get; set; }

    public bool? Active { get; set; }

    public int? AbsenceAlertThreshold { get; set; }
}

public sealed class LegacySchoolTerm
{
    public int TermId { get; set; }

    public int? SchoolId { get; set; }

    public string? TermName { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }
}

public sealed class LegacyStudent
{
    public int StudentId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Grade { get; set; }

    public int? SchoolId { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedDate { get; set; }
}

public sealed class LegacyAttendanceCode
{
    public string CodeValue { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool? IsAbsent { get; set; }

    public bool? IsExcused { get; set; }

    public bool? IsActive { get; set; }
}

public sealed class LegacyStudentAttendance
{
    public int AttendanceId { get; set; }

    public int? StudentId { get; set; }

    public int? SchoolId { get; set; }

    public DateTime? AttendDate { get; set; }

    public string? AttendCode { get; set; }

    public bool? IsAbsent { get; set; }

    public bool? IsExcused { get; set; }

    public int? MinutesLate { get; set; }

    public string? Notes { get; set; }

    public int? TermId { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? CreatedDate { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }
}

public sealed class LegacyStudentAlert
{
    public int AlertId { get; set; }

    public int? StudentId { get; set; }

    public int? SchoolId { get; set; }

    public string? AlertType { get; set; }

    public string? SchoolYear { get; set; }

    public DateTime? AlertDate { get; set; }

    public string? AlertMessage { get; set; }

    public DateTime? ResolvedDate { get; set; }

    public string? ResolvedBy { get; set; }
}
