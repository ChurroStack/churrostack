using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChurrOS.Api.Data
{
    public class ChurrosDbContextFactory : IDesignTimeDbContextFactory<ChurrosDbContext>
    {
        public ChurrosDbContext CreateDbContext(string[] args)
        {
            // ONLY FOR DESIGN TIME USE. DO NOT USE THIS IN PRODUCTION CODE.
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json")
                .Build();

            var connectionString = configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Connection string 'Database' not found.");
            var optionsBuilder = new DbContextOptionsBuilder<ChurrosDbContext>();
            optionsBuilder.UseNpgsql(connectionString.ToString(), o =>
            {
                o.MigrationsAssembly(typeof(ChurrosDbContext).Assembly.FullName);
                o.CommandTimeout((int)TimeSpan.FromMinutes(1).TotalSeconds);
            })
            .UseSnakeCaseNamingConvention();

            return new ChurrosDbContext(optionsBuilder.Options, null!, null!, null!);
        }
    }
}
