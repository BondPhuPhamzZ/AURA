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
        public DbSet<VisionProviderCooldown> VisionProviderCooldowns { get; set; }
        public DbSet<VisionInvocationFence> VisionInvocationFences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Index for faster queries on status
            modelBuilder.Entity<ReimbursementRequest>()
                .HasIndex(r => r.Status);

            modelBuilder.Entity<ReimbursementRequest>()
                .HasIndex(r => r.FileSha256);

            modelBuilder.Entity<ReimbursementRequest>()
                .HasIndex(r => new { r.ProcessingState, r.QueuedAt });

            modelBuilder.Entity<ReimbursementRequest>()
                .HasIndex(r => r.RetentionExpiresAt);

            modelBuilder.Entity<AuditLog>()
                .HasIndex(log => new { log.RequestId, log.Timestamp });

            modelBuilder.Entity<ReimbursementRequest>()
                .Property(r => r.ClaimedAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<VisionProviderCooldown>()
                .HasKey(cooldown => cooldown.Provider);
            modelBuilder.Entity<VisionProviderCooldown>()
                .Property(cooldown => cooldown.Provider)
                .HasMaxLength(50);

            modelBuilder.Entity<VisionInvocationFence>()
                .HasKey(fence => fence.Id);
            modelBuilder.Entity<VisionInvocationFence>()
                .HasData(new VisionInvocationFence { Id = 1, Token = 0 });
        }
    }
}
