using Microsoft.EntityFrameworkCore;
using System;

namespace DatabaseAccess
{
    public class SampleDbContext: DbContext
    {
        public DbSet<Url> Urls { get; set; }

        public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options)
        {
        }
    }
}
