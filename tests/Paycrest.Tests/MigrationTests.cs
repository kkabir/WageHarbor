using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Paycrest.Data;

namespace Paycrest.Tests;

public class MigrationTests
{
    /// <summary>
    /// If the data model changes without a migration, the live site refuses to start (HTTP 502.5 on SmarterASP),
    /// because startup runs Migrate and EF stops on pending model changes. publish.ps1 runs the tests first, so this
    /// test blocks that publish. Fix: dotnet ef migrations add &lt;Name&gt; in src/Paycrest.
    /// </summary>
    [Fact]
    public void Every_model_change_has_a_migration()
    {
        // Same Identity set-up as Program.cs: the schema version decides which Identity tables are in the model.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer("Server=(localdb)\\none;Database=none;Trusted_Connection=True"));
        services.AddIdentityCore<ApplicationUser>(o => o.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // No database is opened: this only compares the model with the latest migration snapshot.
        Assert.False(db.Database.HasPendingModelChanges(),
            "The data model has changes with no migration. Run 'dotnet ef migrations add <Name>' in src/Paycrest before publishing.");
    }
}
