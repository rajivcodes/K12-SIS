# AttendanceApi: Decisions Log

The full record behind the one-page [README](../README.md): every architectural and implementation decision, how each legacy gap was handled, the product owner's answers, and the configuration reference.

## Key architectural decisions

From sections 1 and 11 of the [implementation plan](Implementation-Plan.md).

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
| 3 | EF Core packages are 8.0.31 (the latest 8.0 patch at least two weeks old, matching the installed .NET 8 runtime); the `dotnet-ef` local tool moves from 8.0.11 to 8.0.31 to stay in step. | Picks up 8.0 security fixes; tool and runtime versions match. |
| 3 | The Application layer exposes `IAttendanceDbContext` (DbSets + `SaveChangesAsync` + `Database` for transactions), implemented by `AttendanceDbContext` in Infrastructure. Application references only `Microsoft.EntityFrameworkCore`, not the SQL Server provider. | Use cases query with LINQ directly (plan: all queries in LINQ) without a repository layer. |
| 3 | Table names are singular, as listed in plan §3. Enums (`ChronicMethod`, `Role`, issue `Reason`) are stored by name with a check constraint listing the allowed names. | Readable in the database and reports; the database rejects unknown values. |
| 3 | Rules within a row are also database check constraints (dates in order, `MinutesLate ≥ 0`, thresholds in range, totals add up, submission row counts add up). Rules across rows that SQL Server can't express declaratively (no overlapping school years or terms) stay in the domain. | Defence in depth without triggers or stored procedures. |
| 3 | Unique keys: (StudentId, Date) on records (D-06); (SchoolId, Label) on school years; (SchoolId, Date) on non-instructional days; Code; UserName; and the filtered unique index `UX_StudentAlert_OpenChronicAbsence` on (StudentId, SchoolYearLabel) where `ResolvedAt IS NULL AND AlertType = 'CHRONIC_ABSENCE'` (BR-09). | Plan §3; the database enforces what the legacy procedure only attempted. |
| 3 | Foreign keys use `Restrict` (no cascading deletes), except child rows that only exist with their parent: settings → school, terms → school year, user-school assignments → user, issues → submission. Audit user IDs (`CreatedByUserId`, `SubmittedByUserId`, etc.) are foreign keys to `AppUser`. | Attendance history can't disappear through a cascade. |
| 3 | Every `DateTime` is stored as `datetime2` and read back with `DateTimeKind.Utc` (a model-wide value converter); `DateOnly` maps to `date`. `AttendanceRecord.RowVersion` is a SQL Server `rowversion`. | Q2 (UTC everywhere); optimistic concurrency for the save flow. |
| 3 | Seeding runs in code at startup (`DatabaseInitializer`), not with `HasData`: the §4 codes only when the code table is empty, and the first admin only when no admin exists, with the password hashed by ASP.NET Core `PasswordHasher<AppUser>`. Both are safe on every start. | `HasData` would insert codes even into a non-empty table (e.g. after a legacy import) and can't hash a password from configuration. |
| 3 | Migrations are applied at startup only when `Database:ApplyMigrationsOnStartup` is true (Development and tests); otherwise use `dotnet ef database update`. A design-time factory lets `dotnet ef` run against the Infrastructure project alone. | Convenient locally, explicit in production. |
| 3 | The connection string is read when the DbContext is first created and a missing value fails startup with a clear message. SQL Server retry-on-failure is enabled. The `/health` check now includes a database check. | Test hosts can override configuration; plan §9 asks health checks to report the database. |
| 3 | Infrastructure references the ASP.NET Core shared framework (`Microsoft.AspNetCore.App`). | Needed for options binding and `PasswordHasher<T>` now, and for reading the current user from the JWT in Phase 4. |
| 3 | `.editorconfig`: IDE0005 (unused usings) is also turned off for `**/Migrations/*.cs`. | Generated migration code otherwise breaks the warnings-as-errors build. |
| 3 | Persistence integration tests (`tests/AttendanceApi.Api.IntegrationTests/Persistence`) share one Testcontainers SQL Server 2022 container and create a fresh migrated database per test. | Real SQL Server behaviour (filtered index, check constraints, rowversion) with isolated tests. |
| 4 | Use cases are plain handler/query classes in Application (`SaveDailyAttendanceHandler`, `AttendanceHistoryQuery`, `ChronicStatusQuery`, `RosterQuery`, `LoginHandler`); controllers only bind the request and call them. No mediator library. | Little ceremony; each use case is easy to find and test. |
| 4 | Expected errors are typed exceptions (`RequestValidationException` 400, `AccountInactiveException` 401, `ForbiddenException` 403, `NotFoundException` 404, `BusinessRuleException` 422 with a machine-readable `reason`) mapped to ProblemDetails by an MVC exception filter and logged as warnings. Anything else goes to a global `IExceptionHandler`: logged once as an error with the correlation ID, answered with a generic 500 ProblemDetails that leaks no internals. | Consistent RFC 7807 errors; expected outcomes aren't logged as server errors. |
| 4 | Logging uses **Serilog** (console + rolling daily JSON file in `logs/`, 14 days kept), configured in the `Serilog` section of appsettings, with request logging and the `X-Correlation-ID` on every log line and every ProblemDetails. Startup runs in `try/catch/finally`: a startup failure (e.g. missing JWT key, database unreachable) is logged as Fatal, logs are flushed, and the process exits with code 1. Secrets are validated before the database is touched. | Product-owner request (structured logging and robust error handling); plan §9 correlation IDs. |
| 4 | The skipped-rows notification is sent **after** the transaction commits, and a notification failure is caught and logged; it never fails a save that was committed. | A client must not retry (and duplicate) a save that succeeded. |
| 4 | The save runs inside EF Core's execution strategy (transient-fault retries) and one explicit transaction. A unique-key collision or row-version conflict with a concurrent save repeats the whole save once on fresh data; a second conflict is a 500. | Plan §6 step 9; concurrent saves end with one row per student and day (tested). |
| 4 | Batch limit: 1–1000 rows, otherwise 400. A missing settings row is created with the defaults on first save. If the default year can't be auto-created because it would overlap an admin-defined year, the save gets 422 `NoSchoolYear`. | "No row limits broken" (plan §6); auto-creation must never corrupt an edited calendar. |
| 4 | **Student read access for non-admins:** allowed when the student has an enrollment overlapping the requested school year at one of the user's schools; the history and totals returned still cover all schools (decision 5). Otherwise 403. | Extends "non-admins limited to their schools" to the student endpoints, which have no school in the route. |
| 4 | Default `schoolYear` for the student endpoints: the year containing today at the school of the student's latest enrollment, else the 1 September rule. Invalid format → 400. | Respects per-school calendars (decision 7). |
| 4 | The chronic endpoint counts totals **live from the records**; the stored summary is still recalculated on every save for other consumers (G-08). Rate is returned rounded to 2 decimals but compared unrounded. With no enrollment in the year: totals, default thresholds, status `NotDeterminable` with a reason, no alert (confirmed by the product owner). | Avoids D-09 cache drift. |
| 4 | Roster (Q5) is `GET` on the same route as the save (`/api/v1/schools/{schoolId}/attendance/{date}?grade=`): active students enrolled that day, sorted by name, each with that day's record if any. | Load and save use one resource. |
| 4 | Login: one generic 401 for every failure; rate limit **10 attempts per minute per client IP** (`Auth:LoginRateLimit`, all attempts counted); outdated hashes are upgraded on login; `LastLoginAt` recorded. Tokens carry `sub`, `name`, `display_name`, `role`; inbound claim mapping is off; token lifetime is checked against `IClock`. A deactivated account's token stops working on the next request (401). | Plan §9; no user enumeration. |
| 4 | Enums are serialized as strings in JSON. Swagger UI (with a Bearer button) is served in Development at `/swagger`. EF Core SQL logging is on in Development only. | Readable API; easy local exploration. |
| 4 | API integration tests use `WebApplicationFactory<Program>`, a fresh database per test and a fixed `TestClock` (Thursday 15 Oct 2026 UTC). They include the **D-01 regression test**, concurrent saves, skip logging, 422 date rules, cross-school totals, a single alert, auth/roles/school scoping, rate limiting, and the 500/notification-failure paths. | Plan §9 test list. |
| 4 | Schools, students and enrollments have no API in v1 (none in plan §5); they arrive through the legacy import (Phase 6) or directly in the database. | Scope as approved. |
| 5 | **Demo data switch** `DemoData:Enabled` (on only in `appsettings.Development.json`): fills a database **that has no schools** with two schools (Lincoln: Count method; Roosevelt: Either), two-semester calendars with holidays, 11 students incl. a mid-year transfer, attendance up to yesterday, recalculated totals and chronic alerts. `DemoData:UserPassword` (secret, optional) also creates `lincoln.clerk`, `roosevelt.clerk` and `district.viewer`. Deterministic for a given "today". | Reviewers can try every endpoint immediately; real or imported data is never touched (product-owner request). |
| 5 | Admin writes to tables without audit columns (settings, calendar, users) and system writes (demo seed, import runs) add an **`AuditEntry`** (user ID and name, UTC time, action, entity, JSON details) in the same transaction. Passwords never appear in details. | "Every write records the authenticated user + UTC timestamp" without widening every table. |
| 5 | Admin endpoints (Admin policy): `GET/PUT …/admin/schools/{id}/settings`; `GET/POST/PUT …/school-years`; `POST/PUT/DELETE …/school-years/{yearId}/terms/{termId}`; `GET/POST/DELETE …/non-instructional-days`; `GET …/admin/submission-issues?schoolId=&from=&to=&page=&pageSize=`; `GET/POST/PUT /api/v1/admin/users`. PUT is full replacement. | Plan §5. Term DELETE is added so an admin can replace the auto-created whole-year term (Q1). |
| 5 | Status codes: overlapping years/terms, duplicate labels, user names and closure dates → **409**; invalid values (e.g. rate 0, year longer than a year, term outside its year) → 400; changes that would strand recorded attendance (records outside new year dates, a label change once records exist) → **422**; demoting/deactivating the **last active admin** → 422. `DomainException` carries a `Kind` (Invalid/Conflict) so the API can tell 400 from 409. | Clear, testable outcomes; history can't be orphaned. |
| 5 | Any term change re-tags every record of that school year with the term that now contains its date (set-based `ExecuteUpdate`, one transaction). Application references `Microsoft.EntityFrameworkCore.Relational` for this (still provider-independent). | BR-06 stays true after calendar edits. |
| 5 | Admin-set passwords must be at least 12 characters; unknown school IDs in assignments → 400. The seeded admin's password is not policed (it comes from deployment configuration). | Reasonable baseline; no policy was specified. |
| 5 | Out of scope for v1 (not in plan §5): resolving alerts via the API, CRUD for schools/students/enrollments (they come from the import). | Scope as approved. |
| 6 | The legacy import reads the legacy database through a **read-only EF Core context** mapped to the assumed legacy shape (LINQ, no SQL strings, `SaveChanges` blocked). The assumed DDL is shipped in `Legacy/assumed-legacy-schema.sql` and must be checked against the real database first. | Keeps "all queries in LINQ"; makes the G-07 assumption explicit and easy to correct. |
| 6 | **One transaction per run**; `--dry-run` produces the full report then rolls back. Re-runs use the `LegacyIdMap` table (legacy table + key → new ID): schools/students are refreshed, terms/alerts are not duplicated, attendance rows imported earlier are refreshed, and records entered in the new system are kept and reported. | Plan §8 "safe to run again"; no legacy columns in the domain model. |
| 6 | Calendar from SchoolTerms: term → year by its **midpoint**; a year starts on 1 Sep or its earliest term (if earlier) and ends the day before the next year or on 31 Aug (max one year); overlapping/out-of-range terms are reported. Dates no term covers get the default year (same rule as a save). | A term starting 20 Aug belongs to the year it mostly covers; years never overlap. |
| 6 | Attendance import keeps the **stored flags** and the legacy CreatedBy/ModifiedBy and timestamps (a dedicated `AttendanceRecord.Import` factory). Duplicates per student and day: the most recently changed row wins. Unknown codes (D-07) get an **inactive placeholder** code so data isn't lost. Negative minutes → 0; notes > 500 cut; legacy DATETIME treated as UTC. All reported. | Plan §8 steps 5 and 8. |
| 6 | Enrollment for legacy students (no history, G-04): one open enrollment at the current school starting at the school-year start of the first legacy attendance, else `CreatedDate`. Students whose school doesn't exist are imported without an enrollment and reported. | Plan §8 step 3; earliest start covers the imported history for rates and access. |
| 6 | Totals (`StudentAttendanceSummary`) are recalculated from the imported records; the legacy summary is never read. A second open chronic alert for the same student and year is skipped (the newest is kept). | D-01/D-03/D-09; the filtered unique index. |
| 6 | The tool logs with Serilog to the console, writes a Markdown report (counts per table + every issue by category) and exits 0/1/2. Settings file `legacyimport.settings.json`, user-secrets or environment variables. | Operable by someone who didn't write it. |
| 7 | The README is kept to one page (deliverable limit): how to run and test, key decisions, and how each legacy unknown was handled. This log, [Legacy-Import.md](Legacy-Import.md), the [analysis](Legacy-System-Analysis.md) and the [plan](Implementation-Plan.md) hold the detail. | Readable in one sitting; nothing lost. |

