using Microsoft.EntityFrameworkCore;

namespace Yonder.DataProvider
{
    public class YonderContext : DbContext
    {
        public YonderContext(DbContextOptions options) : base(options)
        {

        }
        public DbSet<Classes> Classes { get; set; }
        public DbSet<RollCall> RollCall { get; set; }

    }
}
