using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AttendanceApi.Api.IntegrationTests.Fixtures;
using AttendanceApi.Application.Auth;
using AttendanceApi.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Api.IntegrationTests.Api;

public class AuthTests : ApiTestBase
{
    public AuthTests(SqlServerFixture server)
        : base(server)
    {
    }

    [Fact]
    public async Task Seeded_admin_can_sign_in_and_gets_a_60_minute_token()
    {
        var response = await AnonymousClient().PostAsJsonAsync(
            "/api/v1/auth/login", new { userName = ApiFactory.AdminUserName, password = ApiFactory.AdminPassword });

        var login = await ReadAsync<LoginResponse>(response);
        Assert.Equal("Bearer", login.TokenType);
        Assert.Equal(TestClock.Default.AddMinutes(60), login.ExpiresAt);
        Assert.Equal(("admin", "Admin"), (login.User.UserName, login.User.Role));
        Assert.Equal(3, login.AccessToken.Split('.').Length);
    }

    [Theory]
    [InlineData("admin", "wrong-password")]
    [InlineData("nobody", "Admin-Test-Pw-1")]
    [InlineData("", "")]
    public async Task Bad_credentials_get_401(string userName, string password)
    {
        var response = await AnonymousClient().PostAsJsonAsync("/api/v1/auth/login", new { userName, password });

        var problem = await ProblemAsync(response, 401);
        Assert.Equal("Invalid user name or password.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Inactive_user_cannot_sign_in()
    {
        var id = await CreateUserAsync("former.clerk", UserRole.AttendanceClerk);
        await using (var db = Db())
        {
            (await db.AppUsers.SingleAsync(u => u.Id == id)).Deactivate();
            await db.SaveChangesAsync();
        }

        var response = await AnonymousClient().PostAsJsonAsync("/api/v1/auth/login", new { userName = "former.clerk", password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/attendance-codes")]
    [InlineData("/api/v1/students/1/attendance")]
    [InlineData("/api/v1/students/1/chronic-absenteeism")]
    [InlineData("/api/v1/schools/1/attendance/2026-10-15")]
    public async Task Endpoints_require_a_token(string url)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await AnonymousClient().GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task A_forged_token_is_rejected()
    {
        var client = AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwibmFtZSI6ImFkbWluIiwicm9sZSI6IkFkbWluIn0.c2lnbmF0dXJl");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/attendance-codes")).StatusCode);
    }

    [Fact]
    public async Task Viewer_can_read_but_cannot_save()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var studentId = await CreateStudentAsync("Reader", schoolId);
        await CreateUserAsync("viewer", UserRole.Viewer, schoolId);
        var client = await ClientForAsync("viewer");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/schools/{schoolId}/attendance/2026-10-15")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SaveAsync(client, schoolId, new DateOnly(2026, 10, 15), new { studentId, code = "P" })).StatusCode);
    }

    [Fact]
    public async Task Clerk_is_limited_to_assigned_schools()
    {
        var mine = await CreateSchoolAsync("Mine");
        var other = await CreateSchoolAsync("Other");
        var studentAtOther = await CreateStudentAsync("Elsewhere", other);
        await CreateUserAsync("clerk", UserRole.AttendanceClerk, mine);
        var client = await ClientForAsync("clerk");

        await ProblemAsync(await SaveAsync(client, other, new DateOnly(2026, 10, 15), new { studentId = studentAtOther, code = "P" }), 403);
        await ProblemAsync(await client.GetAsync($"/api/v1/schools/{other}/attendance/2026-10-15"), 403);
        await ProblemAsync(await client.GetAsync($"/api/v1/students/{studentAtOther}/attendance"), 403);
        await ProblemAsync(await client.GetAsync($"/api/v1/students/{studentAtOther}/chronic-absenteeism"), 403);

        // Unknown student IDs look the same as other schools' students (no ID probing).
        await ProblemAsync(await client.GetAsync("/api/v1/students/999999/attendance"), 403);
    }

    [Fact]
    public async Task A_demoted_admins_token_stops_working_immediately()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var secondAdminId = await CreateUserAsync("second.admin", UserRole.Admin);
        var secondAdmin = await ClientForAsync("second.admin");
        var admin = await AdminClientAsync();

        var demote = await admin.PutAsJsonAsync($"/api/v1/admin/users/{secondAdminId}", new { displayName = "Second", role = "Viewer", isActive = true, schoolIds = new[] { schoolId } });
        Assert.Equal(HttpStatusCode.OK, demote.StatusCode);

        // The old token still says Admin; it must not be able to act as one (e.g. re-promote itself).
        await ProblemAsync(await secondAdmin.PutAsJsonAsync($"/api/v1/admin/users/{secondAdminId}", new { displayName = "Second", role = "Admin", isActive = true }), 401);
        await ProblemAsync(await secondAdmin.GetAsync($"/api/v1/schools/{schoolId}/attendance/2026-10-15"), 401);

        // Signing in again gives a Viewer token that works for reading.
        var asViewer = await ClientForAsync("second.admin");
        Assert.Equal(HttpStatusCode.OK, (await asViewer.GetAsync($"/api/v1/schools/{schoolId}/attendance/2026-10-15")).StatusCode);
    }

    [Fact]
    public async Task Token_of_a_user_deactivated_after_sign_in_stops_working()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var id = await CreateUserAsync("clerk", UserRole.AttendanceClerk, schoolId);
        var client = await ClientForAsync("clerk");
        await using (var db = Db())
        {
            (await db.AppUsers.SingleAsync(u => u.Id == id)).Deactivate();
            await db.SaveChangesAsync();
        }

        await ProblemAsync(await client.GetAsync($"/api/v1/schools/{schoolId}/attendance/2026-10-15"), 401);
    }
}

public class LoginRateLimitTests : ApiTestBase
{
    public LoginRateLimitTests(SqlServerFixture server)
        : base(server)
    {
    }

    protected override int LoginPermitLimit => 3;

    [Fact]
    public async Task Repeated_login_attempts_are_rate_limited()
    {
        var client = AnonymousClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "admin", password = "guess" + i });
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests],
            statuses);
    }
}
