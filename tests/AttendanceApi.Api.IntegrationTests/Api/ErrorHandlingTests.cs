using AttendanceApi.Api.IntegrationTests.Fixtures;
using AttendanceApi.Application.Abstractions;
using AttendanceApi.Application.Attendance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AttendanceApi.Api.IntegrationTests.Api;

/// <summary>Unexpected failures: logged, and answered with a generic 500 that leaks nothing.</summary>
public class ErrorHandlingTests : ApiTestBase
{
    public ErrorHandlingTests(SqlServerFixture server)
        : base(server)
    {
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<INotificationSender>();
        services.AddSingleton<INotificationSender, ThrowingNotificationSender>();
    }

    [Fact]
    public async Task A_failing_notification_does_not_fail_a_committed_save()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var studentId = await CreateStudentAsync("Adams", schoolId);
        var client = await AdminClientAsync();

        // One skipped row triggers the (failing) notification after the commit.
        var result = await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(
            client, schoolId, new DateOnly(2026, 10, 15), new { studentId, code = "P" }, new { studentId = 999_999, code = "P" }));

        Assert.Equal((1, 1), (result.RowsSaved, result.Skipped.Count));
        Assert.Single(await RecordsAsync());
    }

    [Fact]
    public async Task An_unexpected_exception_returns_a_generic_500_with_a_correlation_id()
    {
        var token = (await AdminClientAsync()).DefaultRequestHeaders.Authorization;

        // Same host and database, but reading the current user throws: an unexpected server-side failure.
        using var failingHost = Factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<ICurrentUser>();
            s.AddScoped<ICurrentUser, ThrowingCurrentUser>();
        }));
        var client = failingHost.CreateClient();
        client.DefaultRequestHeaders.Authorization = token;
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "boom-1");

        var response = await client.GetAsync("/api/v1/schools/1/attendance/2026-10-15");

        var problem = await ProblemAsync(response, 500);
        Assert.Equal("An unexpected error occurred", problem.GetProperty("title").GetString());
        Assert.Equal("boom-1", problem.GetProperty("correlationId").GetString());
        Assert.DoesNotContain("Simulated", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private sealed class ThrowingNotificationSender : INotificationSender
    {
        public Task NotifyRowsSkippedAsync(RowsSkippedNotification notification, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated mail server outage.");
    }

    private sealed class ThrowingCurrentUser : ICurrentUser
    {
        public int UserId => throw new InvalidOperationException("Simulated failure reading the user.");

        public string UserName => "admin";

        public Domain.Users.UserRole Role => Domain.Users.UserRole.Admin;
    }
}
