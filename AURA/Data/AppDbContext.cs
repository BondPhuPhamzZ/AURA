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
        public DbSet<MailboxConnection> MailboxConnections { get; set; }
        public DbSet<MailboxSyncCursor> MailboxSyncCursors { get; set; }
        public DbSet<EmailMessage> EmailMessages { get; set; }
        public DbSet<EmailAttachment> EmailAttachments { get; set; }
        public DbSet<IngestionOutboxEvent> IngestionOutboxEvents { get; set; }

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
                .HasIndex(r => r.SourceType);

            modelBuilder.Entity<AuditLog>()
                .HasIndex(log => new { log.RequestId, log.Timestamp });

            modelBuilder.Entity<ReimbursementRequest>()
                .Property(r => r.ClaimedAmount)
                .HasPrecision(18, 2);

            // SQL Server generates rowversion values. The isolated SQLite integration
            // tests do not have that database type, so they persist the initialized byte
            // value while keeping the property as a concurrency token.
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                modelBuilder.Entity<ReimbursementRequest>()
                    .Property(r => r.RowVersion)
                    .IsConcurrencyToken()
                    .ValueGeneratedNever();
            }

            modelBuilder.Entity<MailboxConnection>()
                .HasIndex(item => new { item.Provider, item.ExternalMailboxId })
                .IsUnique();

            modelBuilder.Entity<MailboxSyncCursor>()
                .HasOne<MailboxConnection>()
                .WithOne()
                .HasForeignKey<MailboxSyncCursor>(item => item.MailboxConnectionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmailMessage>()
                .HasIndex(item => new { item.MailboxConnectionId, item.ExternalMessageId })
                .IsUnique();

            modelBuilder.Entity<EmailMessage>()
                .HasOne<MailboxConnection>()
                .WithMany()
                .HasForeignKey(item => item.MailboxConnectionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmailAttachment>()
                .HasIndex(item => new { item.EmailMessageId, item.ExternalAttachmentId })
                .IsUnique();

            modelBuilder.Entity<EmailAttachment>()
                .HasIndex(item => item.FileSha256);

            modelBuilder.Entity<EmailAttachment>()
                .HasOne<EmailMessage>()
                .WithMany()
                .HasForeignKey(item => item.EmailMessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmailAttachment>()
                .HasOne<ReimbursementRequest>()
                .WithMany()
                .HasForeignKey(item => item.ReimbursementRequestId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<IngestionOutboxEvent>()
                .HasIndex(item => new { item.ProcessedAt, item.OccurredAt });
        }
    }
}
