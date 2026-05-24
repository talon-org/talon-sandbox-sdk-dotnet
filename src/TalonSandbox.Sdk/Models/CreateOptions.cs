using System.Text.Json.Serialization;

namespace TalonSandbox.Models;

/// <summary>Options for <see cref="Sandbox.CreateAsync"/>.</summary>
public sealed class CreateOptions
{
    /// <summary>Container image (e.g. "node:20-bookworm").</summary>
    public string? Image { get; set; }

    /// <summary>Resource allocation. Strings like "4GiB" and "30m" are accepted.</summary>
    public ResourceOptions? Resources { get; set; }

    /// <summary>Network policy: "allowlist" | "open" | "sealed".</summary>
    public string? Network { get; set; }

    /// <summary>Startup environment variables.</summary>
    public Dictionary<string, string>? Env { get; set; }

    /// <summary>Idle timeout as duration string (e.g. "30m").</summary>
    public string? Timeout { get; set; }

    /// <summary>Hard TTL as duration string (e.g. "6h").</summary>
    public string? Ttl { get; set; }

    /// <summary>Arbitrary key-value labels.</summary>
    public Dictionary<string, string>? Labels { get; set; }
}

/// <summary>CPU / memory / disk for a sandbox.</summary>
public sealed class ResourceOptions
{
    /// <summary>CPU cores (e.g. 2 = 2000 mCPU, 0.5 = 500 mCPU).</summary>
    public double? Cpu { get; set; }

    /// <summary>Memory as size string (e.g. "4GiB") or bytes.</summary>
    public string? Memory { get; set; }

    /// <summary>Disk as size string (e.g. "10GiB").</summary>
    public string? Disk { get; set; }
}

/// <summary>Options for <see cref="Sandbox.ExposeAsync"/>.</summary>
public sealed class ExposeOptions
{
    /// <summary>When true, returns a signed URL requiring no auth. Defaults to false.</summary>
    public bool Sign { get; set; }

    /// <summary>TTL for the signed token (e.g. "1h"). Only meaningful when Sign=true.</summary>
    public string? Ttl { get; set; }

    /// <summary>Custom subdomain prefix. Conflict returns 409.</summary>
    public string? Subdomain { get; set; }
}

/// <summary>Options for <see cref="Sandbox.ListAsync"/>.</summary>
public sealed class ListOptions
{
    public Dictionary<string, string>? Labels { get; set; }
}

// ── Internal wire DTOs ────────────────────────────────────────────────────────

internal sealed class CreateSandboxRequest
{
    [JsonPropertyName("image_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImageId { get; set; }

    [JsonPropertyName("cpu_millis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long CpuMillis { get; set; }

    [JsonPropertyName("memory_bytes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long MemoryBytes { get; set; }

    [JsonPropertyName("disk_bytes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long DiskBytes { get; set; }

    [JsonPropertyName("network_policy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NetworkPolicy { get; set; }

    [JsonPropertyName("env")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Env { get; set; }

    [JsonPropertyName("idle_timeout_seconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long IdleTimeoutSeconds { get; set; }

    [JsonPropertyName("ttl_seconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long TtlSeconds { get; set; }

    [JsonPropertyName("labels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Labels { get; set; }
}

internal sealed class RunRequest
{
    [JsonPropertyName("command")]
    public IReadOnlyList<string> Command { get; set; } = [];

    [JsonPropertyName("env")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Env { get; set; }

    [JsonPropertyName("cwd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cwd { get; set; }
}

internal sealed class ExecResult
{
    [JsonPropertyName("stdout")]
    public string Stdout { get; set; } = string.Empty;

    [JsonPropertyName("stderr")]
    public string Stderr { get; set; } = string.Empty;

    [JsonPropertyName("exit_code")]
    public int ExitCode { get; set; }

    [JsonPropertyName("duration_ms")]
    public double DurationMs { get; set; }
}

internal sealed class ProcessInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("command")]
    public IReadOnlyList<string> Command { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

internal sealed class ProcessListResponse
{
    [JsonPropertyName("processes")]
    public List<ProcessInfo> Processes { get; set; } = [];
}

internal sealed class EnvGetResponse
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

internal sealed class EnvAllResponse
{
    [JsonPropertyName("env")]
    public Dictionary<string, string> Env { get; set; } = [];
}

internal sealed class EnvSetRequest
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

internal sealed class ErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}
