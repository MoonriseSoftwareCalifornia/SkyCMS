using Microsoft.EntityFrameworkCore;

namespace Cosmos.MultiTenant.Administrator.Data
{
    public class StoryDeskDbContext : DbContext
    {
        public StoryDeskDbContext(DbContextOptions<StoryDeskDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configure your entities here
            // Example: modelBuilder.Entity<YourEntity>().ToTable("YourTableName");
            modelBuilder.Entity<WebsiteAuthor>()
                .ToContainer("WebsiteAuthors")
                .HasPartitionKey(k => k.Id);
        }

        public DbSet<WebsiteAuthor> WebsiteAuthors { get; set; }
    }
}
