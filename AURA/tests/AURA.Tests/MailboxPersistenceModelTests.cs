using AURA.Data;
using AURA.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AURA.Tests;

public sealed class MailboxPersistenceModelTests
{
    [Fact]
    public void Mailbox_source_ids_have_unique_database_indexes()
    {
        using var db = CreateContext();

        var messageIndex = db.Model.FindEntityType(typeof(EmailMessage))!.GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(EmailMessage.MailboxConnectionId), nameof(EmailMessage.ExternalMessageId)]));
        var attachmentIndex = db.Model.FindEntityType(typeof(EmailAttachment))!.GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(EmailAttachment.EmailMessageId), nameof(EmailAttachment.ExternalAttachmentId)]));

        Assert.True(messageIndex.IsUnique);
        Assert.True(attachmentIndex.IsUnique);
    }

    [Fact]
    public void Outbox_is_indexed_for_unprocessed_event_delivery()
    {
        using var db = CreateContext();
        var index = db.Model.FindEntityType(typeof(IngestionOutboxEvent))!.GetIndexes()
            .Single(candidate => candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(IngestionOutboxEvent.ProcessedAt), nameof(IngestionOutboxEvent.OccurredAt)]));

        Assert.False(index.IsUnique);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=AuraModelOnly;Trusted_Connection=True")
            .Options;
        return new AppDbContext(options);
    }
}
