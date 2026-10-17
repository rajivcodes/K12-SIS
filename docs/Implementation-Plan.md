# AttendanceApi: Implementation Plan

**Status:** Approved, ready for implementation
**Date:** 17 Oct 2026
**Location:** `D:\Projects\AttendanceApi`
**Input:** `Legacy-System-Analysis.md` (IDs D-xx and G-xx below refer to that document)

The legacy system is used **only as a reference**. The new solution has no project references, no shared code, no stored procedures, and no dependency on legacy objects.

---

## 1. Decisions captured from the review

| # | Topic | Decision | Notes |
|---|---|---|---|
| 1 | Database | **Clean cut-over:** a new database with its own schema, owned by EF Core (code-first + migrations). Legacy data is imported by a separate tool that can safely be run again. VB6 is retired once the API is live. | All queries in LINQ. Stored procedures only if a real need appears. |
| 2 | Attendance codes | Seed a **standard K-12 code set** (§4). Codes found in legacy data that aren't in the seed are imported as-is and reported. | Codes are data, so an admin can change them. |
| 3 | Location | `D:\Projects\AttendanceApi` | |
| 4 | Chronic definition | **Both** methods are calculated: **count** (legacy: absent days ≥ threshold, default 10) and **rate** (absent days ÷ enrolled instructional days ≥ threshold %, default 10%). | Decided by a **per-school setting** (Count, Rate or Either). The default is Count (legacy rule). |
| 5 | Cross-school | Absences are **combined across all schools** in the database for the school year. The response lists the schools included and notes that data from schools outside this database is not available. | |
| 6 | Counting rules | Legacy rules kept: every day with `IsAbsent = 1` counts, **excused included**; tardies and minutes late don't count. Absent and excused flags are copied onto the record at save time. | We fix legacy bug D-03 by counting **per school year**, not across all years. |
| 7 | School year | One **SchoolYear per school**, with Terms inside it. It defaults to **1 Sep – 31 Aug**, and an admin can edit it per school. A record's year and term are worked out from its school's calendar. | |
| 8 | Totals and alerts | Kept. Saving updates the stored absence totals and raises a **CHRONIC_ABSENCE** alert (at most one open alert per student per year). | |
| 9 | Bad rows | **Skip just the bad row**, save the rest, record each skip in a submission-issues table that admins can view, and return the skips in the API response. Email notification is behind an interface with a no-op version for now. | |
| 10 | Date rules | Admin settings per school: allow future dates, allow weekends, allow dates outside a term, plus how far ahead or back entry is allowed. | |
| 11 | Auth | **JWT bearer, issued by the API itself** (login endpoint, user accounts in our database). Every save records the user from the token, never a shared login. | Passwords are hashed with ASP.NET Core `PasswordHasher`. The first admin is seeded from configuration. |
| 12 | Stack | .NET 8, EF Core 8 (SQL Server), xUnit, integration tests in Docker (Testcontainers). | **Controllers.** |

---

## 2. Solution structure

```
D:\Projects\AttendanceApi
├─ src
│  ├─ AttendanceApi.Domain           Entities, value objects (SchoolYear, AttendanceDate), rules:
│  │                                 ChronicAbsenceEvaluator, AttendanceRowValidator. No dependencies.
│  ├─ AttendanceApi.Application      Use cases (SaveDailyAttendance, GetAttendanceHistory,
│  │                                 GetChronicStatus, admin settings), DTOs, validation,
│  │                                 interfaces (ICurrentUser, IClock, INotificationSender)
│  ├─ AttendanceApi.Infrastructure   EF Core DbContext, entity configurations, migrations,
│  │                                 seed data, CurrentUser from JWT, system clock
│  ├─ AttendanceApi.Api              HTTP endpoints, JWT auth, ProblemDetails, Swagger,
│  │                                 health checks, logging
│  └─ AttendanceApi.LegacyImport     Console tool: reads legacy DB → writes new schema
│                                    (connects by connection string only; no legacy code)
├─ tests
│  ├─ AttendanceApi.Domain.Tests     Pure unit tests (school year, chronic calculation, row rules)
│  ├─ AttendanceApi.Application.Tests
│  └─ AttendanceApi.Api.IntegrationTests   WebApplicationFactory + Testcontainers SQL Server
├─ docs                              Legacy-System-Analysis.md, Implementation-Plan.md
├─ docker-compose.yml                SQL Server + API for local runs
└─ README.md                         Architecture decisions + ambiguities (kept current)
```

Dependencies point inward: Api → Application → Domain, with Infrastructure → Application/Domain.

---

## 3. Data model (new, clean)

Every table has a primary key, foreign keys, and suitable unique and check constraints. Dates are `date` (DateOnly) and timestamps are UTC `datetime2`.