---

## What cannot be determined from the legacy code / ambiguities

From section 7 (G-01 to G-10) of the [legacy analysis](Legacy-System-Analysis.md).

| ID | Gap | How the new design handles it |
|---|---|---|
| G-01 | Whether the legacy scripts match production (the save proc can't be the deployed version; the real `SchoolYear()` is unknown). | Nothing depends on legacy scripts. School year comes from our own `SchoolYear` table per school. The import copies raw records and **recalculates** totals instead of trusting the legacy summary. |
| G-02 | The real attendance codes and their flags. | Standard set from plan §4 seeded only into an empty table. The import brings in legacy codes not in the seed as-is and lists them in its report. Admins can change flags. |
| G-03 | The intended definition of chronic absenteeism (count vs. rate; this year vs. all years). | Both count and rate are always calculated and returned, **per school year**. The per-school `ChronicMethod` (default Count, the legacy rule) decides `isChronicallyAbsent` and the alert. |
| G-04 | No enrollment history or instructional-day calendar, so a rate can't be calculated. | New `StudentEnrollment`, `Term` and `NonInstructionalDay` tables. When enrollment or calendar data is missing, the rate result is "not determinable" with a reason. The import creates one enrollment per student starting at the school-year start (or CreatedDate). |
| G-05 | Whether partial-day absences or tardies should count. | Whole-day codes only, as in legacy. Tardies and minutes late are stored but never counted as absences. Partial-day attendance is out of scope for v1. |
| G-06 | Whether the school year starts on 1 September for every school. | One SchoolYear per school, default 1 Sep – 31 Aug, editable by an admin. A missing year is auto-created with the default dates and one whole-year term (Q1). |
| G-07 | Structure of `Schools`, `SchoolTerms`, `AttendanceSubmissionLog` and the logic of `sp_GetStudentsForAttendance`. | The new schema defines its own School, SchoolYear/Term and AttendanceSubmission tables. The import tool reads legacy tables by their **inferred** shape ([assumed DDL](../src/AttendanceApi.LegacyImport/Legacy/assumed-legacy-schema.sql), [import guide](Legacy-Import.md)), which must be checked against the real database before it is run. The legacy roster proc is replaced by a new roster endpoint (Q5). |
| G-08 | Who else reads the summary, alerts and submission log (other screens, reports, state reporting). | Summary, alerts and submissions are kept as first-class tables in the new schema. Downstream consumers must be identified before VB6 is retired; they are outside this API's scope. |
| G-09 | Who the users are, how they are identified and what roles exist. | API-issued JWTs, individual `AppUser` accounts, roles Admin / AttendanceClerk / Viewer, school scoping via `AppUserSchool`. Every write records the authenticated user. |
| G-10 | How history should be scoped across schools for transferred students. | History and totals combine all schools in the database for the school year. Responses list the schools included and state that data from schools outside this database isn't available. |

### Open questions (raised during implementation)

Answers from the product owner (17 Oct 2026). They extend the plan and are binding.

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
| Q10 | What does the chronic endpoint return for a student with no enrollment in the requested year (no "current school")? | **Totals, both results with the district default thresholds, status `NotDeterminable` with a reason**, and no alert. |
| Q11 | Which students may a non-admin read on the student endpoints (no school in the route)? | **Students enrolled at one of the user's schools during the requested school year**; the data returned still covers all schools. Otherwise 403. |

---

## Configuration reference

| Key | Default | Notes |
|---|---|---|
| `ConnectionStrings:AttendanceDb` | none | SQL Server connection string. Secret. |
| `Jwt:SigningKey` | none | HMAC-SHA256 key. Secret; user-secrets or environment only. |
| `Jwt:Issuer` / `Jwt:Audience` | `AttendanceApi` | |
| `Jwt:AccessTokenMinutes` | `60` | No refresh tokens in v1. |
| `Auth:LoginRateLimit:PermitLimit` / `WindowSeconds` | `10` / `60` | Login attempts per client IP per window. |
| `Serilog:*` | console + `logs/attendance-api-.log` | Standard Serilog configuration section. |
| `DemoData:Enabled` / `DemoData:UserPassword` | `false` (`true` in Development) / none | Demo data for an empty database; optional demo account password (secret). |
| `SeedAdmin:*` | none | First admin account (`UserName`, `Password`, optional `DisplayName`, `Email`), created at startup only if no admin exists. If unset, a warning is logged and no admin is created. |
| `Database:ApplyMigrationsOnStartup` | `false` (`true` in `appsettings.Development.json`) | Apply pending EF Core migrations when the API starts. Production should run migrations as a separate deployment step. |

## Database migrations

Migrations live in `src/AttendanceApi.Infrastructure/Persistence/Migrations` and are generated with the local `dotnet-ef` tool (no database needed to add one):

```bash
dotnet ef migrations add <Name> --project src/AttendanceApi.Infrastructure --output-dir Persistence/Migrations
```

Apply them to a database without starting the API:

```bash
dotnet ef database update --project src/AttendanceApi.Infrastructure --connection "<connection string>"
```

An integration test fails if the model changes without a new migration.
