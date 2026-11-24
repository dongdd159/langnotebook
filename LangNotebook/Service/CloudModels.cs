using System.Text.Json.Serialization;

namespace LangNotebook.Service;

public sealed class CloudProviderInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Configured { get; set; }
}

public sealed class CloudStatus
{
    public bool Connected { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public bool AutoBackup { get; set; } = true;

    /// <summary>False until this device's user has chosen between restoring a backup and keeping the local data.</summary>
    public bool Armed { get; set; }

    public string? LastBackupUtc { get; set; }
    public bool NeedsReconnect { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceLabel { get; set; } = string.Empty;
    public bool HasSnapshot { get; set; }

    public DateTime? LastBackup => DateTime.TryParse(LastBackupUtc, null,
        System.Globalization.DateTimeStyles.RoundtripKind, out var value)
        ? value.ToUniversalTime()
        : null;
}

public sealed class CloudFile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string ModifiedUtc { get; set; } = string.Empty;
    public long Size { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceLabel { get; set; } = string.Empty;
    public bool ThisDevice { get; set; }

    public DateTime Created => DateTime.TryParse(CreatedUtc, null,
        System.Globalization.DateTimeStyles.RoundtripKind, out var value)
        ? value.ToUniversalTime()
        : DateTime.MinValue;
}

public sealed class CloudResult
{
    public bool Ok { get; set; }

    /// <summary>offline, network, auth-expired, popup-blocked, cancelled, quota, not-configured, not-connected,
    /// busy, invalid, snapshot or provider. Empty when Ok.</summary>
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
    public bool Uploaded { get; set; }
    public string? Skipped { get; set; }
    public CloudFile? File { get; set; }
    public List<CloudFile>? Files { get; set; }
    public List<CloudProviderInfo>? Providers { get; set; }
    public string? Json { get; set; }
    public CloudStatus? Status { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CloudResult))]
internal partial class CloudJsonContext : JsonSerializerContext
{
}

public enum CloudSyncState
{
    Disconnected,
    Synced,
    Pending,
    Working,
    Offline,
    NeedsReconnect,
    DecisionNeeded,
    Error
}

public enum CloudAction
{
    Backup,
    Connect,
    Disconnect,
    Restore,
    Undo
}
