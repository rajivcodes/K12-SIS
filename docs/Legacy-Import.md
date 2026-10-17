# Legacy import tool

`src/AttendanceApi.LegacyImport` copies the legacy VB6/SQL Server data into the new database (plan §8). It connects to the legacy database **by connection string only** and reads it with LINQ through a read-only EF Core model; no legacy code or stored procedure is used.

## Before the first run: check the assumed legacy schema

The legacy repository did not include `Schools` or `SchoolTerms`, and some column names are inferred (analysis §1.1, gap G-07). The shape the tool expects is in [`Legacy/assumed-legacy-schema.sql`](../src/AttendanceApi.LegacyImport/Legacy/assumed-legacy-schema.sql). Compare it with the real database; if a name differs, change the mapping in `Legacy/LegacyDbContext.cs`.

## Run

```bash
# 1. Dry run: full report, every data change rolled back
#    (--apply-migrations still creates/updates the schema; that part is not rolled back)
dotnet run --project src/AttendanceApi.LegacyImport -- \
  --legacy "<legacy connection string>" --target "<new database connection string>" \
  --apply-migrations --dry-run --report import-dry-run.md

# 2. Real run (safe to repeat until cut-over)
dotnet run --project src/AttendanceApi.LegacyImport -- --legacy "..." --target "..." --report import.md
```

Connection strings can also come from user-secrets or the `ConnectionStrings__Legacy` / `ConnectionStrings__AttendanceDb` environment variables. Exit codes: `0` done (see the report), `1` failed with nothing committed, `2` missing arguments.

## What it does

The whole run is **one transaction** (schema migrations, if requested, run before it and are kept even in a dry run). Every imported row is recorded in `LegacyIdMap`, so a second run refreshes earlier imports instead of duplicating them. Records entered in the new system, and imported records a user has since corrected, are never overwritten; they are listed in the report. Each run adds an `AuditEntry`.

| Legacy | New | Rules |
|---|---|---|
| Schools | School + SchoolAttendanceSettings | `AbsenceAlertThreshold` becomes the count threshold (default 10, method Count). Name and active flag are refreshed on re-runs; settings are only set on first import. |
| SchoolTerms | SchoolYear + Term | A term belongs to the year containing its midpoint. A year starts on 1 Sep or its earliest term (if earlier) and ends the day before the next year or on 31 Aug. Overlapping terms, terms past the year end, orphans and missing dates are reported and skipped. |
| AttendanceCodes | AttendanceCode | The standard set is seeded first if the table is empty. Legacy codes not in it are imported as-is and reported; flag differences are reported (standard kept). |
| Students | Student + StudentEnrollment | One open enrollment at the current school, starting at the school-year start of the student's first legacy attendance (else `CreatedDate`). Rows without a name are skipped. |
| StudentAttendance | AttendanceRecord | **Stored absent/excused flags and the legacy audit user and timestamps are kept.** Year and term come from the school's calendar (a default year is created if none covers the date). Duplicates per student and day: the most recently changed row wins. Unknown codes (D-07) get an inactive placeholder code. Negative minutes become 0. |
| StudentAlerts | StudentAlert | Type, message and resolution kept. A second open chronic alert for the same student and year is skipped. |
| StudentAttendanceSummary | (intentionally not read) | **Recalculated** from the imported records per school year, because the legacy totals are wrong (D-01, D-03, D-08, D-09). |

Legacy `DATETIME` values have no time zone; they are stored as UTC unchanged (reported as an assumption).

## Tests

`tests/AttendanceApi.Api.IntegrationTests/LegacyImport` builds a legacy database from the assumed schema plus [sample data with the known defects](../tests/AttendanceApi.Api.IntegrationTests/LegacyImport/legacy-sample-data.sql), imports it, re-runs it, and checks a dry run changes nothing.
