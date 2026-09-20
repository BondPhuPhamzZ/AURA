using Microsoft.EntityFrameworkCore;
using AURA.Models;

namespace AURA.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<ReimbursementRequest> ReimbursementRequests { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Index for faster queries on status
            modelBuilder.Entity<ReimbursementRequest>()
                .HasIndex(r => r.Status);
        }
    }
}
