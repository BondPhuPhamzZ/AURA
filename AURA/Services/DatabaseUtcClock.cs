using AURA.Data;
using Microsoft.EntityFrameworkCore;

namespace AURA.Services;

/// <summary>Provides one authoritative UTC clock for work coordinated through SQL Server.</summary>
public static class DatabaseUtcClock
{
    public static Task<DateTime> ReadAsync(AppDbContext database,
        CancellationToken cancellationToken = default) =>
        database.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]")
            .SingleAsync(cancellationToken);
}
