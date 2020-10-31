using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Yonder.DataProvider
{
    public class YonderContext : DbContext
    {
        private readonly string connectionString;

        public YonderContext(string connectionString)
        {
            this.connectionString = connectionString;
        }
        public DbSet<Classes> Classes { get; set; }
        public DbSet<RollCall> RollCalls { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(this.connectionString);
        }
    }
}
