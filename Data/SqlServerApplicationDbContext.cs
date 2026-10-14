using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyMvcApp10.Data;

public sealed class SqlServerApplicationDbContext : ApplicationDbContext
{
    public SqlServerApplicationDbContext(DbContextOptions<SqlServerApplicationDbContext> options)
        : base(options)
    {
    }
}

public sealed class SqlServerApplicationDbContextFactory : IDesignTimeDbContextFactory<SqlServerApplicationDbContext>
{
    public SqlServerApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SqlServerApplicationDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=MyMvcApp10Design;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsAssembly(typeof(SqlServerApplicationDbContext).Assembly.GetName().Name))
            .Options;

        return new SqlServerApplicationDbContext(options);
    }
}