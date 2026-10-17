using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUser",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PasswordHash = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUser", x => x.Id);
                    table.CheckConstraint("CK_AppUser_Role", "[Role] IN ('Admin', 'AttendanceClerk', 'Viewer')");
                });

            migrationBuilder.CreateTable(
                name: "AttendanceCode",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsAbsent = table.Column<bool>(type: "bit", nullable: false),
                    IsExcused = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceCode", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "School",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_School", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Student",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Grade = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Student", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppUserSchool",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserSchool", x => new { x.UserId, x.SchoolId });
                    table.ForeignKey(
                        name: "FK_AppUserSchool_AppUser_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppUserSchool_School_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "School",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceSubmission",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    SubmittedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowsReceived = table.Column<int>(type: "int", nullable: false),
                    RowsSaved = table.Column<int>(type: "int", nullable: false),
                    RowsSkipped = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSubmission", x => x.Id);
                    table.CheckConstraint("CK_AttendanceSubmission_Rows", "[RowsSaved] >= 0 AND [RowsSkipped] >= 0 AND [RowsReceived] = [RowsSaved] + [RowsSkipped]");
                    table.ForeignKey(
                        name: "FK_AttendanceSubmission_AppUser_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSubmission_School_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "School",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NonInstructionalDay",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NonInstructionalDay", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NonInstructionalDay_School_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "School",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchoolAttendanceSettings",
                columns: table => new
                {
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ChronicMethod = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ChronicCountThreshold = table.Column<int>(type: "int", nullable: false),
                    ChronicRateThreshold = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    AllowFutureDates = table.Column<bool>(type: "bit", nullable: false),
                    AllowWeekends = table.Column<bool>(type: "bit", nullable: false),
                    AllowOutsideTerm = table.Column<bool>(type: "bit", nullable: false),
                    MaxBackdateDays = table.Column<int>(type: "int", nullable: true),
                    MaxFutureDays = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolAttendanceSettings", x => x.SchoolId);
                    table.CheckConstraint("CK_SchoolAttendanceSettings_ChronicCountThreshold", "[ChronicCountThreshold] >= 1");
                    table.CheckConstraint("CK_SchoolAttendanceSettings_ChronicMethod", "[ChronicMethod] IN ('Count', 'Rate', 'Either')");
                    table.CheckConstraint("CK_SchoolAttendanceSettings_ChronicRateThreshold", "[ChronicRateThreshold] > 0 AND [ChronicRateThreshold] <= 100");
                    table.CheckConstraint("CK_SchoolAttendanceSettings_MaxBackdateDays", "[MaxBackdateDays] IS NULL OR [MaxBackdateDays] >= 0");
                    table.CheckConstraint("CK_SchoolAttendanceSettings_MaxFutureDays", "[MaxFutureDays] IS NULL OR [MaxFutureDays] >= 0");
                    table.ForeignKey(
                        name: "FK_SchoolAttendanceSettings_School_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "School",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchoolYear",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "varchar(9)", unicode: false, maxLength: 9, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolYear", x => x.Id);
                    table.CheckConstraint("CK_SchoolYear_Dates", "[EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_SchoolYear_Label", "[Label] LIKE '[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'");
                    table.ForeignKey(
                        name: "FK_SchoolYear_School_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "School",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentAlert",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AlertType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    SchoolYearLabel = table.Column<string>(type: "varchar(9)", unicode: false, maxLength: 9, nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "int", nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAlert", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAlert_AppUser_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAlert_School_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "School",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAlert_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentAttendanceSummary",
                columns: table => new
                {
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    SchoolYearLabel = table.Column<string>(type: "varchar(9)", unicode: false, maxLength: 9, nullable: false),
                    TotalAbsences = table.Column<int>(type: "int", nullable: false),
                    ExcusedAbsences = table.Column<int>(type: "int", nullable: false),
                    UnexcusedAbsences = table.Column<int>(type: "int", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAttendanceSummary", x => new { x.StudentId, x.SchoolYearLabel });
                    table.CheckConstraint("CK_StudentAttendanceSummary_Counts", "[ExcusedAbsences] >= 0 AND [UnexcusedAbsences] >= 0");
                    table.CheckConstraint("CK_StudentAttendanceSummary_Total", "[TotalAbsences] = [ExcusedAbsences] + [UnexcusedAbsences]");
                    table.ForeignKey(
                        name: "FK_StudentAttendanceSummary_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentEnrollment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExitDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentEnrollment", x => x.Id);
                    table.CheckConstraint("CK_StudentEnrollment_Dates", "[ExitDate] IS NULL OR [ExitDate] >= [EntryDate]");
                    table.ForeignKey(
                        name: "FK_StudentEnrollment_School_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "School",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentEnrollment_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceSubmissionIssue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubmissionId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Reason = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSubmissionIssue", x => x.Id);
                    table.CheckConstraint("CK_AttendanceSubmissionIssue_Reason", "[Reason] IN ('UnknownStudent', 'StudentInactive', 'NotEnrolled', 'UnknownCode', 'InactiveCode', 'NegativeMinutesLate', 'NotesTooLong', 'DuplicateStudentInBatch')");
                    table.ForeignKey(
                        name: "FK_AttendanceSubmissionIssue_AttendanceSubmission_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "AttendanceSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceSubmissionIssue_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Term",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolYearId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Term", x => x.Id);
                    table.CheckConstraint("CK_Term_Dates", "[EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_Term_SchoolYear_SchoolYearId",
                        column: x => x.SchoolYearId,
                        principalTable: "SchoolYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SchoolYearId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: true),
                    AttendanceCodeId = table.Column<int>(type: "int", nullable: false),
                    IsAbsent = table.Column<bool>(type: "bit", nullable: false),
                    IsExcused = table.Column<bool>(type: "bit", nullable: false),
                    MinutesLate = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecord", x => x.Id);
                    table.CheckConstraint("CK_AttendanceRecord_MinutesLate", "[MinutesLate] >= 0");
                    table.ForeignKey(
                        name: "FK_AttendanceRecord_AppUser_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecord_AppUser_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecord_AttendanceCode_AttendanceCodeId",
                        column: x => x.AttendanceCodeId,
                        principalTable: "AttendanceCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecord_SchoolYear_SchoolYearId",
                        column: x => x.SchoolYearId,
                        principalTable: "SchoolYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecord_School_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "School",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecord_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecord_Term_TermId",
                        column: x => x.TermId,
                        principalTable: "Term",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUser_UserName",
                table: "AppUser",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUserSchool_SchoolId",
                table: "AppUserSchool",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCode_Code",
                table: "AttendanceCode",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_AttendanceCodeId",
                table: "AttendanceRecord",
                column: "AttendanceCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_CreatedByUserId",
                table: "AttendanceRecord",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_ModifiedByUserId",
                table: "AttendanceRecord",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_SchoolId_Date",
                table: "AttendanceRecord",
                columns: new[] { "SchoolId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_SchoolYearId_StudentId",
                table: "AttendanceRecord",
                columns: new[] { "SchoolYearId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_StudentId_Date",
                table: "AttendanceRecord",
                columns: new[] { "StudentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_TermId",
                table: "AttendanceRecord",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSubmission_SchoolId_Date",
                table: "AttendanceSubmission",
                columns: new[] { "SchoolId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSubmission_SubmittedAt",
                table: "AttendanceSubmission",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSubmission_SubmittedByUserId",
                table: "AttendanceSubmission",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSubmissionIssue_StudentId",
                table: "AttendanceSubmissionIssue",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSubmissionIssue_SubmissionId",
                table: "AttendanceSubmissionIssue",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_NonInstructionalDay_SchoolId_Date",
                table: "NonInstructionalDay",
                columns: new[] { "SchoolId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolYear_SchoolId_Label",
                table: "SchoolYear",
                columns: new[] { "SchoolId", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolYear_SchoolId_StartDate_EndDate",
                table: "SchoolYear",
                columns: new[] { "SchoolId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Student_LastName_FirstName",
                table: "Student",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAlert_ResolvedByUserId",
                table: "StudentAlert",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAlert_SchoolId_ResolvedAt",
                table: "StudentAlert",
                columns: new[] { "SchoolId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_StudentAlert_OpenChronicAbsence",
                table: "StudentAlert",
                columns: new[] { "StudentId", "SchoolYearLabel" },
                unique: true,
                filter: "[ResolvedAt] IS NULL AND [AlertType] = 'CHRONIC_ABSENCE'");

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollment_SchoolId_EntryDate",
                table: "StudentEnrollment",
                columns: new[] { "SchoolId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollment_StudentId_EntryDate",
                table: "StudentEnrollment",
                columns: new[] { "StudentId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Term_SchoolYearId_StartDate",
                table: "Term",
                columns: new[] { "SchoolYearId", "StartDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserSchool");

            migrationBuilder.DropTable(
                name: "AttendanceRecord");

            migrationBuilder.DropTable(
                name: "AttendanceSubmissionIssue");

            migrationBuilder.DropTable(
                name: "NonInstructionalDay");

            migrationBuilder.DropTable(
                name: "SchoolAttendanceSettings");

            migrationBuilder.DropTable(
                name: "StudentAlert");

            migrationBuilder.DropTable(
                name: "StudentAttendanceSummary");

            migrationBuilder.DropTable(
                name: "StudentEnrollment");

            migrationBuilder.DropTable(
                name: "AttendanceCode");

            migrationBuilder.DropTable(
                name: "Term");

            migrationBuilder.DropTable(
                name: "AttendanceSubmission");

            migrationBuilder.DropTable(
                name: "Student");

            migrationBuilder.DropTable(
                name: "SchoolYear");

            migrationBuilder.DropTable(
                name: "AppUser");

            migrationBuilder.DropTable(
                name: "School");
        }
    }
}
