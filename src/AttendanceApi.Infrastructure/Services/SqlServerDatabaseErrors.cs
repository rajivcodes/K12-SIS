using AttendanceApi.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Infrastructure.Services;

internal sealed class SqlServerDatabaseErrors : IDatabaseErrors
{
    // 2601: duplicate key in a unique index; 2627: unique/primary key constraint violation.
    private static readonly int[] UniqueViolationNumbers = [2601, 2627];

    public bool IsConcurrentWriteConflict(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return true;
        }

        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is SqlException sql && UniqueViolationNumbers.Contains(sql.Number))
            {
                return true;
            }
        }

        return false;
    }
}
