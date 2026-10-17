-- Sample legacy data with the defects and gaps described in docs/Legacy-System-Analysis.md.
INSERT INTO Schools (SchoolID, SchoolName, Active, AbsenceAlertThreshold) VALUES
 (1, 'Lincoln Elementary', 1, 8),
 (2, 'Roosevelt Middle', 1, NULL),
 (3, 'Closed School', 0, NULL);

INSERT INTO SchoolTerms (TermID, SchoolID, TermName, StartDate, EndDate) VALUES
 (1, 1, 'Fall 2025',   '2025-08-20', '2025-12-19'),
 (2, 1, 'Spring 2026', '2026-01-06', '2026-06-12'),
 (3, 1, 'Fall 2026',   '2026-08-19', '2026-12-18'),
 (4, 1, 'Overlap',     '2026-12-01', '2027-01-15'),  -- overlaps Fall 2026
 (5, 2, 'Year 2026',   '2026-09-01', '2027-06-15'),
 (6, 99, 'Orphan',     '2026-09-01', '2026-12-01'),  -- unknown school
 (7, 2, 'No dates',    NULL, NULL);

INSERT INTO AttendanceCodes (CodeValue, Description, IsAbsent, IsExcused, IsActive) VALUES
 ('P', 'Present', 0, 0, 1),
 ('A', 'Absent', 1, 0, 1),
 ('E', 'Excused', 1, 1, 1),
 ('T', 'Tardy', 0, 0, 1),
 ('ISS', 'In-school suspension', 1, 0, 1),   -- standard set says present
 ('OT', 'Other absence', 1, 0, 1);           -- not in the standard set

INSERT INTO Students (StudentID, FirstName, LastName, Grade, SchoolID, DateOfBirth, IsActive, CreatedDate) VALUES
 (100, 'Ada', 'Lovelace', '05', 1, '2015-12-10', 1, '2024-08-01'),
 (101, 'Alan', 'Turing', '05', 1, NULL, 1, NULL),
 (102, 'Grace', 'Hopper', '07', 2, NULL, 1, NULL),
 (103, '', NULL, '06', 2, NULL, 1, NULL),     -- no name
 (104, 'Old', 'Student', '08', 3, NULL, 0, NULL),
 (105, 'Lost', 'Kid', '04', 77, NULL, 1, NULL); -- unknown school

INSERT INTO StudentAttendance (AttendanceID, StudentID, SchoolID, AttendDate, AttendCode, IsAbsent, IsExcused, MinutesLate, Notes, TermID, CreatedBy, CreatedDate, ModifiedBy, ModifiedDate) VALUES
 (1, 100, 1, '2025-10-01', 'A', 1, 0, 0, NULL, 1, 'DOMAIN\clerk1', '2025-10-01 08:00', NULL, NULL),
 (2, 100, 1, '2026-09-15', 'A', 1, 0, 0, NULL, 3, 'DOMAIN\clerk1', '2026-09-15 08:00', NULL, NULL),
 (3, 100, 1, '2026-09-16', 'E', 1, 1, 0, 'Doctor', 3, 'DOMAIN\clerk1', '2026-09-16 08:00', NULL, NULL),
 (4, 100, 1, '2026-09-17', 'A', 0, 0, 0, 'Stored flags say present', 3, 'DOMAIN\clerk1', '2026-09-17 08:00', NULL, NULL),
 (5, 101, 1, '2026-09-15', 'P', 0, 0, 0, NULL, 3, 'DOMAIN\clerk1', '2026-09-15 08:00', NULL, NULL),
 (6, 101, 1, '2026-09-15', 'A', 1, 0, 0, 'Second save', 3, 'DOMAIN\clerk2', '2026-09-15 09:00', 'DOMAIN\clerk2', '2026-09-16 10:00'),
 (7, 101, 1, '2026-09-16', 'ZZ', 1, 0, 0, NULL, 3, 'DOMAIN\clerk1', '2026-09-16 08:00', NULL, NULL),
 (8, 101, 1, '2026-09-17', 'T', 0, 0, -5, NULL, 3, 'DOMAIN\clerk1', '2026-09-17 08:00', NULL, NULL),
 (9, 999, 1, '2026-09-15', 'P', 0, 0, 0, NULL, 3, 'DOMAIN\clerk1', '2026-09-15 08:00', NULL, NULL),
 (10, 102, 2, '2026-09-15', 'A', 1, 0, 0, NULL, 5, 'DOMAIN\clerk3', '2026-09-15 08:00', NULL, NULL),
 (11, 102, NULL, '2026-09-16', 'A', 1, 0, 0, NULL, NULL, NULL, NULL, NULL, NULL),
 (12, 100, 1, NULL, 'P', 0, 0, 0, NULL, NULL, NULL, NULL, NULL, NULL),
 (13, 102, 2, '2027-07-10', 'P', 0, 0, 0, 'Summer school', NULL, 'DOMAIN\clerk3', '2027-07-10 08:00', NULL, NULL),
 (14, 100, 1, '2024-10-01', 'A', 1, 0, 0, NULL, NULL, 'DOMAIN\clerk1', '2024-10-01 08:00', NULL, NULL),
 (15, 100, 1, '2026-01-20', 'A', 1, 0, 0, NULL, 2, 'DOMAIN\clerk1', '2026-01-20 08:00', NULL, NULL);

INSERT INTO StudentAlerts (AlertID, StudentID, SchoolID, AlertType, SchoolYear, AlertDate, AlertMessage, ResolvedDate, ResolvedBy) VALUES
 (1, 100, 1, 'CHRONIC_ABSENCE', '2026-2027', '2026-10-01', 'Student has reached 10 absences', NULL, NULL),
 (2, 100, 1, 'CHRONIC_ABSENCE', '2026-2027', '2026-09-20', 'Older open alert', NULL, NULL),
 (3, 101, 1, 'CHRONIC_ABSENCE', '2025-2026', '2026-02-01', 'Resolved alert', '2026-03-01', 'DOMAIN\admin'),
 (4, 999, 1, 'CHRONIC_ABSENCE', '2026-2027', '2026-10-01', 'Unknown student', NULL, NULL),
 (5, 102, NULL, 'chronic_absence', 'bad', NULL, 'No year', NULL, NULL);
