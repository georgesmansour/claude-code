using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InvitationPlatform.Infrastructure.Data;

/// <summary>
/// Lets `dotnet ef` create the DbContext without running the API's Program.cs
/// (which opens a database connection on startup). No live database is needed
/// for generating migrations or scripts.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time_only",
                npg => npg.MigrationsAssembly("InvitationPlatform.Infrastructure"))
            .Options;
        return new AppDbContext(options);
    }
}
