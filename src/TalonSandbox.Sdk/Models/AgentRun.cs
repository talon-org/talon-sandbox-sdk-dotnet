using System.Text.Json.Serialization;

namespace TalonSandbox.Models;

/// <summary>AgentRunAsync 的可选参数。</summary>
public sealed class AgentRunOptions
{
    /// <summary>最大步骤数，默认 20，服务端硬上限 100。</summary>
    public int MaxSteps { get; set; }

    /// <summary>
    /// 指定 browser-harness 使用的 LLM，例如 "anthropic:claude-sonnet-4-6"。
    /// null 时使用服务端默认值。
    /// </summary>
    public string? LlmModel { get; set; }
}

/// <summary>
/// agent 执行过程中单个步骤的记录。与后端 dto.AgentRunStep 字段对应。
/// </summary>
public sealed class AgentRunStep
{
    /// <summary>步骤序号（从 1 开始）。</summary>
    [JsonPropertyName("step")]
    public int Step { get; set; }

    /// <summary>步骤类型，如 "Page.navigate" / "Input.click" / "result"。</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>LLM 对本步骤的解释（可选）。</summary>
    [JsonPropertyName("thought")]
    public string? Thought { get; set; }

    /// <summary>步骤相关的额外字段（action-specific）。</summary>
    [JsonPropertyName("details")]
    public Dictionary<string, object>? Details { get; set; }
}

/// <summary>
/// POST /v1/sandboxes/{id}/agent/run 的同步响应。
/// 与后端 dto.AgentRunResponse 字段对应。
/// </summary>
public sealed class AgentRunResult
{
    /// <summary>本次 run 的唯一 ID。</summary>
    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    /// <summary>"completed" | "failed" | "timeout"。</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>总耗时（毫秒）。</summary>
    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }

    /// <summary>browser-harness 每一步的结构化记录。</summary>
    [JsonPropertyName("steps")]
    public List<AgentRunStep> Steps { get; set; } = [];

    /// <summary>
    /// browser-harness 最后输出的 result 字段（LLM 自我评估）。
    /// status="completed" 不代表任务成功——任务是否达成 goal 看此字段。
    /// </summary>
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    /// <summary>browser-harness 进程退出码；0 = 正常。</summary>
    [JsonPropertyName("exit_code")]
    public int ExitCode { get; set; }

    /// <summary>browser-harness 的 stderr（失败时辅助排障）。</summary>
    [JsonPropertyName("stderr")]
    public string? Stderr { get; set; }
}

/// <summary>POST /v1/sandboxes/{id}/agent/run 的请求体。与后端 dto.AgentRunRequest 对应。</summary>
internal sealed class AgentRunRequest
{
    [JsonPropertyName("goal")]
    public string Goal { get; set; } = string.Empty;

    /// <summary>最大步骤数。0 时不发送该字段，服务端使用默认值 20。</summary>
    [JsonPropertyName("max_steps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int MaxSteps { get; set; }

    /// <summary>LLM 模型 hint，如 "anthropic:claude-sonnet-4-6"。null 时不发送该字段。</summary>
    [JsonPropertyName("llm_model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LlmModel { get; set; }
}
