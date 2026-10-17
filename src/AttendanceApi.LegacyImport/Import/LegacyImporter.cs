using System.Globalization;
using AttendanceApi.Application.Chronic;
using AttendanceApi.Domain.Alerts;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Students;
using AttendanceApi.Infrastructure.Persistence;
using AttendanceApi.Infrastructure.Persistence.Seeding;
using AttendanceApi.LegacyImport.Legacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AttendanceApi.LegacyImport.Import;

public sealed record ImportOptions(bool DryRun, bool ApplyMigrations, int StudentBatchSize = 100);

/// <summary>
/// Copies the legacy database into the new schema (plan §8). Safe to run again: every imported row is
/// recorded in <see cref="LegacyIdMap"/>, so a re-run updates instead of duplicating. The whole run is one
/// transaction; a dry run rolls it back after producing the report. Totals are recalculated from the
/// imported records, never copied (the legacy totals are affected by D-01 and D-03).
/// </summary>
public sealed partial class LegacyImporter
{
    public static readonly Actor ImportActor = new(null, "legacy-import");

    private const string SchoolsTable = "Schools";
    private const string TermsTable = "SchoolTerms";
    private const string StudentsTable = "Students";
    private const string CodesTable = "AttendanceCodes";
    private const string AttendanceTable = "StudentAttendance";
    private const string AlertsTable = "StudentAlerts";

    private readonly LegacyDbContext _legacy;
    private readonly AttendanceDbContext _target;
    private readonly ILogger<LegacyImporter> _logger;
    private readonly Func<DateTime> _utcNow;

    private readonly Dictionary<string, Dictionary<string, int>> _maps = [];
    private readonly Dictionary<int, List<SchoolYear>> _yearsBySchool = [];
    private readonly Dictionary<string, AttendanceCode> _codes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(int StudentId, string Label)> _touchedTotals = [];
    private readonly Dictionary<int, int?> _legacyStudentSchool = [];

    private ImportReport _report = null!;
    private DateTime _now;

