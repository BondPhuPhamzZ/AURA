using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AURA.Models;

public sealed class IngestionOutboxEvent
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required, StringLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string AggregateId { get; set; } = string.Empty;

    [Required]
    public string PayloadJson { get; set; } = "{}";

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public int AttemptCount { get; set; }
}
