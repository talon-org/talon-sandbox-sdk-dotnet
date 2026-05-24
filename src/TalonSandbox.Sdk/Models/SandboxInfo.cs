using System.Text.Json.Serialization;

namespace TalonSandbox.Models;

/// <summary>Sandbox state as returned by the API.</summary>
public sealed class SandboxInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("image_id")]
    public string? ImageId { get; set; }

    [JsonPropertyName("cpu_millis")]
    public long CpuMillis { get; set; }

    [JsonPropertyName("memory_bytes")]
    public long MemoryBytes { get; set; }

    [JsonPropertyName("idle_timeout_seconds")]
    public long IdleTimeoutSeconds { get; set; }

    [JsonPropertyName("ttl_seconds")]
    public long TtlSeconds { get; set; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("network_policy")]
    public string? NetworkPolicy { get; set; }

    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; set; }
}

internal sealed class SandboxListResponse
{
    [JsonPropertyName("sandboxes")]
    public List<SandboxInfo> Sandboxes { get; set; } = [];
}