| Table | Key columns | Purpose / notes |
|---|---|---|
| **School** | Id, Name, IsActive | |
| **SchoolAttendanceSettings** | SchoolId (PK/FK), ChronicMethod (Count/Rate/Either, default Count), ChronicCountThreshold (10), ChronicRateThreshold (10.0%), AllowFutureDates (false), AllowWeekends (false), AllowOutsideTerm (false), MaxBackdateDays (null = unlimited) | The admin settings (decisions 4 and 10) |
| **SchoolYear** | Id, SchoolId, Label `YYYY-YYYY`, StartDate, EndDate | Default 1 Sep – 31 Aug. Unique per (SchoolId, Label). No overlapping years for a school. |
| **Term** | Id, SchoolYearId, Name, StartDate, EndDate | Must fall inside its year. No overlapping terms. |
| **NonInstructionalDay** | Id, SchoolId, Date, Reason | Holidays and closures. Used for the enrolled-days count in the rate method. |
| **Student** | Id, FirstName, LastName, Grade, DateOfBirth, IsActive | |
| **StudentEnrollment** | Id, StudentId, SchoolId, EntryDate, ExitDate (null) | **New** (legacy has none, G-04). Used to decide whether a student is enrolled on a date, and for the rate method. |
| **AttendanceCode** | Id, Code (unique), Description, IsAbsent, IsExcused, IsActive | |
| **AttendanceRecord** | Id, StudentId, SchoolId, Date, SchoolYearId, TermId?, AttendanceCodeId, IsAbsent, IsExcused, MinutesLate (≥0), Notes (≤500), CreatedBy/At, ModifiedBy/At, RowVersion | **Unique (StudentId, Date)** fixes D-06. The flags are a copy taken at save time (decision 6). |
| **StudentAttendanceSummary** | StudentId, SchoolYearLabel (unique together), TotalAbsences, ExcusedAbsences, UnexcusedAbsences, LastUpdated | District-wide (all schools), which fixes D-08. Always recalculated from the records, never added to. |
| **StudentAlert** | Id, StudentId, SchoolId, AlertType, SchoolYearLabel, RaisedAt, Message, ResolvedAt, ResolvedBy | Only one open CHRONIC_ABSENCE alert per student per year (filtered unique index). |
| **AppUser** | Id, UserName (unique), DisplayName, Email, PasswordHash, Role, IsActive, LastLoginAt | API-issued accounts |
| **AppUserSchool** | UserId, SchoolId | Schools a non-admin user may access (enforced) |
| **AttendanceSubmission** | Id, SchoolId, Date, SubmittedBy, SubmittedAt, RowsReceived, RowsSaved, RowsSkipped | Replaces the legacy submission log |
| **AttendanceSubmissionIssue** | Id, SubmissionId, StudentId?, Code?, Reason (enum), Detail | The skipped-row log admins can see (decision 9) |

---

## 4. Seed attendance codes (proposed standard set)

| Code | Description | IsAbsent | IsExcused |
|---|---|---|---|
| P | Present | 0 | 0 |
| T | Tardy (unexcused) | 0 | 0 |
| TE | Tardy (excused) | 0 | 1 |
| A | Absent, unexcused | 1 | 0 |
| E | Absent, excused | 1 | 1 |
| M | Absent, medical (excused) | 1 | 1 |
| OSS | Out-of-school suspension | 1 | 0 |
| ISS | In-school suspension (counts as present) | 0 | 0 |
| FT | Field trip / school activity (counts as present) | 0 | 0 |

The seed runs only if the code table is empty. How suspensions are treated varies by state, so admins can change these.

---

## 5. API endpoints (v1)

