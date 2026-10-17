using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Calendar;

/// <summary>
/// One school's school year, with its terms (plan decision 7). The label comes from the start date's
/// calendar year, so a year starting in August or September 2026 is <c>2026-2027</c> at every school.
/// </summary>
public sealed class SchoolYear
{
    public const string DefaultTermName = "Full year";

    private readonly List<Term> _terms = [];

    private SchoolYear()
    {
        Label = string.Empty;
    }

    private SchoolYear(int schoolId, DateOnly startDate, DateOnly endDate)
    {
        SchoolId = schoolId;
        SetDates(startDate, endDate);
        Label = YearLabel.ToString();
    }

    public int Id { get; private set; }

    public int SchoolId { get; private set; }

    /// <summary>The <c>YYYY-YYYY</c> label, unique per school.</summary>
    public string Label { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public IReadOnlyCollection<Term> Terms => _terms.AsReadOnly();

    public SchoolYearLabel YearLabel => new(StartDate.Year);

    /// <summary>Creates a school year with admin-chosen dates. It must last at most one year.</summary>
    public static SchoolYear Create(int schoolId, DateOnly startDate, DateOnly endDate) =>
        new(schoolId, startDate, endDate);

    /// <summary>
    /// Creates the default 1 Sep – 31 Aug school year that contains <paramref name="date"/>, with one term
    /// covering the whole year (Q1). Used when a save arrives for a date no school year covers.
    /// </summary>
    public static SchoolYear CreateDefault(int schoolId, DateOnly date)
    {
        var label = SchoolYearLabel.ForDate(date);
        var year = new SchoolYear(schoolId, label.DefaultStartDate, label.DefaultEndDate);
        year.AddTerm(DefaultTermName, year.StartDate, year.EndDate);
        return year;
    }

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    public bool Overlaps(SchoolYear other) =>
        AttendanceDates.Overlaps(StartDate, EndDate, other.StartDate, other.EndDate);

    /// <summary>The term containing <paramref name="date"/>, or null if the date is outside every term.</summary>
    public Term? FindTerm(DateOnly date) => _terms.Find(t => t.Contains(date));

    /// <summary>
    /// Checks that this year can sit alongside a school's existing years: same school, no overlapping dates
    /// and no duplicate label.
    /// </summary>
    public void EnsureNoConflictWith(IEnumerable<SchoolYear> existingYears)
    {
        foreach (var other in existingYears)
        {
            if (ReferenceEquals(other, this) || (other.Id != 0 && other.Id == Id) || other.SchoolId != SchoolId)
            {
                continue;
            }

            if (other.Label == Label)
            {
                throw new DomainException($"School year {Label} already exists for this school.", DomainErrorKind.Conflict);
            }

            if (Overlaps(other))
            {
                throw new DomainException($"School year {Label} overlaps school year {other.Label}.", DomainErrorKind.Conflict);
            }
        }
    }

    public void ChangeDates(DateOnly startDate, DateOnly endDate)
    {
        var outside = _terms.Find(t => t.StartDate < startDate || t.EndDate > endDate);
        if (outside is not null)
        {
            throw new DomainException($"Term '{outside.Name}' would fall outside the school year.");
        }

        SetDates(startDate, endDate);
        Label = YearLabel.ToString();
    }

    public Term AddTerm(string name, DateOnly startDate, DateOnly endDate)
    {
        var term = new Term(this, name, startDate, endDate);
        EnsureTermFits(term, startDate, endDate);
        _terms.Add(term);
        return term;
    }

    public void ChangeTerm(Term term, string name, DateOnly startDate, DateOnly endDate)
    {
        if (!_terms.Contains(term))
        {
            throw new DomainException("The term does not belong to this school year.");
        }

        EnsureTermFits(term, startDate, endDate);
        term.Change(name, startDate, endDate);
    }

    public void RemoveTerm(Term term) => _terms.Remove(term);

    private void SetDates(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new DomainException("School year end date must be on or after its start date.");
        }

        if (endDate > startDate.AddYears(1).AddDays(-1))
        {
            throw new DomainException("A school year cannot be longer than one year.");
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    private void EnsureTermFits(Term term, DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new DomainException("Term end date must be on or after its start date.");
        }

        if (startDate < StartDate || endDate > EndDate)
        {
            throw new DomainException($"Term must fall inside school year {Label} ({StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}).");
        }

        var clash = _terms.Find(t => !ReferenceEquals(t, term)
            && AttendanceDates.Overlaps(startDate, endDate, t.StartDate, t.EndDate));
        if (clash is not null)
        {
            throw new DomainException($"Term overlaps term '{clash.Name}'.", DomainErrorKind.Conflict);
        }
    }
}
