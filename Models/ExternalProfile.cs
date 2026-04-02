using System.Text.Json;
using Xllerate.Enums;

namespace Xllerate.Models;

public class ExternalProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Platform Platform { get; set; }
    public required string ProfileUrl { get; set; }
    public string? Username { get; set; }
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
    public string? SyncErrorMessage { get; set; }
    public DateTime? LastSyncedAt { get; set; }

    // Full API response — read by enrichment worker, never queried
    public string? RawData { get; set; }

    public User User { get; set; } = null!;
}