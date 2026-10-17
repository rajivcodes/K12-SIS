-- ASSUMED shape of the legacy tables the import reads (SQL Server).
-- Students, AttendanceCodes, StudentAttendance and StudentAlerts follow Database/schema.sql as described in
-- docs/Legacy-System-Analysis.md §3; Schools and SchoolTerms are INFERRED from how the legacy code uses them
-- (§1.1, gap G-07). Column names marked "assumed" must be checked against the real database before an
-- import; adjust Legacy/LegacyDbContext.cs if they differ. The import only reads these tables.
-- Used by the integration tests to build a sample legacy database.

CREATE TABLE Schools (
    SchoolID              INT           NOT NULL PRIMARY KEY,
    SchoolName            VARCHAR(100)  NULL,
    Active                BIT           NULL,
    AbsenceAlertThreshold INT           NULL       -- NULL means the legacy default of 10
);

CREATE TABLE SchoolTerms (
    TermID    INT          NOT NULL PRIMARY KEY,
    SchoolID  INT          NULL,
    TermName  VARCHAR(50)  NULL,
    StartDate DATETIME     NULL,
    EndDate   DATETIME     NULL
);

CREATE TABLE Students (
    StudentID   INT          NOT NULL PRIMARY KEY,
    FirstName   VARCHAR(50)  NULL,
    LastName    VARCHAR(50)  NULL,
    Grade       VARCHAR(10)  NULL,
    SchoolID    INT          NULL,
    DateOfBirth DATETIME     NULL,   -- assumed
    IsActive    BIT          NULL,   -- assumed name
    CreatedDate DATETIME     NULL    -- assumed (plan §8 uses it for the enrollment entry date)
);

CREATE TABLE AttendanceCodes (
    CodeValue   VARCHAR(10)  NOT NULL PRIMARY KEY,
    Description VARCHAR(100) NULL,   -- assumed name
    IsAbsent    BIT          NULL,
    IsExcused   BIT          NULL,
    IsActive    BIT          NULL
);

CREATE TABLE StudentAttendance (
    AttendanceID INT          NOT NULL PRIMARY KEY,
    StudentID    INT          NULL,
    SchoolID     INT          NULL,
    AttendDate   DATETIME     NULL,
    AttendCode   VARCHAR(10)  NULL,
    IsAbsent     BIT          NULL,
    IsExcused    BIT          NULL,
    MinutesLate  INT          NULL,
    Notes        VARCHAR(500) NULL,
    TermID       INT          NULL,
    CreatedBy    VARCHAR(100) NULL,
    CreatedDate  DATETIME     NULL,
    ModifiedBy   VARCHAR(100) NULL,
    ModifiedDate DATETIME     NULL
);

CREATE TABLE StudentAlerts (
    AlertID      INT          NOT NULL PRIMARY KEY,
    StudentID    INT          NULL,
    SchoolID     INT          NULL,   -- assumed
    AlertType    VARCHAR(50)  NULL,
    SchoolYear   VARCHAR(9)   NULL,
    AlertDate    DATETIME     NULL,   -- assumed name
    AlertMessage VARCHAR(500) NULL,   -- assumed name
    ResolvedDate DATETIME     NULL,
    ResolvedBy   VARCHAR(100) NULL
);