    public LegacyImporter(LegacyDbContext legacy, AttendanceDbContext target, ILogger<LegacyImporter> logger, Func<DateTime>? utcNow = null)
    {
        _legacy = legacy;
        _target = target;
        _logger = logger;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public async Task<ImportReport> RunAsync(ImportOptions options, CancellationToken cancellationToken = default)
    {
        _now = _utcNow();
        _report = new ImportReport(_now, options.DryRun);

        if (options.ApplyMigrations)
        {
            await _target.Database.MigrateAsync(cancellationToken);
        }

        _target.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        await using var transaction = await _target.Database.BeginTransactionAsync(cancellationToken);

        await LoadMapsAsync(cancellationToken);
        await ImportSchoolsAsync(cancellationToken);
        await ImportTermsAsync(cancellationToken);
        await ImportCodesAsync(cancellationToken);
        await ImportStudentsAsync(cancellationToken);
        await ImportAttendanceAsync(options.StudentBatchSize, cancellationToken);
        await ImportAlertsAsync(cancellationToken);
        await RecalculateTotalsAsync(cancellationToken);

        _report.Issue("All", IssueCategories.Assumption, "-", "Legacy DATETIME values have no time zone; they were stored as UTC unchanged.");
        _report.FinishedAt = _utcNow();
        _target.AuditEntries.Add(AuditEntry.Record(
            ImportActor,
            _now,
            options.DryRun ? "LegacyImportDryRun" : "LegacyImportRun",
            "Database",
            "legacy",
            string.Join("; ", _report.Tables.Select(t => $"{t.Key}: +{t.Value.Inserted} ~{t.Value.Updated} ={t.Value.Unchanged} skipped {t.Value.Skipped}"))));
        await _target.SaveChangesAsync(cancellationToken);

        if (options.DryRun)
        {
            await transaction.RollbackAsync(cancellationToken);
            LogDryRun();
        }
        else
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return _report;
    }

    // ---------------------------------------------------------------- Schools

    private async Task ImportSchoolsAsync(CancellationToken ct)
    {
        var counts = _report.For(SchoolsTable);
        foreach (var legacy in await _legacy.Schools.OrderBy(s => s.SchoolId).ToListAsync(ct))
        {
            counts.Read++;
            var key = Key(legacy.SchoolId);
            var name = string.IsNullOrWhiteSpace(legacy.SchoolName) ? $"School {key}" : legacy.SchoolName.Trim();
            if (name.Length > School.MaxNameLength)
            {
                name = name[..School.MaxNameLength];
            }

            if (MappedId(SchoolsTable, key) is { } schoolId)
            {
                var school = await _target.Schools.SingleAsync(s => s.Id == schoolId, ct);
                var changed = school.Name != name || school.IsActive != (legacy.Active ?? true);
                school.Rename(name);
                SetActive(school, legacy.Active ?? true);
                Count(counts, changed);
                continue;
            }

            var created = School.Create(name);
            SetActive(created, legacy.Active ?? true);
            _target.Schools.Add(created);
            await _target.SaveChangesAsync(ct);

            var settings = SchoolAttendanceSettings.CreateDefault(created.Id);
            if (legacy.AbsenceAlertThreshold is > 0 and var threshold)
            {
                settings.SetChronicRules(ChronicMethod.Count, threshold, SchoolAttendanceSettings.DefaultChronicRateThreshold);
            }

            _target.SchoolAttendanceSettings.Add(settings);
            AddMap(SchoolsTable, key, created.Id);
            counts.Inserted++;
        }

        await _target.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- SchoolTerms → SchoolYear + Term

    private async Task ImportTermsAsync(CancellationToken ct)
    {
        var counts = _report.For(TermsTable);
        var legacyTerms = await _legacy.SchoolTerms.OrderBy(t => t.TermId).ToListAsync(ct);
        counts.Read = legacyTerms.Count;

        foreach (var schoolGroup in legacyTerms.GroupBy(t => t.SchoolId))
        {
            var schoolId = schoolGroup.Key is { } legacySchoolId ? MappedId(SchoolsTable, Key(legacySchoolId)) : null;
            var inputs = new List<LegacyTermInput>();
            foreach (var term in schoolGroup)
            {
                if (schoolId is null)
                {
                    Skip(counts, TermsTable, IssueCategories.Orphan, term.TermId, $"School {schoolGroup.Key?.ToString(CultureInfo.InvariantCulture) ?? "NULL"} does not exist.");
                }
                else if (term.StartDate is null || term.EndDate is null || term.EndDate < term.StartDate)
                {
                    Skip(counts, TermsTable, IssueCategories.InvalidData, term.TermId, "Missing or reversed start/end dates.");
                }
                else if (MappedId(TermsTable, Key(term.TermId)) is not null)
                {
                    counts.Unchanged++;
                }
                else
                {
                    var name = string.IsNullOrWhiteSpace(term.TermName) ? $"Term {term.TermId}" : term.TermName.Trim();
                    inputs.Add(new LegacyTermInput(term.TermId, name.Length > Term.MaxNameLength ? name[..Term.MaxNameLength] : name, DateOnly.FromDateTime(term.StartDate.Value), DateOnly.FromDateTime(term.EndDate.Value)));
                }
            }

            if (schoolId is not { } targetSchoolId || inputs.Count == 0)
            {
                continue;
            }

            var plan = LegacyCalendarBuilder.Build(inputs);
            foreach (var rejected in plan.Rejected)
            {
                Skip(counts, TermsTable, IssueCategories.Calendar, rejected.Term.LegacyTermId, rejected.Reason);
            }

            var years = await _target.SchoolYears.Include(y => y.Terms).Where(y => y.SchoolId == targetSchoolId).ToListAsync(ct);
            foreach (var planned in plan.Years)
            {
                var year = years.Find(y => y.Label == planned.Label.ToString());
                if (year is null)
                {
                    year = SchoolYear.Create(targetSchoolId, planned.StartDate, planned.EndDate);
                    try
                    {
                        year.EnsureNoConflictWith(years);
                    }
                    catch (DomainException ex)
                    {
                        foreach (var term in planned.Terms)
                        {
                            Skip(counts, TermsTable, IssueCategories.Calendar, term.LegacyTermId, ex.Message);
                        }

                        continue;
                    }

                    _target.SchoolYears.Add(year);
                    years.Add(year);
                }

                foreach (var input in planned.Terms)
                {
                    try
                    {
                        year.AddTerm(input.Name, input.StartDate, input.EndDate);
                        await _target.SaveChangesAsync(ct);
                        AddMap(TermsTable, Key(input.LegacyTermId), year.Terms.Single(t => t.StartDate == input.StartDate && t.Name == input.Name).Id);
                        counts.Inserted++;
                    }
                    catch (DomainException ex)
                    {
                        Skip(counts, TermsTable, IssueCategories.Calendar, input.LegacyTermId, ex.Message);
                    }
                }
            }

            await _target.SaveChangesAsync(ct);
        }
    }

    // ---------------------------------------------------------------- AttendanceCodes

    private async Task ImportCodesAsync(CancellationToken ct)
    {
        // The standard §4 set is seeded first if the table is empty (plan decision 2).
        if (!await _target.AttendanceCodes.AnyAsync(ct))
        {
            _target.AttendanceCodes.AddRange(StandardAttendanceCodes.Create());
            await _target.SaveChangesAsync(ct);
        }

        foreach (var code in await _target.AttendanceCodes.ToListAsync(ct))
        {
            _codes[code.Code] = code;
        }

        var counts = _report.For(CodesTable);
        foreach (var legacy in await _legacy.AttendanceCodes.ToListAsync(ct))
        {
            counts.Read++;
            var value = legacy.CodeValue.Trim();
            if (value.Length is 0 or > AttendanceCode.MaxCodeLength)
            {
                Skip(counts, CodesTable, IssueCategories.InvalidData, legacy.CodeValue, "Code is empty or longer than 10 characters.");
                continue;
            }

            if (_codes.TryGetValue(value, out var existing))
            {
                if (existing.IsAbsent != (legacy.IsAbsent ?? false) || existing.IsExcused != (legacy.IsExcused ?? false))
                {
                    _report.Issue(CodesTable, IssueCategories.CodeDifference, value,
                        $"Legacy flags absent={legacy.IsAbsent}, excused={legacy.IsExcused}; kept absent={existing.IsAbsent}, excused={existing.IsExcused}. Imported records keep their own stored flags.");
                }

                counts.Unchanged++;
                continue;
            }

            var created = AttendanceCode.Create(value, string.IsNullOrWhiteSpace(legacy.Description) ? value : Truncate(legacy.Description.Trim(), AttendanceCode.MaxDescriptionLength), legacy.IsAbsent ?? false, legacy.IsExcused ?? false, legacy.IsActive ?? true);
            _target.AttendanceCodes.Add(created);
            _codes[created.Code] = created;
            _report.Issue(CodesTable, IssueCategories.LegacyCode, value, $"Imported as-is: absent={created.IsAbsent}, excused={created.IsExcused}, active={created.IsActive}. Review its flags.");
            counts.Inserted++;
        }

        await _target.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- Students → Student + StudentEnrollment

    private async Task ImportStudentsAsync(CancellationToken ct)
    {
        var counts = _report.For(StudentsTable);
        var firstAttendance = await _legacy.StudentAttendance
            .Where(a => a.StudentId != null && a.AttendDate != null)
            .GroupBy(a => a.StudentId!.Value)
            .Select(g => new { StudentId = g.Key, First = g.Min(a => a.AttendDate!.Value) })
            .ToDictionaryAsync(x => x.StudentId, x => DateOnly.FromDateTime(x.First), ct);
        var enrolledStudentIds = (await _target.StudentEnrollments.Select(e => e.StudentId).Distinct().ToListAsync(ct)).ToHashSet();

        foreach (var legacy in await _legacy.Students.OrderBy(s => s.StudentId).ToListAsync(ct))
        {
            counts.Read++;
            var key = Key(legacy.StudentId);
            _legacyStudentSchool[legacy.StudentId] = legacy.SchoolId;
            if (string.IsNullOrWhiteSpace(legacy.FirstName) || string.IsNullOrWhiteSpace(legacy.LastName))
            {
                Skip(counts, StudentsTable, IssueCategories.InvalidData, key, "First or last name is missing.");
                continue;
            }

            var grade = string.IsNullOrWhiteSpace(legacy.Grade) ? "?" : Truncate(legacy.Grade.Trim(), Student.MaxGradeLength);
            Student student;
            if (MappedId(StudentsTable, key) is { } studentId)
            {
                student = await _target.Students.SingleAsync(s => s.Id == studentId, ct);
                var changed = student.Grade != grade || student.IsActive != (legacy.IsActive ?? true);
                student.ChangeGrade(grade);
                SetActive(student, legacy.IsActive ?? true);
                Count(counts, changed);
            }
            else
            {
                student = Student.Create(Truncate(legacy.FirstName.Trim(), Student.MaxNameLength), Truncate(legacy.LastName.Trim(), Student.MaxNameLength), grade, legacy.DateOfBirth is { } dob ? DateOnly.FromDateTime(dob) : null);
                SetActive(student, legacy.IsActive ?? true);
                _target.Students.Add(student);
                await _target.SaveChangesAsync(ct);
                AddMap(StudentsTable, key, student.Id);
                counts.Inserted++;
            }

            // Legacy keeps no enrollment history (G-04): one open enrollment at the current school, starting at
            // the school-year start of the student's first legacy attendance, else CreatedDate, else this year.
            if (enrolledStudentIds.Contains(student.Id))
            {
                continue;
            }

            if (legacy.SchoolId is not { } legacySchool || MappedId(SchoolsTable, Key(legacySchool)) is not { } schoolId)
            {
                _report.Issue(StudentsTable, IssueCategories.Orphan, key, "Student imported without an enrollment: their school does not exist.");
                continue;
            }

            var entry = firstAttendance.TryGetValue(legacy.StudentId, out var first)
                ? await YearStartAsync(schoolId, first, ct)
                : legacy.CreatedDate is { } createdDate ? DateOnly.FromDateTime(createdDate) : SchoolYearLabel.ForDate(DateOnly.FromDateTime(_now)).DefaultStartDate;
            _target.StudentEnrollments.Add(StudentEnrollment.Create(student.Id, schoolId, entry));
            enrolledStudentIds.Add(student.Id);
        }

        await _target.SaveChangesAsync(ct);
        _report.Issue(StudentsTable, IssueCategories.Assumption, "-", "Enrollments: one open enrollment per student at their current legacy school, starting at the school-year start of their first legacy attendance (or CreatedDate). Inactive students keep an open enrollment; they are excluded from rosters and saves.");
    }

    private async Task<DateOnly> YearStartAsync(int schoolId, DateOnly date, CancellationToken ct)
    {
        var year = (await YearsAsync(schoolId, ct)).Find(y => y.Contains(date));
        return year?.StartDate ?? SchoolYearLabel.ForDate(date).DefaultStartDate;
    }

    // ---------------------------------------------------------------- StudentAttendance → AttendanceRecord

    private async Task ImportAttendanceAsync(int batchSize, CancellationToken ct)
    {
        var counts = _report.For(AttendanceTable);
        var importedRecordIds = MapValues(AttendanceTable);

        var withoutStudent = await _legacy.StudentAttendance.Where(a => a.StudentId == null).Select(a => a.AttendanceId).ToListAsync(ct);
        foreach (var id in withoutStudent)
        {
            counts.Read++;
            Skip(counts, AttendanceTable, IssueCategories.Orphan, id, "StudentID is NULL.");
        }

        var legacyStudentIds = await _legacy.StudentAttendance.Where(a => a.StudentId != null).Select(a => a.StudentId!.Value).Distinct().OrderBy(id => id).ToListAsync(ct);
        foreach (var batch in legacyStudentIds.Chunk(batchSize))
        {
            var rows = await _legacy.StudentAttendance.Where(a => a.StudentId != null && batch.Contains(a.StudentId.Value)).ToListAsync(ct);
            counts.Read += rows.Count;

            var newStudentIds = batch.Select(id => MappedId(StudentsTable, Key(id))).OfType<int>().ToList();
            var existing = await _target.AttendanceRecords.Where(r => newStudentIds.Contains(r.StudentId)).ToDictionaryAsync(r => (r.StudentId, r.Date), ct);

            var pendingMaps = new List<(string LegacyKey, AttendanceRecord Record)>();
            foreach (var group in rows.GroupBy(r => (r.StudentId!.Value, Date: r.AttendDate?.Date)))
            {
                var ordered = group.OrderByDescending(r => r.ModifiedDate ?? r.CreatedDate ?? DateTime.MinValue).ThenByDescending(r => r.AttendanceId).ToList();
                var winner = ordered[0];
                if (group.Key.Date is not null)
                {
                    foreach (var loser in ordered.Skip(1))
                    {
                        Skip(counts, AttendanceTable, IssueCategories.Duplicate, loser.AttendanceId, $"Student {group.Key.Item1} already has row {winner.AttendanceId} for {group.Key.Date:yyyy-MM-dd} (the most recently changed row is kept).");
                    }
                }
                else
                {
                    foreach (var row in ordered)
                    {
                        Skip(counts, AttendanceTable, IssueCategories.InvalidData, row.AttendanceId, "AttendDate is NULL.");
                    }

                    continue;
                }

                await ImportAttendanceRowAsync(winner, existing, importedRecordIds, pendingMaps, counts, ct);
            }

            // One round trip for the batch's new records, then their legacy maps (which need the new IDs).
            await _target.SaveChangesAsync(ct);
            foreach (var (legacyKey, record) in pendingMaps)
            {
                AddMap(AttendanceTable, legacyKey, record.Id);
            }

            await _target.SaveChangesAsync(ct);
            foreach (var entry in _target.ChangeTracker.Entries().Where(e => e.Entity is AttendanceRecord or LegacyIdMap).ToList())
            {
                entry.State = EntityState.Detached; // keep memory flat across batches
            }
        }
    }

    private async Task ImportAttendanceRowAsync(
        LegacyStudentAttendance row,
        Dictionary<(int StudentId, DateOnly Date), AttendanceRecord> existing,
        HashSet<int> importedRecordIds,
        List<(string LegacyKey, AttendanceRecord Record)> pendingMaps,
        TableCounts counts,
        CancellationToken ct)
    {
        var key = row.AttendanceId;
        if (MappedId(StudentsTable, Key(row.StudentId!.Value)) is not { } studentId)
        {
            Skip(counts, AttendanceTable, IssueCategories.Orphan, key, $"Student {row.StudentId} does not exist (or was skipped).");
            return;
        }

        var legacySchool = row.SchoolId ?? _legacyStudentSchool.GetValueOrDefault(row.StudentId.Value);
        if (legacySchool is null || MappedId(SchoolsTable, Key(legacySchool.Value)) is not { } schoolId)
        {
            Skip(counts, AttendanceTable, IssueCategories.Orphan, key, $"School {legacySchool?.ToString(CultureInfo.InvariantCulture) ?? "NULL"} does not exist.");
            return;
        }

        var date = DateOnly.FromDateTime(row.AttendDate!.Value);
        var code = await ResolveCodeAsync(row, counts, ct);
        if (code is null)
        {
            return;
        }

        var year = await YearForAsync(schoolId, date, ct);
        if (year is null)
        {
            Skip(counts, AttendanceTable, IssueCategories.Calendar, key, $"No school year at school {schoolId} contains {date:yyyy-MM-dd}, and the default year would overlap an existing one.");
            return;
        }

        var minutesLate = row.MinutesLate ?? 0;
        if (minutesLate < 0)
        {
            _report.Issue(AttendanceTable, IssueCategories.Adjusted, Key(key), $"MinutesLate {minutesLate} imported as 0.");
            minutesLate = 0;
        }

        if (row.Notes?.Length > AttendanceRecord.MaxNotesLength)
        {
            _report.Issue(AttendanceTable, IssueCategories.Adjusted, Key(key), $"Notes cut to {AttendanceRecord.MaxNotesLength} characters.");
        }

        var placement = new AttendancePlacement(schoolId, date, year.Id, year.FindTerm(date)?.Id);
        var imported = new ImportedAttendance(
            code.Id,
            row.IsAbsent ?? code.IsAbsent,
            row.IsExcused ?? code.IsExcused,
            minutesLate,
            row.Notes,
            LegacyActor(row.CreatedBy) ?? ImportActor,
            AsUtc(row.CreatedDate) ?? _now,
            LegacyActor(row.ModifiedBy),
            AsUtc(row.ModifiedDate));

        if (existing.TryGetValue((studentId, date), out var record))
        {
            if (!importedRecordIds.Contains(record.Id))
            {
                Skip(counts, AttendanceTable, IssueCategories.KeptNewSystem, key, $"Student {studentId} already has a record for {date:yyyy-MM-dd} entered in the new system.");
                return;
            }

            record.ReplaceImported(placement, imported);
            SetMap(AttendanceTable, Key(key), record.Id);
            counts.Updated++;
        }
        else
        {
            record = AttendanceRecord.Import(studentId, placement, imported);
            _target.AttendanceRecords.Add(record);
            existing[(studentId, date)] = record;
            pendingMaps.Add((Key(key), record));
            counts.Inserted++;
        }

        _touchedTotals.Add((studentId, year.Label));
    }

    private async Task<AttendanceCode?> ResolveCodeAsync(LegacyStudentAttendance row, TableCounts counts, CancellationToken ct)
    {
        var value = row.AttendCode?.Trim() ?? string.Empty;
        if (value.Length is 0 or > AttendanceCode.MaxCodeLength)
        {
            Skip(counts, AttendanceTable, IssueCategories.InvalidData, row.AttendanceId, $"Attendance code '{row.AttendCode}' is empty or too long.");
            return null;
        }

        if (_codes.TryGetValue(value, out var code))
        {
            return code;
        }

        // D-07: legacy accepted codes that don't exist. Keep the row (its stored flags) under an inactive placeholder.
        code = AttendanceCode.Create(value, "Unknown legacy code (imported)", row.IsAbsent ?? false, row.IsExcused ?? false, isActive: false);
        _target.AttendanceCodes.Add(code);
        await _target.SaveChangesAsync(ct);
        _codes[code.Code] = code;
        _report.Issue(AttendanceTable, IssueCategories.UnknownCode, value, "Not in the legacy code table; created as an inactive placeholder so its records are kept. Review it.");
        return code;
    }

    private async Task<List<SchoolYear>> YearsAsync(int schoolId, CancellationToken ct)
    {
        if (!_yearsBySchool.TryGetValue(schoolId, out var years))
        {
            years = await _target.SchoolYears.AsNoTracking().Include(y => y.Terms).Where(y => y.SchoolId == schoolId).ToListAsync(ct);
            _yearsBySchool[schoolId] = years;
        }

        return years;
    }

    private async Task<SchoolYear?> YearForAsync(int schoolId, DateOnly date, CancellationToken ct)
    {
        var years = await YearsAsync(schoolId, ct);
        var year = years.Find(y => y.Contains(date));
        if (year is not null)
        {
            return year;
        }

        // Same rule as a save for an uncovered date: default 1 Sep – 31 Aug year with one whole-year term (Q1).
        var created = SchoolYear.CreateDefault(schoolId, date);
        try
        {
            created.EnsureNoConflictWith(years);
        }
        catch (DomainException)
        {
            return null;
        }

        _target.SchoolYears.Add(created);
        await _target.SaveChangesAsync(ct);
        years.Add(created);
        _report.Issue(TermsTable, IssueCategories.Calendar, $"school {schoolId}", $"No legacy terms covered {date:yyyy-MM-dd}; created default school year {created.Label} with one whole-year term.");
        return created;
    }

    // ---------------------------------------------------------------- StudentAlerts → StudentAlert

    private async Task ImportAlertsAsync(CancellationToken ct)
    {
        var counts = _report.For(AlertsTable);
        var openChronic = (await _target.StudentAlerts
                .Where(a => a.AlertType == StudentAlert.ChronicAbsenceType && a.ResolvedAt == null)
                .Select(a => new { a.StudentId, a.SchoolYearLabel })
                .ToListAsync(ct))
            .Select(a => (a.StudentId, a.SchoolYearLabel))
            .ToHashSet();

        // Newest first, so if legacy has two open chronic alerts for a student and year, the newest is kept.
        foreach (var legacy in await _legacy.StudentAlerts.OrderByDescending(a => a.AlertDate).ThenByDescending(a => a.AlertId).ToListAsync(ct))
        {
            counts.Read++;
            var key = legacy.AlertId;
            if (MappedId(AlertsTable, Key(key)) is { } alertId)
            {
                var alert = await _target.StudentAlerts.SingleAsync(a => a.Id == alertId, ct);
                if (alert.IsOpen && AsUtc(legacy.ResolvedDate) is { } resolvedAt)
                {
                    alert.Resolve(LegacyActor(legacy.ResolvedBy) ?? ImportActor, resolvedAt);
                    counts.Updated++;
                }
                else
                {
                    counts.Unchanged++;
                }

                continue;
            }

            if (legacy.StudentId is not { } legacyStudent || MappedId(StudentsTable, Key(legacyStudent)) is not { } studentId)
            {
                Skip(counts, AlertsTable, IssueCategories.Orphan, key, $"Student {legacy.StudentId?.ToString(CultureInfo.InvariantCulture) ?? "NULL"} does not exist.");
                continue;
            }

            var legacySchool = legacy.SchoolId ?? _legacyStudentSchool.GetValueOrDefault(legacyStudent);
            if (legacySchool is null || MappedId(SchoolsTable, Key(legacySchool.Value)) is not { } schoolId)
            {
                Skip(counts, AlertsTable, IssueCategories.Orphan, key, "The alert's school does not exist.");
                continue;
            }

            SchoolYearLabel label;
            if (SchoolYearLabel.TryParse(legacy.SchoolYear?.Trim(), out var parsed))
            {
                label = parsed;
            }
            else if (legacy.AlertDate is { } alertDate)
            {
                label = SchoolYearLabel.ForDate(DateOnly.FromDateTime(alertDate));
            }
            else
            {
                Skip(counts, AlertsTable, IssueCategories.InvalidData, key, $"School year '{legacy.SchoolYear}' is not YYYY-YYYY and there is no alert date.");
                continue;
            }

            var type = string.IsNullOrWhiteSpace(legacy.AlertType) ? "UNKNOWN" : legacy.AlertType.Trim().ToUpperInvariant();
            var isOpen = legacy.ResolvedDate is null;
            if (isOpen && type == StudentAlert.ChronicAbsenceType && !openChronic.Add((studentId, label.ToString())))
            {
                Skip(counts, AlertsTable, IssueCategories.DuplicateAlert, key, $"Student {studentId} already has an open chronic-absence alert for {label}.");
                continue;
            }

            var imported = StudentAlert.Import(studentId, schoolId, Truncate(type, StudentAlert.MaxAlertTypeLength), label, legacy.AlertMessage ?? string.Empty, AsUtc(legacy.AlertDate) ?? _now, AsUtc(legacy.ResolvedDate), LegacyActor(legacy.ResolvedBy));
            _target.StudentAlerts.Add(imported);
            await _target.SaveChangesAsync(ct);
            AddMap(AlertsTable, Key(key), imported.Id);
            counts.Inserted++;
        }

        await _target.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- Totals

    private async Task RecalculateTotalsAsync(CancellationToken ct)
    {
        var calculator = new ChronicStatusCalculator(_target);
        var counts = _report.For("StudentAttendanceSummary (recalculated)");
        foreach (var byLabel in _touchedTotals.GroupBy(t => t.Label))
        {
            var label = SchoolYearLabel.Parse(byLabel.Key);
            foreach (var batch in byLabel.Select(t => t.StudentId).Chunk(500))
            {
                var totals = await calculator.CountTotalsAsync(batch, label, ct);
                var summaries = await _target.StudentAttendanceSummaries
                    .Where(s => batch.Contains(s.StudentId) && s.SchoolYearLabel == byLabel.Key)
                    .ToDictionaryAsync(s => s.StudentId, ct);
                foreach (var (studentId, studentTotals) in totals)
                {
                    counts.Read++;
                    if (summaries.TryGetValue(studentId, out var summary))
                    {
                        summary.Replace(studentTotals, _now);
                        counts.Updated++;
                    }
                    else
                    {
                        _target.StudentAttendanceSummaries.Add(StudentAttendanceSummary.Create(studentId, label, studentTotals, _now));
                        counts.Inserted++;
                    }
                }

                await _target.SaveChangesAsync(ct);
            }
        }
    }

    // ---------------------------------------------------------------- Helpers

    private async Task LoadMapsAsync(CancellationToken ct)
    {
        foreach (var map in await _target.LegacyIdMaps.AsNoTracking().ToListAsync(ct))
        {
            Maps(map.EntityType)[map.LegacyId] = map.NewId;
        }
    }

    private Dictionary<string, int> Maps(string table)
    {
        if (!_maps.TryGetValue(table, out var map))
        {
            map = [];
            _maps[table] = map;
        }

        return map;
    }

    private int? MappedId(string table, string legacyKey) => Maps(table).TryGetValue(legacyKey, out var id) ? id : null;

    private HashSet<int> MapValues(string table) => [.. Maps(table).Values];

    private void AddMap(string table, string legacyKey, int newId)
    {
        Maps(table)[legacyKey] = newId;
        _target.LegacyIdMaps.Add(new LegacyIdMap(table, legacyKey, newId));
    }

    /// <summary>Records which legacy row a record now comes from (after a re-run picked a different duplicate).</summary>
    private void SetMap(string table, string legacyKey, int newId)
    {
        if (MappedId(table, legacyKey) == newId)
        {
            return;
        }

        if (MappedId(table, legacyKey) is null)
        {
            AddMap(table, legacyKey, newId);
            return;
        }

        Maps(table)[legacyKey] = newId;
        _target.LegacyIdMaps.Update(new LegacyIdMap(table, legacyKey, newId));
    }

    private void Skip(TableCounts counts, string table, string category, object legacyKey, string message)
    {
        counts.Skipped++;
        _report.Issue(table, category, Convert.ToString(legacyKey, CultureInfo.InvariantCulture) ?? "?", message);
    }

    private static void Count(TableCounts counts, bool changed)
    {
        if (changed)
        {
            counts.Updated++;
        }
        else
        {
            counts.Unchanged++;
        }
    }

    private static void SetActive(School school, bool active)
    {
        if (active)
        {
            school.Activate();
        }
        else
        {
            school.Deactivate();
        }
    }

    private static void SetActive(Student student, bool active)
    {
        if (active)
        {
            student.Activate();
        }
        else
        {
            student.Deactivate();
        }
    }

    private static Actor? LegacyActor(string? userName) =>
        string.IsNullOrWhiteSpace(userName) ? null : new Actor(null, Truncate(userName.Trim(), Actor.MaxNameLength));

    private static DateTime? AsUtc(DateTime? value) =>
        value is { } v ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : null;

    private static string Truncate(string value, int maxLength) => value.Length > maxLength ? value[..maxLength] : value;

    private static string Key(int legacyId) => legacyId.ToString(CultureInfo.InvariantCulture);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dry run: every change was rolled back")]
    private partial void LogDryRun();
}
