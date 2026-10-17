using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AttendanceApi.Api.IntegrationTests.Fixtures;
using AttendanceApi.Application.Auth;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Students;
using AttendanceApi.Domain.Users;
using AttendanceApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Api.IntegrationTests.Api;

/// <summary>
/// Each test gets a fresh database and API host. Helpers seed data directly and sign in through the API.
/// </summary>
[Collection(SqlServerCollection.Name)]
public abstract class ApiTestBase : IAsyncLifetime
{
    public const string Password = "Test-User-Pw-1";

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly SqlServerFixture _server;
    private TestDatabase? _database;
    private ApiFactory? _factory;

    protected ApiTestBase(SqlServerFixture server)
    {
        _server = server;
    }

    protected ApiFactory Factory => _factory ?? throw new InvalidOperationException("Not initialized.");

    protected TestDatabase Database => _database ?? throw new InvalidOperationException("Not initialized.");

    protected virtual int LoginPermitLimit => 1000;

    /// <summary>Lets a test class replace services in the API host (e.g. a failing dependency).</summary>
    protected virtual void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
    }

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync(_server);
        _factory = new ApiFactory(_database.ConnectionString, LoginPermitLimit, ConfigureServices);
        _ = _factory.Server; // start the host: runs migrations and seeding
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    protected AttendanceDbContext Db() => Database.CreateContext();

    protected HttpClient AnonymousClient() => Factory.CreateClient();

    protected async Task<HttpClient> ClientForAsync(string userName, string password = Password)
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    protected Task<HttpClient> AdminClientAsync() => ClientForAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);

    protected async Task<int> CreateUserAsync(string userName, UserRole role, params int[] schoolIds)
    {
        await using var db = Db();
        var user = AppUser.Create(userName, userName, null, "pending", role);
        user.SetPasswordHash(new PasswordHasher<AppUser>().HashPassword(user, Password));
        user.AssignSchools(schoolIds);
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>A school with default settings; optionally with a default school year for 2026-2027.</summary>
    protected async Task<int> CreateSchoolAsync(string name, bool withSchoolYear = true, Action<SchoolAttendanceSettings>? configure = null)
    {
        await using var db = Db();
        var school = School.Create(name);
        db.Schools.Add(school);
        await db.SaveChangesAsync();

        var settings = SchoolAttendanceSettings.CreateDefault(school.Id);
        configure?.Invoke(settings);
        db.SchoolAttendanceSettings.Add(settings);
        if (withSchoolYear)
        {
            db.SchoolYears.Add(SchoolYear.CreateDefault(school.Id, new DateOnly(2026, 9, 1)));
        }

        await db.SaveChangesAsync();
        return school.Id;
    }

    protected async Task<int> CreateStudentAsync(string lastName, int schoolId, DateOnly? entry = null, DateOnly? exit = null, string grade = "05")
    {
        await using var db = Db();
        var student = Student.Create("Test", lastName, grade, null);
        db.Students.Add(student);
        await db.SaveChangesAsync();
        db.StudentEnrollments.Add(StudentEnrollment.Create(student.Id, schoolId, entry ?? new DateOnly(2026, 9, 1), exit));
        await db.SaveChangesAsync();
        return student.Id;
    }

    protected async Task EnrollAsync(int studentId, int schoolId, DateOnly entry, DateOnly? exit = null)
    {
        await using var db = Db();
        db.StudentEnrollments.Add(StudentEnrollment.Create(studentId, schoolId, entry, exit));
        await db.SaveChangesAsync();
    }

    protected static string Day(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    protected static Task<HttpResponseMessage> SaveAsync(HttpClient client, int schoolId, DateOnly date, params object[] rows) =>
        client.PostAsJsonAsync($"/api/v1/schools/{schoolId}/attendance/{Day(date)}", rows);

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    protected static async Task<JsonElement> ProblemAsync(HttpResponseMessage response, int expectedStatus)
    {
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    protected async Task<List<AttendanceRecord>> RecordsAsync()
    {
        await using var db = Db();
        return await db.AttendanceRecords.AsNoTracking().OrderBy(r => r.StudentId).ThenBy(r => r.Date).ToListAsync();
    }
}
