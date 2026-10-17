using AttendanceApi.LegacyImport.Import;

namespace AttendanceApi.Api.IntegrationTests.LegacyImport;

public class LegacyCalendarBuilderTests
{
    private static LegacyTermInput Term(int id, string start, string end) =>
        new(id, "T" + id, DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public void Terms_are_grouped_into_default_school_years()
    {
        var plan = LegacyCalendarBuilder.Build([Term(1, "2026-09-02", "2026-12-18"), Term(2, "2027-01-05", "2027-06-11")]);

        var year = Assert.Single(plan.Years);
        Assert.Equal(("2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31)), (year.Label.ToString(), year.StartDate, year.EndDate));
        Assert.Equal([1, 2], year.Terms.Select(t => t.LegacyTermId));
        Assert.Empty(plan.Rejected);
    }

    [Fact]
    public void A_term_starting_in_August_belongs_to_the_year_it_mostly_covers_and_years_do_not_overlap()
    {
        var plan = LegacyCalendarBuilder.Build(
        [
            Term(1, "2025-08-20", "2025-12-19"),
            Term(2, "2026-01-06", "2026-06-12"),
            Term(3, "2026-08-19", "2026-12-18"),
        ]);

        Assert.Collection(
            plan.Years,
            y => Assert.Equal(("2025-2026", new DateOnly(2025, 8, 20), new DateOnly(2026, 8, 18)), (y.Label.ToString(), y.StartDate, y.EndDate)),
            y => Assert.Equal(("2026-2027", new DateOnly(2026, 8, 19), new DateOnly(2027, 8, 18)), (y.Label.ToString(), y.StartDate, y.EndDate)));
    }

    [Fact]
    public void Overlapping_terms_and_terms_past_the_year_end_are_rejected()
    {
        var plan = LegacyCalendarBuilder.Build(
        [
            Term(1, "2026-09-01", "2026-12-18"),
            Term(2, "2026-12-01", "2027-01-15"), // overlaps term 1
            Term(3, "2027-01-20", "2027-09-30"), // ends after 31 Aug 2027
        ]);

        Assert.Equal([1], plan.Years.Single().Terms.Select(t => t.LegacyTermId));
        Assert.Equal([2, 3], plan.Rejected.Select(r => r.Term.LegacyTermId).Order());
    }

    [Fact]
    public void No_terms_gives_no_years()
    {
        Assert.Empty(LegacyCalendarBuilder.Build([]).Years);
    }
}
