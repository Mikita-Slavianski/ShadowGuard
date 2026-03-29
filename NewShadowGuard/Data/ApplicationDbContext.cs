using Microsoft.EntityFrameworkCore;
using NewShadowGuard.Models;

namespace NewShadowGuard.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<Incident> Incidents { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Comment> Comments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Явно указываем имена таблиц (как в БД)
            modelBuilder.Entity<Tenant>().ToTable("Tenant");
            modelBuilder.Entity<User>().ToTable("User");
            modelBuilder.Entity<Asset>().ToTable("Asset");
            modelBuilder.Entity<Log>().ToTable("Log");
            modelBuilder.Entity<Incident>().ToTable("Incident");
            modelBuilder.Entity<AuditLog>().ToTable("AuditLog");
            modelBuilder.Entity<Comment>().ToTable("Comment");
        }
    }
}