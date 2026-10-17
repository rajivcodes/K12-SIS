using AttendanceApi.Application.Abstractions;
using AttendanceApi.Application.Common;
using AttendanceApi.Domain.Calendar;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Application.Admin;

public sealed record TermResponse(int Id, string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record SchoolYearResponse(int Id, int SchoolId, string Label, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<TermResponse> Terms);

public sealed record SchoolYearRequest(DateOnly? StartDate, DateOnly? EndDate);

public sealed record TermRequest(string? Name, DateOnly? StartDate, DateOnly? EndDate);

public sealed record NonInstructionalDayResponse(int Id, DateOnly Date, string Reason);

public sealed record NonInstructionalDayRequest(DateOnly? Date, string? Reason);

/// <summary>
/// School years, terms and non-instructional days (plan decision 7). Whenever terms change, existing
/// attendance records of that year are re-tagged with the term that now contains their date (BR-06).
/// </summary>
public sealed class SchoolCalendarAdmin
{
    private readonly IAttendanceDbContext _db;
    private readonly AccessGuard _access;
    private readonly AuditLog _audit;

    public SchoolCalendarAdmin(IAttendanceDbContext db, AccessGuard access, AuditLog audit)
    {
        _db = db;
        _access = access;
        _audit = audit;
    }

    public async Task<List<SchoolYearResponse>> ListYearsAsync(int schoolId, CancellationToken cancellationToken)
    {
        await PrepareAsync(schoolId, cancellationToken);
        var years = await _db.SchoolYears.AsNoTracking().Include(y => y.Terms)
            .Where(y => y.SchoolId == schoolId)
            .OrderByDescending(y => y.StartDate)
            .ToListAsync(cancellationToken);
        return years.Select(ToResponse).ToList();
    }

    public async Task<SchoolYearResponse> CreateYearAsync(int schoolId, SchoolYearRequest request, CancellationToken cancellationToken)
    {
        await PrepareAsync(schoolId, cancellationToken);
        var (start, end) = RequireDates(request.StartDate, request.EndDate);
        var year = SchoolYear.Create(schoolId, start, end);
        await EnsureNoConflictAsync(year, cancellationToken);

        _db.SchoolYears.Add(year);
        await _db.SaveChangesAsync(cancellationToken);
        _audit.Add("SchoolYearCreated", "SchoolYear", year.Id, new { schoolId, year.Label, start, end });
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(year);
    }

    public async Task<SchoolYearResponse> UpdateYearAsync(int schoolId, int yearId, SchoolYearRequest request, CancellationToken cancellationToken)
    {
        await PrepareAsync(schoolId, cancellationToken);
        var (start, end) = RequireDates(request.StartDate, request.EndDate);
        var year = await LoadYearAsync(schoolId, yearId, cancellationToken);

        var records = _db.AttendanceRecords.Where(r => r.SchoolYearId == yearId);
        if (await records.AnyAsync(r => r.Date < start || r.Date > end, cancellationToken))
        {
            throw new BusinessRuleException("Attendance outside the new dates", $"School year {year.Label} has attendance records outside {start:yyyy-MM-dd} – {end:yyyy-MM-dd}.", "RecordsOutsideYear");
        }

        var oldLabel = year.Label;
        year.ChangeDates(start, end);
        if (year.Label != oldLabel && await records.AnyAsync(cancellationToken))
        {
            throw new BusinessRuleException("School year label would change", $"The new start date changes the label from {oldLabel} to {year.Label}, but attendance is already recorded under {oldLabel}.", "LabelChangeWithRecords");
        }

        await EnsureNoConflictAsync(year, cancellationToken);
        _audit.Add("SchoolYearUpdated", "SchoolYear", year.Id, new { year.Label, start, end });
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(year);
    }

    public async Task<SchoolYearResponse> AddTermAsync(int schoolId, int yearId, TermRequest request, CancellationToken cancellationToken)
    {
        await PrepareAsync(schoolId, cancellationToken);
        var (start, end) = RequireDates(request.StartDate, request.EndDate);
        var year = await LoadYearAsync(schoolId, yearId, cancellationToken);
        var term = year.AddTerm(request.Name ?? string.Empty, start, end);
        await SaveAndRetagAsync(year, "TermAdded", new { term.Name, start, end }, cancellationToken);
        return ToResponse(year);
    }

    public async Task<SchoolYearResponse> UpdateTermAsync(int schoolId, int yearId, int termId, TermRequest request, CancellationToken cancellationToken)
    {
        await PrepareAsync(schoolId, cancellationToken);
        var (start, end) = RequireDates(request.StartDate, request.EndDate);
        var year = await LoadYearAsync(schoolId, yearId, cancellationToken);
        var term = year.Terms.SingleOrDefault(t => t.Id == termId) ?? throw new NotFoundException($"Term {termId} does not exist in school year {yearId}.");
        year.ChangeTerm(term, request.Name ?? string.Empty, start, end);
        await SaveAndRetagAsync(year, "TermUpdated", new { termId, term.Name, start, end }, cancellationToken);
        return ToResponse(year);
    }

    public async Task<SchoolYearResponse> DeleteTermAsync(int schoolId, int yearId, int termId, CancellationToken cancellationToken)
    {
        await PrepareAsync(schoolId, cancellationToken);
        var year = await LoadYearAsync(schoolId, yearId, cancellationToken);
        var term = year.Terms.SingleOrDefault(t => t.Id == termId) ?? throw new NotFoundException($"Term {termId} does not exist in school year {yearId}.");
        year.RemoveTerm(term);
        await SaveAndRetagAsync(year, "TermDeleted", new { termId, term.Name }, cancellationToken);
        return ToResponse(year);
    }

    public async Task<List<NonInstructionalDayResponse>> ListNonInstructionalDaysAsync(int schoolId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        await PrepareAsync(schoolId, cancellationToken);
        return await _db.NonInstructionalDays
            .Where(d => d.SchoolId == schoolId && (from == null || d.Date >= from) && (to == null || d.Date <= to))
            .OrderBy(d => d.Date)
            .Select(d => new NonInstructionalDayResponse(d.Id, d.Date, d.Reason))
            .ToListAsync(cancellationToken);
    }

    public async Task<NonInstructionalDayResponse> AddNonInstructionalDayAsync(int schoolId, NonInstructionalDayRequest request, CancellationToken cancellationToken)
    {
        await PrepareAsync(schoolId, cancellationToken);
        var date = request.Date ?? throw new RequestValidationException("date is required.");
        if (await _db.NonInstructionalDays.AnyAsync(d => d.SchoolId == schoolId && d.Date == date, cancellationToken))
        {
            throw new ConflictException($"{date:yyyy-MM-dd} is already a non-instructional day for this school.");
        }

        var day = NonInstructionalDay.Create(schoolId, date, request.Reason ?? string.Empty);
        _db.NonInstructionalDays.Add(day);
        await _db.SaveChangesAsync(cancellationToken);
        _audit.Add("NonInstructionalDayAdded", "NonInstructionalDay", day.Id, new { schoolId, date, day.Reason });
        await _db.SaveChangesAsync(cancellationToken);
        return new NonInstructionalDayResponse(day.Id, day.Date, day.Reason);
    }

    public async Task DeleteNonInstructionalDayAsync(int schoolId, int dayId, CancellationToken cancellationToken)
    {
        await PrepareAsync(schoolId, cancellationToken);
        var day = await _db.NonInstructionalDays.SingleOrDefaultAsync(d => d.Id == dayId && d.SchoolId == schoolId, cancellationToken)
            ?? throw new NotFoundException($"Non-instructional day {dayId} does not exist for school {schoolId}.");
        _db.NonInstructionalDays.Remove(day);
        _audit.Add("NonInstructionalDayDeleted", "NonInstructionalDay", day.Id, new { schoolId, day.Date });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task PrepareAsync(int schoolId, CancellationToken cancellationToken)
    {
        await _access.EnsureActiveUserAsync(cancellationToken);
        await AdminLookups.EnsureSchoolExistsAsync(_db, schoolId, cancellationToken);
    }

    private async Task<SchoolYear> LoadYearAsync(int schoolId, int yearId, CancellationToken cancellationToken) =>
        await _db.SchoolYears.Include(y => y.Terms).SingleOrDefaultAsync(y => y.Id == yearId && y.SchoolId == schoolId, cancellationToken)
        ?? throw new NotFoundException($"School year {yearId} does not exist for school {schoolId}.");

    private async Task EnsureNoConflictAsync(SchoolYear year, CancellationToken cancellationToken)
    {
        var others = await _db.SchoolYears.AsNoTracking().Where(y => y.SchoolId == year.SchoolId && y.Id != year.Id).ToListAsync(cancellationToken);
        year.EnsureNoConflictWith(others);
    }

    /// <summary>
    /// Saves a term change and moves every record of the year to the term containing its date, in one
    /// transaction. Records are first detached from all terms so a deleted term can be removed.
    /// </summary>
    private async Task SaveAndRetagAsync(SchoolYear year, string action, object details, CancellationToken cancellationToken)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            await _db.AttendanceRecords.Where(r => r.SchoolYearId == year.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.TermId, (int?)null), ct);
            _audit.Add(action, "SchoolYear", year.Id, details);
            await _db.SaveChangesAsync(ct);
            foreach (var term in year.Terms)
            {
                var (termId, start, end) = (term.Id, term.StartDate, term.EndDate);
                await _db.AttendanceRecords.Where(r => r.SchoolYearId == year.Id && r.Date >= start && r.Date <= end)
                    .ExecuteUpdateAsync(u => u.SetProperty(r => r.TermId, termId), ct);
            }

            await transaction.CommitAsync(ct);
        }, cancellationToken);
    }

    private static (DateOnly Start, DateOnly End) RequireDates(DateOnly? start, DateOnly? end) =>
        start is { } s && end is { } e ? (s, e) : throw new RequestValidationException("startDate and endDate are required.");

    private static SchoolYearResponse ToResponse(SchoolYear y) => new(
        y.Id, y.SchoolId, y.Label, y.StartDate, y.EndDate,
        y.Terms.OrderBy(t => t.StartDate).Select(t => new TermResponse(t.Id, t.Name, t.StartDate, t.EndDate)).ToList());
}
