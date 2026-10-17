using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Tests.Calendar;

public class SchoolYearTests
{
    [Theory]
    [InlineData(2026, 9, 1, 2026)]
    [InlineData(2027, 8, 31, 2026)]
    [InlineData(2026, 8, 31, 2025)]
    public void CreateDefault_builds_the_1_Sep_to_31_Aug_year_containing_the_date(int y, int m, int d, int startYear)
    {
        var date = new DateOnly(y, m, d);

        var year = SchoolYear.CreateDefault(schoolId: 3, date);

        Assert.Equal(3, year.SchoolId);
        Assert.Equal(new DateOnly(startYear, 9, 1), year.StartDate);
        Assert.Equal(new DateOnly(startYear + 1, 8, 31), year.EndDate);
        Assert.Equal($"{startYear}-{startYear + 1}", year.Label);
        Assert.True(year.Contains(date));
    }

    [Fact]
    public void CreateDefault_adds_one_term_covering_the_whole_year()
    {
        var year = SchoolYear.CreateDefault(1, new DateOnly(2026, 10, 12));

        var term = Assert.Single(year.Terms);
        Assert.Equal(SchoolYear.DefaultTermName, term.Name);
        Assert.Equal(year.StartDate, term.StartDate);
        Assert.Equal(year.EndDate, term.EndDate);
    }

    [Fact]
    public void Contains_is_inclusive_at_both_ends()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 8, 17), new DateOnly(2027, 6, 4));

        Assert.False(year.Contains(new DateOnly(2026, 8, 16)));
        Assert.True(year.Contains(new DateOnly(2026, 8, 17)));
        Assert.True(year.Contains(new DateOnly(2027, 6, 4)));
        Assert.False(year.Contains(new DateOnly(2027, 6, 5)));
    }

    [Fact]
    public void Label_comes_from_the_start_dates_calendar_year()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 8, 17), new DateOnly(2027, 6, 4));

        Assert.Equal("2026-2027", year.Label);
        Assert.Equal(new SchoolYearLabel(2026), year.YearLabel);
    }

    [Fact]
    public void Create_rejects_end_before_start()
    {
        Assert.Throws<DomainException>(() => SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 31)));
    }

    [Fact]
    public void Create_allows_exactly_one_year_but_not_longer()
    {
        var oneYear = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31));
        Assert.Equal(new DateOnly(2027, 8, 31), oneYear.EndDate);

        Assert.Throws<DomainException>(() => SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 9, 1)));
    }

    [Fact]
    public void FindTerm_returns_the_term_containing_the_date_or_null()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31));
        var fall = year.AddTerm("Fall", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 18));
        var spring = year.AddTerm("Spring", new DateOnly(2027, 1, 5), new DateOnly(2027, 6, 11));

        Assert.Same(fall, year.FindTerm(new DateOnly(2026, 12, 18)));
        Assert.Same(spring, year.FindTerm(new DateOnly(2027, 1, 5)));
        Assert.Null(year.FindTerm(new DateOnly(2026, 12, 25)));
        Assert.Null(year.FindTerm(new DateOnly(2027, 7, 1)));
    }

    [Fact]
    public void AddTerm_rejects_a_term_outside_the_year()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));

        Assert.Throws<DomainException>(() => year.AddTerm("Early", new DateOnly(2026, 8, 31), new DateOnly(2026, 12, 18)));
        Assert.Throws<DomainException>(() => year.AddTerm("Late", new DateOnly(2027, 1, 5), new DateOnly(2027, 7, 1)));
    }

    [Fact]
    public void AddTerm_rejects_overlapping_terms_including_a_shared_boundary_day()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31));
        year.AddTerm("Fall", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 18));

        Assert.Throws<DomainException>(() => year.AddTerm("Spring", new DateOnly(2026, 12, 18), new DateOnly(2027, 6, 11)));
    }

    [Fact]
    public void AddTerm_rejects_end_before_start()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31));

        Assert.Throws<DomainException>(() => year.AddTerm("Bad", new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void ChangeTerm_can_move_a_term_but_not_onto_another()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31));
        var fall = year.AddTerm("Fall", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 18));
        year.AddTerm("Spring", new DateOnly(2027, 1, 5), new DateOnly(2027, 6, 11));

        year.ChangeTerm(fall, "Autumn", new DateOnly(2026, 9, 2), new DateOnly(2026, 12, 19));
        Assert.Equal("Autumn", fall.Name);
        Assert.Equal(new DateOnly(2026, 12, 19), fall.EndDate);

        Assert.Throws<DomainException>(() => year.ChangeTerm(fall, "Autumn", new DateOnly(2026, 9, 2), new DateOnly(2027, 1, 5)));
    }

    [Fact]
    public void ChangeDates_rejects_dates_that_would_leave_a_term_outside()
    {
        var year = SchoolYear.CreateDefault(1, new DateOnly(2026, 10, 1));

        Assert.Throws<DomainException>(() => year.ChangeDates(new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30)));
    }

    [Fact]
    public void ChangeDates_updates_the_label()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));

        year.ChangeDates(new DateOnly(2027, 8, 16), new DateOnly(2028, 6, 9));

        Assert.Equal("2027-2028", year.Label);
    }

    [Fact]
    public void EnsureNoConflictWith_rejects_an_overlapping_year_at_the_same_school()
    {
        var existing = SchoolYear.Create(1, new DateOnly(2026, 8, 17), new DateOnly(2027, 6, 4)).WithId(10);
        var candidate = SchoolYear.Create(1, new DateOnly(2027, 6, 4), new DateOnly(2028, 6, 1));

        Assert.Throws<DomainException>(() => candidate.EnsureNoConflictWith([existing]));
    }

    [Fact]
    public void EnsureNoConflictWith_rejects_a_duplicate_label_even_without_overlap()
    {
        var existing = SchoolYear.Create(1, new DateOnly(2026, 1, 5), new DateOnly(2026, 6, 30)).WithId(10);
        var candidate = SchoolYear.Create(1, new DateOnly(2026, 8, 17), new DateOnly(2027, 6, 4));

        Assert.Throws<DomainException>(() => candidate.EnsureNoConflictWith([existing]));
    }

    [Fact]
    public void EnsureNoConflictWith_ignores_other_schools_and_itself()
    {
        var otherSchool = SchoolYear.Create(2, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31)).WithId(10);
        var adjacent = SchoolYear.Create(1, new DateOnly(2025, 9, 1), new DateOnly(2026, 8, 31)).WithId(11);
        var candidate = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31)).WithId(12);

        candidate.EnsureNoConflictWith([otherSchool, adjacent, candidate]);
    }
}
