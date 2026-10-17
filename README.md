# AttendanceApi

A .NET 8 Web API that replaces the daily-attendance module of a legacy VB6 + SQL Server K-12 student information system. It was built from scratch: the legacy code was used only as a reference ([analysis](docs/Legacy-System-Analysis.md), [plan](docs/Implementation-Plan.md)), and nothing from it is ported or wrapped. Every decision and its reasoning is in the [decisions log](docs/Decisions-Log.md).

## Run and test

Requires the .NET 8 SDK and Docker.

* **Everything in Docker:** copy `.env.example` to `.env`, fill in the SQL Server password, a JWT signing key (`openssl rand -base64 48`) and the first admin's credentials, then run `docker compose up --build`. Open `http://localhost:8080/swagger`, call `POST /api/v1/auth/login`, and use **Authorize** with the token.
* **API on the host:** start SQL Server with `docker compose up -d sqlserver`, set the same values with `dotnet user-secrets` (see the [configuration reference](docs/Decisions-Log.md#configuration-reference)), then `dotnet run --project src/AttendanceApi.Api`.
* **Tests:** `dotnet test` runs 165 domain unit tests and 78 integration tests (API, persistence and legacy import) against a real SQL Server 2022 started by Testcontainers. They include a regression test for legacy defect D-01.

In Development the API applies migrations and seeds the standard codes and, into an empty database only, **demo data**: two schools, eleven students and attendance up to yesterday. Set `DEMO_USER_PASSWORD` to also get the demo accounts `lincoln.clerk`, `roosevelt.clerk` and `district.viewer`. Real schools, students and enrollments come from the [legacy import tool](docs/Legacy-Import.md). Run it with `--dry-run` first; it can safely be run again.

| Endpoint | Purpose |
|---|---|
| `POST /api/v1/schools/{id}/attendance/{date}` | Save one school-day batch. Bad rows are skipped and reported; a date the school doesn't allow returns 422. |
| `GET  /api/v1/schools/{id}/attendance/{date}?grade=` | Roster for entry: enrolled students with any record already saved for that day. |
| `GET  /api/v1/students/{id}/attendance?schoolYear=` | Attendance history for a school year, all schools, newest first. |
| `GET  /api/v1/students/{id}/chronic-absenteeism?schoolYear=` | Totals, the count result, the rate result and the overall status. |
| `/api/v1/admin/…` (Admin only) | School settings, school years, terms, closures, the skipped-row log, and user accounts. |
| `POST /api/v1/auth/login`, `GET /api/v1/attendance-codes`, `GET /health` | Sign-in (JWT), code list, health check. |

## Key architectural decisions

* **Clean layers** (Domain → Application → Infrastructure → Api) with business rules as pure, unit-tested domain classes. The legacy defects lived in untestable T-SQL.
* **New EF Core code-first database, all queries in LINQ, no stored procedures.** Constraints the legacy schema lacked now live in the database: one record per student per day (fixes D-06), check constraints, and a filtered unique index allowing one open chronic alert per student per year.
* **One transaction per save, rows checked and upserted one at a time.** Nothing carries over from one row to the next, which removes D-01 and D-02 by design. A collision with a concurrent save is retried once.
* **Skip the bad row, not the batch.** Each skipped row is returned with its reason and logged to `AttendanceSubmissionIssue` for admins.
* **Totals are recalculated from the records for the school year across all schools,** never added to (fixes D-03, D-08 and D-09).
* **Chronic absenteeism is calculated both ways:** by count (≥ 10 days, the legacy rule) and by rate (≥ 10% of enrolled school days). A per-school setting (Count, Rate or Either, default Count) decides the status and the alert.
* **The API issues its own JWTs** (60 minutes, rate-limited login, hashed passwords). There are three roles, and non-admins are limited to their assigned schools. Every write records the real user, not a shared SQL login.
* **Predictable errors and logs:** RFC 7807 ProblemDetails, a generic 500 for unexpected failures, and Serilog structured logs with a correlation ID on every request. A startup failure is logged and the process exits with code 1.

## What the legacy code could not tell us, and how it is handled

| Unknown | Handling |
|---|---|
| Whether the scripts match production (G-01), and the shape of tables that weren't provided (G-07) | Nothing depends on the scripts. The import reads an assumed legacy shape that is documented for checking first, copies raw records with their stored flags, recalculates totals, and reports every orphan, duplicate and unknown code. |
| The real attendance codes (G-02) | A standard K-12 set is seeded into an empty table. Codes are data that admins can edit; unknown legacy codes are imported and reported. |
| The definition of chronic absenteeism (G-03) | Both methods are always returned, per school year, and the per-school setting decides. |
| No enrollment history or school calendar (G-04, G-06) | New enrollment, school-year/term and closure tables. The default year is 1 Sep – 31 Aug and can be edited per school. A missing year is auto-created with one whole-year term. Without enough data the rate is "not determinable" with a reason. |
| Partial days, other consumers, transfers (G-05, G-08, G-10) | Whole-day codes only. The summary, alert and submission tables are kept for other consumers. Totals combine every school, and the response notes that schools outside this system aren't included. |
| Users and roles (G-09) | Individual accounts with the roles Admin, AttendanceClerk and Viewer, plus school assignments. |
| Gaps found during the build | Answered by the product owner (Q1–Q9: UTC dates, current school, max future days, roster, inclusive exit dates, duplicate rows, inactive students, rate fallback). See the [decisions log](docs/Decisions-Log.md#open-questions-raised-during-implementation). |

**Status:** phases 1–6 of 7 are done: solution, domain, persistence, the main and admin endpoints, and the legacy import tool, all with tests.
