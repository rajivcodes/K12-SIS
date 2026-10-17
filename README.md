# AttendanceApi

A .NET 8 Web API that replaces the daily-attendance module of the legacy VB6 + SQL Server K-12 Student Information System.

The legacy system is **reference material only**. This solution has no project references, copied code, shared files, stored procedures or dependency on any legacy object. The `AttendanceApi.LegacyImport` tool is the only component that touches the legacy database, and only through a connection string with its own queries.

Background documents (copied from the legacy repository):

* [docs/Legacy-System-Analysis.md](docs/Legacy-System-Analysis.md): how the legacy system works, its business rules (BR-xx), defects (D-xx) and gaps (G-xx).
* [docs/Implementation-Plan.md](docs/Implementation-Plan.md): the approved plan this solution follows.

> **Status:** Phase 2 of 7 (domain model and rules). See [Delivery status](#delivery-status).

---

## Overview

| Project | Purpose |
|---|---|
| `src/AttendanceApi.Domain` | Entities, value objects (SchoolYear, AttendanceDate) and rules (ChronicAbsenceEvaluator, AttendanceRowValidator). No dependencies. |
| `src/AttendanceApi.Application` | Use cases (save daily attendance, history, chronic status, admin settings), DTOs, request validation, and the interfaces `ICurrentUser`, `IClock`, `INotificationSender`. |
| `src/AttendanceApi.Infrastructure` | EF Core 8 DbContext, entity configurations, migrations, seed data, current user from the JWT, system clock. |
| `src/AttendanceApi.Api` | ASP.NET Core controllers, JWT auth, ProblemDetails, Swagger, health checks, logging. |
| `src/AttendanceApi.LegacyImport` | Console tool that reads the legacy database and writes the new schema. Safe to run again. |
| `tests/AttendanceApi.Domain.Tests` | xUnit unit tests for the domain rules. |
| `tests/AttendanceApi.Application.Tests` | xUnit tests for the use cases. |
| `tests/AttendanceApi.Api.IntegrationTests` | `WebApplicationFactory` + Testcontainers SQL Server. |

Dependencies point inward: Api → Application → Domain, with Infrastructure → Application/Domain.

## Running locally

### Prerequisites

* A .NET SDK that can build `net8.0` (the .NET 8 SDK, or a newer SDK with the .NET 8 runtime installed). All projects target `net8.0`.
* Docker Desktop (Linux containers) for SQL Server, `docker compose` and the integration tests.
* `dotnet-ef` is pinned to 8.0.x as a **local tool**. Run `dotnet tool restore` once, then use `dotnet ef ...` from the repository root.

### Build and test

```bash
dotnet build AttendanceApi.sln
```

```bash
dotnet test AttendanceApi.sln
```

The integration tests start their own SQL Server container through Testcontainers, so Docker must be running.

### Option 1: everything in Docker

1. Copy `.env.example` to `.env` and fill in `MSSQL_SA_PASSWORD`, `JWT_SIGNING_KEY` (at least 32 random bytes) and the seed admin credentials. `.env` is git-ignored.
2. Start SQL Server and the API:

```bash
docker compose up --build
```

The API listens on `http://localhost:8080`; health check at `/health`.

### Option 2: API on the host, SQL Server in Docker

```bash
docker compose up -d sqlserver
```

Store the secrets with user-secrets (never in `appsettings*.json`):

```bash
dotnet user-secrets --project src/AttendanceApi.Api set "ConnectionStrings:AttendanceDb" "Server=localhost,1433;Database=AttendanceApi;User Id=sa;Password=<your SA password>;TrustServerCertificate=True"
```

```bash
dotnet user-secrets --project src/AttendanceApi.Api set "Jwt:SigningKey" "<at least 32 random bytes, base64>"
```

```bash
dotnet user-secrets --project src/AttendanceApi.Api set "SeedAdmin:UserName" "admin"
```

```bash
dotnet user-secrets --project src/AttendanceApi.Api set "SeedAdmin:Password" "<initial admin password>"
```

```bash
dotnet run --project src/AttendanceApi.Api
```

The API listens on `http://localhost:5080`.

### Configuration reference

| Key | Default | Notes |
|---|---|---|
| `ConnectionStrings:AttendanceDb` | none | SQL Server connection string. Secret. |
| `Jwt:SigningKey` | none | HMAC-SHA256 key. Secret; user-secrets or environment only. |
| `Jwt:Issuer` / `Jwt:Audience` | `AttendanceApi` | |
| `Jwt:AccessTokenMinutes` | `60` | No refresh tokens in v1. |
| `SeedAdmin:*` | none | First admin account, created only if no admin exists. |

---

## Key architectural decisions

From sections 1 and 11 of the [implementation plan](docs/Implementation-Plan.md).

| # | Decision | Rationale |
|---|---|---|
| 1 | **Clean cut-over** to a new database owned by EF Core (code-first + migrations); legacy data arrives through a re-runnable import tool; VB6 is retired once the API is live. | Removes every dependency on legacy objects whose real production versions are unknown (G-01). |
| 2 | **All queries in LINQ**; stored procedures only if a real need appears. | Keeps business logic testable in C# instead of hidden in T-SQL, where D-01 to D-04 lived. |
| 3 | **Standard K-12 attendance code set seeded** only if the code table is empty; legacy codes not in the seed are imported as-is and reported. | The legacy code list is unknown (G-02); codes are data, so admins can change them. |
| 4 | **Both chronic-absence methods are calculated**: count (absences ≥ threshold, default 10) and rate (absences ÷ enrolled instructional days ≥ 10%). | The intended definition is unclear (G-03); returning both lets schools compare and choose. |
| 5 | **Per-school `ChronicMethod` setting** (Count, Rate or Either, default Count) decides `isChronicallyAbsent` and the alert. | Keeps legacy behaviour by default while allowing schools to move to the common US rate definition. |
| 6 | **Absences combined across all schools** in the database for the school year; responses list the schools included and note that outside data is unavailable. | Fixes D-08 (transfer overwrote totals) and answers G-10 transparently. |
| 7 | **Legacy counting rules kept**: every `IsAbsent` day counts, excused included; tardies and minutes late don't; flags copied onto the record at save time. | Preserves BR-03 and BR-08 so results match what users expect. |
| 8 | **Totals counted per school year**, recalculated from the records, never incremented. | Fixes D-03 (all-years count) and D-09 (cache drift). |
| 9 | **One SchoolYear per school** with Terms inside it, default 1 Sep – 31 Aug, admin-editable; a record's year and term come from its school's calendar. | The 1 September rule may not hold for every school (G-06). |
| 10 | **Auto-create the default 1 Sep – 31 Aug school year** when a save arrives for a date no SchoolYear covers. | Saves never fail just because the calendar hasn't been set up; an admin can correct it later. |
| 11 | **Stored totals and CHRONIC_ABSENCE alerts kept**, at most one open alert per student per year (filtered unique index). | Preserves BR-09, enforced by the database rather than by procedure logic. |
| 12 | **Skip only the bad row**, save the rest, log each skip to `AttendanceSubmissionIssue`, return skips in the response. | One bad row no longer silently corrupts or blocks the whole day (D-02, D-07, D-11). |
| 13 | **Notifications behind `INotificationSender`**, no-op implementation for now. | Email transport is not decided; the save flow doesn't depend on it. |
| 14 | **Per-school date rules**: allow future dates, allow weekends, allow outside a term, back-date limit; a disallowed date returns 422 for the whole batch. | Replaces the missing validation of D-11 and the fixed "today" of D-13 with explicit admin policy. |
| 15 | **One database transaction per save**, unique (StudentId, Date) index, retry once on a key collision. | Fixes D-04 (partial saves) and D-06 (duplicate rows). |
| 16 | **JWT bearer tokens issued by the API itself** (login endpoint, `AppUser` table, `PasswordHasher`, HMAC-SHA256, signing key from configuration/user-secrets). | Every write records the real user instead of a shared SQL login (BR-11, G-09). |
| 17 | **No refresh tokens in v1**; access tokens last 60 minutes; failed logins are rate-limited. | Keeps the first version simple; users log in again when the token expires. |
| 18 | **Roles Admin, AttendanceClerk, Viewer**; non-admins limited to their assigned schools (`AppUserSchool`), 403 otherwise. | Least privilege; answers the unknown role model (G-09). |
| 19 | **ASP.NET Core controllers** for endpoints. | Approved choice in the plan's decisions log. |
| 20 | **.NET 8, EF Core 8 (SQL Server), xUnit, Testcontainers** integration tests. | LTS stack; integration tests run against real SQL Server, not an in-memory fake. |

### Implementation decisions (made while building)

| Phase | Decision | Rationale |
|---|---|---|
| 1 | Projects target `net8.0`; build tooling may be a newer SDK. | The machine has the .NET 8 runtime but only SDKs 9 and 10; a newer SDK builds `net8.0` correctly. |
| 1 | Classic `AttendanceApi.sln` (not `.slnx`). | Opens in Visual Studio 2022 and every .NET 8-era tool. |
| 1 | Central package management (`Directory.Packages.props`) and shared `Directory.Build.props` with nullable enabled and warnings as errors. | One place for versions; warnings can't accumulate. |
| 1 | `dotnet-ef` 8.0.x as a local tool (`dotnet-tools.json`). | The globally installed `dotnet-ef` is 9.x; migrations must be generated with the EF Core 8 tool. |
| 1 | Secrets via user-secrets locally and a git-ignored `.env` for Docker Compose; `appsettings.json` holds only empty placeholders. | The plan forbids committing the signing key; the same applies to the SA and seed-admin passwords. |
| 2 | Phase 2 was built and tested with the .NET 8 SDK (8.0.131) on Linux as well as newer SDKs on Windows. | Confirms the solution builds with a plain .NET 8 SDK, with no newer SDK required. |
| 2 | The domain is persistence-ignorant: entities with private setters, factory methods that enforce invariants (`DomainException`), and private parameterless constructors for EF Core. Rules are static, side-effect-free classes (`AttendanceDatePolicy`, `AttendanceRowValidator`, `EnrolledDaysCalculator`, `ChronicAbsenceEvaluator`, `CurrentSchoolResolver`) that take already-loaded data and "today"/"now" as arguments. | Every rule can be unit-tested without a database or a clock; the Application layer loads data with LINQ and supplies `IClock` values. |
| 2 | A school-year label is the start date's calendar year plus one (`2026-2027` for a year starting in Aug or Sep 2026). Years are at most one year long; terms must sit inside their year and may not overlap; a school's years may not overlap or share a label. | Labels must match across schools so totals can be combined district-wide per school year (decision 6). |
| 2 | A date range for a school and label is the school's own `SchoolYear` when it has one, otherwise the default 1 Sep – 31 Aug. | Lets "current school" (Q3) and enrollment overlap be decided even for a school whose year hasn't been created yet. |
| 2 | Enrollment dates are inclusive: `ExitDate` is the last enrolled day (Q6). | Product-owner answer. |
| 2 | Row checks run in a fixed order and report only the first failure: duplicate in batch, unknown student, inactive student, not enrolled, unknown/missing code, inactive code, negative minutes late, notes over 500 characters. | One clear reason per skipped row in the response and the issues log. |
| 2 | A missing or blank code is skipped as `UnknownCode`; it is **not** defaulted to `P`. Codes are matched case-insensitively after trimming and stored upper-case. | BR-02 (blank = P) was a UI default, never a database rule; the API requires an explicit code. |
| 2 | Duplicate student in a batch: every copy is skipped (Q7). Inactive student: new skip reason `StudentInactive` (Q9). | Product-owner answers. |
| 2 | An issue row for a student ID that doesn't exist stores `StudentId = null` (the ID is in the detail text); raw codes over 20 characters are cut short for storage. | Keeps a real foreign key from the issues log to `Student`. |
| 2 | Rate = absences × 100 ÷ enrolled days, compared **unrounded** with the threshold. Enrolled days use each enrollment's own school calendar; a day enrolled at two schools counts once; zero enrolled days is "not determinable", never a division by zero. | Plan §7; avoids a rounded 9.5% being treated as 10%. |
| 2 | Rate not determinable (Q8): method Rate → `NotDeterminable`; method Either → `Chronic` if the count result is chronic, otherwise `NotDeterminable`. `NotDeterminable` never raises an alert. | Product-owner answer. |
| 2 | Audit columns store both the user's ID and name (`CreatedByUserId`/`CreatedBy`, etc.); the ID is null only for system writes such as the legacy import. Timestamps must be `DateTimeKind.Utc`. | Plan §9 ("the user's ID and name") and Q2. |

---

## What cannot be determined from the legacy code / ambiguities

From section 7 (G-01 to G-10) of the [legacy analysis](docs/Legacy-System-Analysis.md).

| ID | Gap | How the new design handles it |
|---|---|---|
| G-01 | Whether the legacy scripts match production (the save proc can't be the deployed version; the real `SchoolYear()` is unknown). | Nothing depends on legacy scripts. School year comes from our own `SchoolYear` table per school. The import copies raw records and **recalculates** totals instead of trusting the legacy summary. |
| G-02 | The real attendance codes and their flags. | Standard set from plan §4 seeded only into an empty table. The import brings in legacy codes not in the seed as-is and lists them in its report. Admins can change flags. |
| G-03 | The intended definition of chronic absenteeism (count vs. rate; this year vs. all years). | Both count and rate are always calculated and returned, **per school year**. The per-school `ChronicMethod` (default Count, the legacy rule) decides `isChronicallyAbsent` and the alert. |
| G-04 | No enrollment history or instructional-day calendar, so a rate can't be calculated. | New `StudentEnrollment`, `Term` and `NonInstructionalDay` tables. When enrollment or calendar data is missing, the rate result is "not determinable" with a reason. The import creates one enrollment per student starting at the school-year start (or CreatedDate). |
| G-05 | Whether partial-day absences or tardies should count. | Whole-day codes only, as in legacy. Tardies and minutes late are stored but never counted as absences. Partial-day attendance is out of scope for v1. |
| G-06 | Whether the school year starts on 1 September for every school. | One SchoolYear per school, default 1 Sep – 31 Aug, editable by an admin. A missing year is auto-created with the default dates and one whole-year term (Q1). |
| G-07 | Structure of `Schools`, `SchoolTerms`, `AttendanceSubmissionLog` and the logic of `sp_GetStudentsForAttendance`. | The new schema defines its own School, SchoolYear/Term and AttendanceSubmission tables. The import tool will read legacy tables by their **inferred** shape and must be checked against the real database before it is run. The legacy roster proc is replaced by a new roster endpoint (Q5). |
| G-08 | Who else reads the summary, alerts and submission log (other screens, reports, state reporting). | Summary, alerts and submissions are kept as first-class tables in the new schema. Downstream consumers must be identified before VB6 is retired; they are outside this API's scope. |
| G-09 | Who the users are, how they are identified and what roles exist. | API-issued JWTs, individual `AppUser` accounts, roles Admin / AttendanceClerk / Viewer, school scoping via `AppUserSchool`. Every write records the authenticated user. |
| G-10 | How history should be scoped across schools for transferred students. | History and totals combine all schools in the database for the school year. Responses list the schools included and state that data from schools outside this database isn't available. |

### Open questions (raised during implementation)

Answers from the product owner (17 Oct 2026). They extend the plan and are binding for later phases.

| # | Question | Answer |
|---|---|---|
| Q1 | An auto-created school year has no terms, so with the default `AllowOutsideTerm = false` every date in it would get a 422. | **Auto-creation also adds one term covering the whole year** (1 Sep – 31 Aug). An admin can replace it with real terms later. |
| Q2 | Which time zone defines "today" for the date checks and the rate calculation? | **UTC.** "Today" is the current UTC date (from `IClock`); all timestamps are stored and returned in UTC, and clients convert to local time using the browser locale. Attendance dates are calendar dates (`date`) with no time zone. |
| Q3 | What is the student's "current school" for the count threshold, the deciding `ChronicMethod` and the alert's SchoolId? | **The school of the student's latest enrollment in that school year** (latest EntryDate overlapping the year). |
| Q4 | Settings have no limit on how far ahead entry is allowed. | **New setting `MaxFutureDays`** (null = unlimited), admin-editable. It applies only when `AllowFutureDates` is true; mirrors `MaxBackdateDays`. |
| Q5 | Replacement for the legacy roster proc (`sp_GetStudentsForAttendance`)? | **Yes, a roster endpoint in v1:** students enrolled at a school on a date (optional grade filter), each with any existing record for that date. Same role and school scoping as the history endpoint (Attendance.Read). |
| Q6 | Is `StudentEnrollment.ExitDate` the last enrolled day or the first day not enrolled? | **Inclusive:** the student is enrolled on D when `EntryDate ≤ D ≤ ExitDate`. |
| Q7 | Which rows are skipped when the same student appears more than once in a batch? | **All copies** are skipped and each is logged as a duplicate; nothing is saved for that student. |
| Q8 | What is the overall status when the deciding method needs the rate but it can't be calculated? | **Rate:** `NotDeterminable`. **Either:** `Chronic` if the count result is chronic, otherwise `NotDeterminable`. No alert unless the status is `Chronic`. |
| Q9 | Should a row for an inactive student be skipped? | **Yes**, with the new skip reason `StudentInactive`, even if an enrollment covers the date. |

---

## Delivery status

| Phase | Scope | Status |
|---|---|---|
| 1 | Solution, Docker Compose, README skeleton | Done |
| 2 | Domain model and rules, with unit tests | Done |
| 3 | EF Core context, configurations, migration, seed data | Not started |
| 4 | Save, history and chronic-status endpoints, with integration tests | Not started |
| 5 | Admin endpoints (settings, calendar, issues log) | Not started |
| 6 | Legacy import tool | Not started |
| 7 | README: final architecture decisions and ambiguities | Not started |
