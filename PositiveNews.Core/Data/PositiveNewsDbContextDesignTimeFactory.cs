using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PositiveNews.Core.Data;

/// <summary>
/// Lets `dotnet ef migrations add ...` work against this DbContext directly (`--project
/// PositiveNews.Core`) without needing a startup project's DI registration. The connection
/// string here only matters for schema generation — real callers (PositiveNews.Cli,
/// PositiveNews.Web) supply their own via <c>IDbContextFactory&lt;PositiveNewsDbContext&gt;</c>.
/// </summary>
public sealed class PositiveNewsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PositiveNewsDbContext>
{
    public PositiveNewsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PositiveNewsDbContext>()
            .UseSqlite("Data Source=positivenews.db")
            .Options;

        return new PositiveNewsDbContext(options);
    }
}
