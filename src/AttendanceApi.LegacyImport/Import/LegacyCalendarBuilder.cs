using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Common;

namespace AttendanceApi.LegacyImport.Import;

public sealed record LegacyTermInput(int LegacyTermId, string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record PlannedYear(SchoolYearLabel Label, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<LegacyTermInput> Terms);

public sealed record RejectedTerm(LegacyTermInput Term, string Reason);

public sealed record CalendarPlan(IReadOnlyList<PlannedYear> Years, IReadOnlyList<RejectedTerm> Rejected);

/// <summary>
/// Turns one school's legacy SchoolTerms (which have no school-year rows) into school years (plan §8 step 2).
/// </summary>
/// <remarks>
/// A term belongs to the school year containing its midpoint (1 September rule), so a term starting on
/// 20 August still belongs to the year it mostly covers. A year starts on 1 September or on its earliest
/// term, whichever is earlier, and ends the day before the next year starts or on 31 August, never
/// lasting more than a year. Terms that overlap an earlier term or don't fit their year are rejected.
/// </remarks>
public static class LegacyCalendarBuilder
{
    public static CalendarPlan Build(IEnumerable<LegacyTermInput> terms)
    {
        var rejected = new List<RejectedTerm>();
        var groups = terms
            .OrderBy(t => t.StartDate).ThenBy(t => t.LegacyTermId)
            .GroupBy(t => SchoolYearLabel.ForDate(Midpoint(t)))
            .OrderBy(g => g.Key.StartYear)
            .ToList();

        var starts = groups.ToDictionary(g => g.Key, g => AttendanceDates.Min(g.Key.DefaultStartDate, g.Min(t => t.StartDate)));
        var years = new List<PlannedYear>();
        foreach (var group in groups)
        {
            var start = starts[group.Key];
            var end = group.Key.DefaultEndDate;
            var next = new SchoolYearLabel(group.Key.StartYear + 1);
            if (starts.TryGetValue(next, out var nextStart) && nextStart <= end)
            {
                end = nextStart.AddDays(-1);
            }

            end = AttendanceDates.Min(end, start.AddYears(1).AddDays(-1));

            var accepted = new List<LegacyTermInput>();
            foreach (var term in group)
            {
                if (term.EndDate > end)
                {
                    rejected.Add(new RejectedTerm(term, $"Term ends after school year {group.Key} ({end:yyyy-MM-dd})."));
                }
                else if (accepted.Exists(a => AttendanceDates.Overlaps(a.StartDate, a.EndDate, term.StartDate, term.EndDate)))
                {
                    rejected.Add(new RejectedTerm(term, "Term overlaps an earlier term of the same school year."));
                }
                else
                {
                    accepted.Add(term);
                }
            }

            years.Add(new PlannedYear(group.Key, start, end, accepted));
        }

        return new CalendarPlan(years, rejected);
    }

    private static DateOnly Midpoint(LegacyTermInput t) =>
        t.StartDate.AddDays((t.EndDate.DayNumber - t.StartDate.DayNumber) / 2);
}