| Method & route | Role | Purpose |
|---|---|---|
| `POST /api/v1/schools/{schoolId}/attendance/{date}` | Attendance.Write | Save one school-day batch: `[{studentId, code, minutesLate, notes}]`. Returns `submissionId`, saved/inserted/updated counts, and `skipped[]` with the reason for each. |
| `GET /api/v1/students/{studentId}/attendance?schoolYear=2026-2027` | Attendance.Read | History, all schools, newest first. Defaults to the current year. Includes code description, flags, minutes late, notes, term and school. |
| `GET /api/v1/students/{studentId}/chronic-absenteeism?schoolYear=` | Attendance.Read | Total, excused and unexcused absences; result by count; result by rate (enrolled days, rate, threshold); overall status; schools included; data-completeness notes; open alert. |
| `POST /api/v1/auth/login` | Anonymous | Username and password → JWT (with an expiry time; the token carries the user's ID, name and role) |
| `GET/POST/PUT /api/v1/admin/users` | Admin | Create users, reset passwords, deactivate accounts, assign roles and schools |
| `GET /api/v1/attendance-codes` | Attendance.Read | Active codes |
| `GET/PUT /api/v1/admin/schools/{schoolId}/settings` | Admin | Thresholds and date rules |
| `GET/POST/PUT /api/v1/admin/schools/{schoolId}/school-years` (+ terms, non-instructional days) | Admin | Manage the calendar |
| `GET /api/v1/admin/submission-issues?schoolId=&from=&to=` | Admin | View skipped rows |

Errors are returned as RFC 7807 ProblemDetails:
* **400:** malformed request.
* **404:** unknown school or student.
* **422:** the date isn't allowed by the school's settings (rejects the whole batch, because the date applies to every row).
* **401/403:** auth.

---

## 6. Save flow (one database transaction)

1. Check the request shape: a valid date and a non-empty row list, with no row limits broken.
2. Load the school, its settings, the school year containing the date, and the term. No school year found → automatically create the default 1 Sep – 31 Aug year for that school.
3. Check the date against the settings (future, weekend, outside term, back-date limit) → 422 if not allowed.
4. **Check each row.** Reasons a row is skipped:
   * unknown student
   * not enrolled at this school on the date
   * unknown or inactive code
   * minutes late < 0
   * notes longer than 500 characters (skipped rather than silently cut short)
   * the same student appears twice in the batch
5. Upsert the valid rows, matched by (StudentId, Date). This fixes D-01 and D-02 by design: there is no shared loop state, and code flags are looked up per row.
6. Recalculate the stored totals for the affected students and school year **from the records across all schools**.
7. Evaluate chronic status and raise an alert if the student has crossed the threshold and has no open alert.
8. Write the AttendanceSubmission and AttendanceSubmissionIssue rows. If anything was skipped, call `INotificationSender` (no-op for now).
9. Commit. If two saves collide on the unique key, retry once.

## 7. Chronic absenteeism calculation

* **Count:** `TotalAbsences ≥ ChronicCountThreshold` of the student's **current** school.
* **Rate:**
  * `EnrolledDays` = school days, inside the student's enrollment periods, from the year start up to the earlier of today and the year end. A school day is a weekday inside a term that isn't a non-instructional day.
  * `Rate = TotalAbsences ÷ EnrolledDays`.
  * If there is no enrollment or calendar data, the rate result is returned as **"not determinable"** with a reason, instead of a misleading number.
* Both results are always returned. Which one decides `isChronicallyAbsent` and the alert is set per school (§11).

## 8. Legacy import tool (one-off, run again whenever needed)

1. Schools → School + default settings.
2. SchoolTerms → SchoolYear + Term.
3. Students → Student + StudentEnrollment. EntryDate is the school-year start (or CreatedDate), because legacy keeps no history.
4. AttendanceCodes → AttendanceCode.
5. StudentAttendance → AttendanceRecord, keeping the flags that were stored.
6. StudentAlerts → StudentAlert.
7. Totals are **recalculated**, not copied, because the legacy totals are affected by D-01 and D-03.
8. Output: an import report covering orphan rows, unknown codes and duplicates (student + date).
9. The tool can safely be run again.

## 9. Cross-cutting

* **Auth:** the API signs its own JWTs (HMAC-SHA256). The signing key comes from configuration or user-secrets, never source control. Tokens are short-lived (default 60 minutes). Failed logins are rate-limited. Roles: **Admin**, **AttendanceClerk** (read + write), **Viewer** (read only). The user's ID and name are written to Created/ModifiedBy and SubmittedBy.
* **Validation:** request checks in the Application layer, and row rules in the Domain layer.
* **Logging:** structured, with correlation IDs. Swagger includes JWT support. Health checks report the API and the database.
* **Testing:**
  * Domain unit tests: school-year boundaries, both chronic calculations, every reason a row can be skipped.
  * Integration tests against real SQL Server in Docker: batch save with mixed new and existing records (a regression test for D-01), skips and the issues log, cross-school totals, alert raised only once, auth required.

## 10. Delivery order

1. Set up the solution, Docker Compose and README skeleton
2. Domain model and rules, with unit tests
3. EF Core context, configurations, migration, seed data
4. Save, history and chronic-status endpoints, with integration tests
5. Admin endpoints (settings, calendar, issues log)
6. Legacy import tool
7. README: final architecture decisions and the list of ambiguities

## 11. Decisions log

All decisions are settled. The plan is approved for implementation (17 Oct 2026).

* Endpoints are **controllers**.
* **Clean cut-over** to a new database, with a legacy import tool that can safely be run again.
* The chronic flag and alert method is a **per-school setting** (Count, Rate or Either). The default is Count.
* The **API issues its own JWTs**.
* **Non-admin users are limited to their assigned schools** (AppUserSchool). A save for any other school returns 403.
* When a save arrives for a date with no SchoolYear row, the API **automatically creates the default 1 Sep – 31 Aug school year** for that school. An admin can edit it later.
* **No refresh tokens in v1.** Users log in again when the access token expires.
