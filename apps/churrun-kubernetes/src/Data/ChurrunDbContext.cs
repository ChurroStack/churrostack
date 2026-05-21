using Microsoft.EntityFrameworkCore;

namespace ChurrunKubernetes.Data
{
    public class ChurrunDbContext : DbContext
    {
        public ChurrunDbContext(DbContextOptions<ChurrunDbContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChurrunDbContext).Assembly);
        }
    }
}
