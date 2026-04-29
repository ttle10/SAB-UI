using Microsoft.EntityFrameworkCore;
using SystemeAideBasculement.Models;

namespace SystemeAideBasculement.Context
{
    public class AideMemoireDbContext : DbContext
    {
        public DbSet<AideMemoireModel> AideMemoires { get; set; }

        public AideMemoireDbContext(DbContextOptions<AideMemoireDbContext> options)
            : base(options) { }
    }
}
