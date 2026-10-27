using Microsoft.EntityFrameworkCore;

namespace Paycrest.Data;

public static class DbExtensions
{
    /// <summary>
    /// Runs <paramref name="work"/> in a transaction that is compatible with the SQL Server retrying execution strategy.
    /// On a transient failure the change tracker is cleared and the whole unit is retried.
    /// </summary>
    public static Task InTransactionAsync(this ApplicationDbContext db, Func<Task> work) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync();
            await work();
            await tx.CommitAsync();
        });
}
